/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyAIStrategistBase.cs
 * @author hqrse
 * @date 2026/07/16
 * @brief パーティ戦略構築のベースクラス
 * =====================================*/

using System.Collections.Generic;
using System.Linq;
using CommandBattleCore;
using CustomConsole;
using UnityEngine;

namespace PPCore
{
    // パーティ単位で行動計画（PPPartyPlan）を立てる AI の基底実装
    // ユニット 1 体ずつが個別に動くのではなく、パーティ全体で
    // 「限られたリソースを誰の何に割り当てるか」を決めるのがこの AI の役割
    // PPEnemyAIDriver が一定間隔で PlanActions を呼び出して駆動する
    // 思考の流れは PlanActions を参照。挙動のチューニングは基本的にコードではなく
    // PPPartyAIProfileDefinition アセット側で行う
    // スコア計算系は全て protected virtual 相当の粒度で切ってあるので、
    // 特殊な敵を作る場合はこのクラスを継承して個別のスコア関数だけを差し替える
    // 乱数は必ず aContext.Rules.RandomProvider を経由すること（UnityEngine.Random は使わない）
    public class PPPartyAIStrategistBase : IPPPartyCommandStrategist
    {
        // ユニット 1 体分の行動希望。採点済みの全候補と、その中から選ばれたベスト候補を保持する
        // パーティ全体で優先順位を付け直す際に、候補一覧ごと持ち回る必要があるため一時的にまとめている
        private sealed class PPPartyWish
        {
            // 行動主体のユニット
            public PPBattleUnit Unit;
            // 採点済みの全候補
            public List<PPActionCandidate> Candidates;
            // ベスト選択
            public PPActionCandidate BestCandidate;
            // ベスト候補のスコア
            public float Score;
        }

        // AI の性格・重み・閾値をまとめた設定アセット
        private readonly PPPartyAIProfileDefinition mProfile;
        // リソース増加トレンドの記録。「待てば撃てるか」の判断に使う
        private readonly PPIncomTrendTracker mTrend = new();

        // 直近の思考で採用された状況ルール名。デバッグ表示用
        public string LastResolvedRuleName {get; private set;} = "Default";

        // aProfile : AI プロファイル。null の場合は既定値のインスタンスを生成して使う
        public PPPartyAIStrategistBase(PPPartyAIProfileDefinition aProfile)
        {
            mProfile = aProfile != null
                ? aProfile
                : ScriptableObject.CreateInstance<PPPartyAIProfileDefinition>();
        }

        // このティックでパーティが取る行動計画を組み立てる
        // 処理の流れは次の通り
        // 1. PPPartyAIContext.Capture でパーティ状況をスナップショット
        // 2. リソース増加トレンドをサンプリング
        // 3. 状況ルールを評価して状況スコアを解決
        // 4. ユニットごとに行動候補を生成し、スコアを付ける
        // 5. 「待って溜めた方が良いか」と比較し、行動した方が良いユニットだけ希望として残す
        // 6. ロール別の状況ウェイトを掛けて希望を優先度順に並べ替える
        // 7. 実リソースを優先度順に確保し、MaxActionsPerTick まで採用する
        // aSelf : 思考主体のパーティ。PPBattleParty でなければ待機を返す
        // aContext : バトルコンテキスト
        // return : 採用された行動の割り当て。何も採用できなければ PPPartyPlan.Wait
        public PPPartyPlan PlanActions(BattleParty aSelf, BattleContext aContext)
        {
            if (aSelf is not PPBattleParty party)
                return PPPartyPlan.Wait;

            // パーティ情報収集
            var snap = PPPartyAIContext.Capture(party, aContext);
            if (snap.AliveMembers.Count == 0 || snap.AliveEnemies.Count == 0)
                return PPPartyPlan.Wait;

            // リソース推移のサンプリング
            mTrend.Sample(snap.Current(PPTypeAttribute.Normal), mProfile.TrendSampleCount);

            // シチュエーション判断
            var situation = ResolveSituationRule(snap);

            // 各ユニットの行動願望の収集(温存を考慮しない)
            // ここでは「他のユニットが使う分」を差し引かない満タンの予算で評価する
            var fullPoolBudget = new PPResourceBudget(party.ResourcePool, 0f);
            var wishes = new List<PPPartyWish>();

            foreach (var unit in snap.AliveMembers)
            {
                // 行動できないならスルー
                if((unit.CurrentRestrictions & ActionRestriction.CannotAct) != 0)
                    continue;

                float intelligence = ResolveIntelligence(unit);
                var focusTarget = ChooseAttackTargetForUnit(unit, snap, intelligence, aContext);

                // ユニットごとに行動候補を収集する
                var candidates = GenerateCandidatesForUnit(unit, snap, focusTarget, aContext);
                if(candidates.Count == 0)
                    continue;

                // 行動のスコア評価
                foreach (var c in candidates)
                {
                    c.Score = Evaluate(c, snap, situation);
                }

                // 待ちに対する評価
                float waitScore = EvaluateWaitForUnit(unit, snap, candidates, fullPoolBudget, situation);

                // 実行不可なものは除外
                var affordable = candidates.Where(c => fullPoolBudget.CanAfford(c.Cost)).ToList();
                // ノイズ入りでベスト選択を出す
                var best = SelectBestCandidate(affordable, intelligence, aContext);

                // 今撃つより待った方が良いと判断した場合は希望を出さない
                if (best != null && best.Score > waitScore)
                {
                    wishes.Add(new PPPartyWish
                    {
                        Unit = unit,
                        Candidates = candidates,
                        BestCandidate = best,
                        Score = best.Score,
                    });
                }
            }

            if(wishes.Count == 0)
                return PPPartyPlan.Wait;

            // 行動願望をもとに役割とシチュエーションを考慮して補正を入れる
            // 元の並び順を index として持っておき、同スコア時の順序を安定させる
            var ordered = wishes
                .Select((w, i) => (wish: w, index: i))
                .OrderByDescending(t => t.wish.Score * SituationWeightFor(ResolveUnitRole(t.wish.Unit), situation))
                .ThenBy(t => t.index) // 同スコアの場合はインデックスの低い順
                .Select(t => t.wish)
                .ToList();

            // 下限なしの実リソースに対し、優先度順に確保を試みる
            var budget = new PPResourceBudget(party.ResourcePool, 0f);
            var picks = new List<PPPartyActionAssignment>();

            foreach (var w in ordered)
            {
                if(picks.Count >= Mathf.Max(1, mProfile.MaxActionsPerTick))
                    break;
                if(!budget.CanAfford(w.BestCandidate.Cost))
                    continue;

                // 先行するユニットがリソースを使った後の残量で待機評価をやり直す
                // 残りが減ったことで「今は待つべき」に判断が変わる場合があるため
                float currentWait = EvaluateWaitForUnit(w.Unit, snap, w.Candidates, budget, situation);
                if(w.Score <= currentWait)
                    continue;
                budget.TrySpend(w.BestCandidate.Cost);
                picks.Add(new PPPartyActionAssignment(w.Unit, w.BestCandidate.BuildCommand(aContext), RoleOrder(w.BestCandidate.Role)));
            }

            return picks.Count == 0 ? PPPartyPlan.Wait : new PPPartyPlan(picks);
        }

        // 状況別にシチュエーションスコアを解決する
        // プロファイルのルールを順に評価し、条件を全て満たしたものでスコアを上書きしていくため、
        // 後ろに定義されたルールほど優先される。どれも成立しなければ既定スコアのまま
        // aSnap : 評価対象のパーティ状況スナップショット
        // return : 解決された状況スコア
        protected PPAISituationScore ResolveSituationRule(PPPartyAIContext aSnap)
        {
            var resolved = mProfile.DefaultScore;
            LastResolvedRuleName = "Default";
            foreach (var rule in mProfile.Rules)
            {
                if(rule == null || rule.Conditions == null || rule.Conditions.Count == 0)
                    continue;

                // 条件は AND 判定。1 つでも満たさなければそのルールは不成立
                bool allMath = true;
                foreach (var condition in rule.Conditions)
                {
                    if (condition == null || !condition.Evaluate(aSnap))
                    {
                        allMath = false;
                        break;
                    }
                }

                if (allMath)
                {
                    LastResolvedRuleName = string.IsNullOrEmpty(rule.Name) ? "(Unnamed)" : rule.Name;
                    resolved = rule.Score;
                }
            }
            CustomConsoleLog.Log("AISituation", $"SelectedRuleName: {LastResolvedRuleName}");
            return resolved;
        }

        // 実行時の知能値を解決する。ユニット個別の値が設定されていればそれを、
        // 0（未設定）ならプロファイルの値を継承する
        // aUnit : 対象ユニット
        // return : 0～1 に丸めた知能値
        protected float ResolveIntelligence(PPBattleUnit aUnit)
            => aUnit.Intelligence > 0f ? Mathf.Clamp01(aUnit.Intelligence) : mProfile.Intelligence;

        // 候補からベストを選ぶ。スコアにノイズを載せてから比較するため、
        // 知能が低いほど最適解を外しやすくなる（ノイズ幅は最大スコアに比例）
        // aCandidates : 選択対象の候補（実行可能なものだけを渡す想定）
        // aIntelligence : この選択に使う知能値（0～100）
        // aContext : 乱数供給元を含むバトルコンテキスト
        // return : 選ばれた候補。候補が空なら null
        protected PPActionCandidate SelectBestCandidate(List<PPActionCandidate> aCandidates, float aIntelligence,
            BattleContext aContext)
        {
            if(aCandidates.Count == 0)
                return null;
            if(aCandidates.Count == 1)
                return aCandidates[0];

            // 知能 1 ならノイズ 0、知能 0 なら最大幅でぶれる
            float maxScore = aCandidates.Max(c => c.Score);
            float noiseRatio = 1f - Mathf.Clamp01(aIntelligence);
            float amplitude = maxScore * mProfile.ActionNoiseAmplitude * noiseRatio;

            PPActionCandidate best = null;
            float bestPerceived = float.NegativeInfinity;
            foreach (var c in aCandidates)
            {
                // 実スコアではなく「AI から見えているスコア」で比較する
                float perceived = c.Score + RandomSigned(aContext) * amplitude;
                if (perceived > bestPerceived)
                {
                    bestPerceived = perceived;
                    best = c;
                }
            }
            return best;
        }

        // -1～1 の符号付き乱数を取得する
        // aContext : 乱数供給元を含むバトルコンテキスト
        protected static float RandomSigned(BattleContext aContext)
            => aContext.Rules.RandomProvider.NextFloat(-1f, 1f);

        // 攻撃対象を抽選する。知能が高いほど HP 最低の敵（＝とどめを刺しやすい相手）を選び、
        // 外れた場合は生存敵からランダムに選ぶ
        // aUnit : 攻撃するユニット
        // aSnap : パーティ状況スナップショット
        // aIntelligence : この選択に使う知能値（0～100）
        // aContext : 乱数供給元を含むバトルコンテキスト
        // return : 攻撃対象。敵が居なければ null
        protected PPBattleUnit ChooseAttackTargetForUnit(PPBattleUnit aUnit, PPPartyAIContext aSnap, float aIntelligence, BattleContext aContext)
        {
            if (aSnap.AliveEnemies.Count == 0)
                return null;
            // 知能が高いほど最低ターゲットを選択しやすい
            float optimalChance = Mathf.Clamp01(aIntelligence);
            if(Chance(optimalChance, aContext))
                return aSnap.LowestHpEnemy;
            int idx = aContext.Rules.RandomProvider.NextInt(aSnap.AliveEnemies.Count);
            return aSnap.AliveEnemies[idx];
        }

        // ユニット 1 体分の行動候補を収集する。通常攻撃と、発動可能な各スキルを候補として並べる
        // この段階ではリソースが足りるかは見ておらず、コスト込みの候補として全て返す
        // aUnit : 対象ユニット
        // aSnap : パーティ状況スナップショット
        // aFocusTarget : このユニットが狙うと決めた攻撃対象
        // aContext : バトルコンテキスト
        // return : スコア未評価の行動候補リスト
        protected List<PPActionCandidate> GenerateCandidatesForUnit(PPBattleUnit aUnit, PPPartyAIContext aSnap, PPBattleUnit aFocusTarget, BattleContext aContext)
        {
            var list = new List<PPActionCandidate>();

            // 通常攻撃
            if (aFocusTarget != null)
            {
                float atkCost = aUnit.ExtraParameters.Get(PPParameterSet.ParameterIdAttackCost)?.CurrentValue ?? 0f;
                // ラムダに載せるためローカルへ退避する
                var u = aUnit;
                var tgt = aFocusTarget;
                list.Add(new PPActionCandidate
                {
                    Unit = u,
                    Role = PPBattleActionRole.Attack,
                    Cost = PPResourceCost.BaseCost(atkCost),
                    Skill = null,
                    Target = tgt,
                    BuildCommand = _ => new PPAttackCommand(u, new SingleEnemyResolver(tgt)),
                });
            }

            // スキル
            foreach (var skill in aUnit.Skills)
            {
                // クールダウン中などで撃てないもの、PP 側の定義を持たないものは候補にしない
                if(!aContext.Rules.CastValidator.Validate(aUnit, skill, aContext).CanCast)
                    continue;
                if(skill.SourceDefinition is not PPSkillDefinition def)
                    continue;

                var role = RoleOf(def);
                var target = ResolveSkillTarget(role, aSnap, aFocusTarget);
                var u = aUnit;
                var s = skill as PPBattleSkill;
                var scope = def.TargetScope;
                var chosen = target as PPBattleUnit;
                list.Add(new PPActionCandidate
                {
                    Unit = u,
                    Role = role,
                    Cost = def.Cost,
                    Skill = s,
                    Target = chosen,
                    BuildCommand = _ => new PPSkillCommand(u, s, BuildSkillResolver(scope, chosen)),
                });
            }

            return list;
        }

        // スキルのロールに応じて対象を決める。回復なら最も HP 割合の低い味方、
        // 攻撃なら狙うと決めた敵、それ以外は対象指定なし（範囲・自己完結スキル）
        // aRole : スキルの行動ロール
        // aSnap : パーティ状況スナップショット
        // aTarget : 攻撃時に使う対象
        // return : 対象ユニット。指定不要なら null
        protected static BattleUnit ResolveSkillTarget(PPBattleActionRole aRole, PPPartyAIContext aSnap, BattleUnit aTarget)
            => aRole switch
            {
                PPBattleActionRole.Heal => aSnap.LowestHpRatioAlly,
                PPBattleActionRole.Attack => aTarget,
                _ => null,
            };

        // スキル定義のスキルロールを、AI 側の行動ロールへ変換する
        // aDef : スキル定義
        // return : 対応する行動ロール
        protected static PPBattleActionRole RoleOf(PPSkillDefinition aDef)
            => aDef.BattleSkillRole switch
            {
                PPBattleSkillRole.Attack => PPBattleActionRole.Attack,
                PPBattleSkillRole.Heal => PPBattleActionRole.Heal,
                PPBattleSkillRole.Support => PPBattleActionRole.Support,
                _ => PPBattleActionRole.None,
            };

        // AI が選んだ対象を焼き込んだターゲットリゾルバを作る
        // 単体対象のスコープのみ差し替え、範囲スコープはスコープ既定のリゾルバをそのまま使う
        // aScope : スキルのターゲットスコープ
        // aTarget : AI が選んだ対象。null ならスコープ既定を使う
        // return : コマンドに渡すターゲットリゾルバ
        protected static ITargetResolver BuildSkillResolver(TargetScope aScope, BattleUnit aTarget)
        {
            if (aTarget == null)
                return aScope.CreateResolver();
            return aScope switch
            {
                TargetScope.SingleEnemy => new SingleEnemyResolver(aTarget),
                TargetScope.SingleAlly => new SingleAllyResolver(aTarget),
                _ => aScope.CreateResolver()
            };
        }

        // ユニットに割り当てられたロールを取得する
        // aUnit : 対象ユニット
        protected static PPUnitRole ResolveUnitRole(PPBattleUnit aUnit) => aUnit.AssignedRole;

        // ユニットのロールに対応する状況ウェイトを引く
        // パーティ内での行動の優先順位付けに掛かる
        // aRole : ユニットのロール
        // aSituation : 解決済みの状況スコア
        // return : ロールに対応するウェイト。未割り当ての場合は 3 種の平均
        protected static float SituationWeightFor(PPUnitRole aRole, PPAISituationScore aSituation)
            => aRole switch
            {
                PPUnitRole.Attacker => aSituation.Attack,
                PPUnitRole.Supporter => aSituation.Support,
                PPUnitRole.Healer => aSituation.Heal,
                _ => (aSituation.Attack + aSituation.Support + aSituation.Heal) / 3f,   // 未割り当ては平均値を返却
            };

        // 候補の行動ロールに対応する、ユニット固有のスコア倍率を引く
        // 「このユニットは攻撃を好む」といった個体差を表現する
        // aUnit : 行動主体のユニット
        // aCandidate : 評価中の候補
        // return : スコアに掛ける倍率。対応ロールが無ければ 1
        protected static float UnitScoreMultiplier(PPBattleUnit aUnit, PPActionCandidate aCandidate)
        {
            var mod = aUnit.ScoreModifier;
            return aCandidate.Role switch
            {
                PPBattleActionRole.Attack => mod.Attack,
                PPBattleActionRole.Support => mod.Support,
                PPBattleActionRole.Heal => mod.Heal,
                _ => 1f,
            };
        }

        // 行動候補のスコアを評価する。ロール別のスコア関数へ振り分け、
        // 最後にユニット固有の倍率を掛ける。通常攻撃と攻撃スキルはスキルの有無で分岐する
        // aCandidate : 評価する候補
        // aSnap : パーティ状況スナップショット
        // aScore : 解決済みの状況スコア
        // return : この候補の最終スコア
        protected float Evaluate(PPActionCandidate aCandidate, PPPartyAIContext aSnap, PPAISituationScore aScore)
        {
            float baseScore = aCandidate.Role switch
            {
                PPBattleActionRole.Attack when aCandidate.Skill == null => ScoreBasicAttack(aCandidate, aSnap, aScore),
                PPBattleActionRole.Attack => ScoreSkillAttack(aCandidate, aSnap, aScore),
                PPBattleActionRole.Support => ScoreSupport(aCandidate, aSnap, aScore),
                PPBattleActionRole.Heal => ScoreHeal(aCandidate, aSnap, aScore),
                _ => 0f,
            };
            return baseScore * UnitScoreMultiplier(aCandidate.Unit, aCandidate);
        }

        // 各スコア計算の共通ベース
        // 「基礎点 + バイアス × 状況係数」に、ロール重み・状況倍率・攻撃性・コスト効率を掛け合わせる
        // aWeight : プロファイルのロール別重み
        // aSituationMul : 状況スコアによる倍率
        // aBaseScore : 行動種別ごとの基礎点
        // aBias : 状況係数に掛けるバイアス
        // aFactor : 状況係数（とどめやすさ・人数比・緊急度など）
        // aUseAggression : 攻撃性（Aggression）を反映するか。攻撃系のみ true
        // aCost : この行動のコスト。コスト効率の算出に使う
        // aAggressionMultiplier : 状況による攻撃性の追加倍率
        // return : 算出されたスコア
        protected float ScoreWeighted(float aWeight, float aSituationMul, float aBaseScore, float aBias, float aFactor,
            bool aUseAggression, PPResourceCost aCost, float aAggressionMultiplier = 1f)
        {
            float raw = aBaseScore + aBias * aFactor;
            float aggr = aUseAggression ? mProfile.Aggression * aAggressionMultiplier : 1f;
            return aWeight * aSituationMul * raw * aggr * CostEfficiency(aCost);
        }

        // 通常攻撃のスコアを計算する。対象の HP 割合が低いほど（＝とどめを刺しやすいほど）高くなる
        // aCandidate : 評価する候補
        // aSnap : パーティ状況スナップショット
        // aSituation : 解決済みの状況スコア
        // return : 算出されたスコア
        protected float ScoreBasicAttack(PPActionCandidate aCandidate, PPPartyAIContext aSnap, PPAISituationScore aSituation)
        {
            var s = mProfile.AttackScore;
            float finishBias = 1f - PPPartyAIContext.HpRatio(aCandidate.Target);
            return ScoreWeighted(
                mProfile.Weights.Attack,
                aSituation.Attack,
                s.BaseScore,
                s.HpRatioBias,
                finishBias,
                true,
                aCandidate.Cost,
                aSituation.AggressionMultiplier);
        }

        // 攻撃スキルのスコアを計算する
        // 単体対象なら通常攻撃と同じくとどめやすさを、対象を持たない範囲スキルなら
        // プロファイルの範囲スキル評価値を係数として使う
        // aCandidate : 評価する候補
        // aSnap : パーティ状況スナップショット
        // aSituation : 解決済みの状況スコア
        // return : 算出されたスコア
        protected float ScoreSkillAttack(PPActionCandidate aCandidate, PPPartyAIContext aSnap, PPAISituationScore aSituation)
        {
            var s = mProfile.SkillScore;

            float finishBias = aCandidate.Target != null
                ? 1f - PPPartyAIContext.HpRatio(aCandidate.Target)
                : s.RangeSkillScore;

            return ScoreWeighted(
                mProfile.Weights.Attack,
                aSituation.Attack,
                s.BaseScore,
                s.HpRatioBias,
                finishBias,
                true,
                aCandidate.Cost,
                aSituation.AggressionMultiplier);
        }

        // サポートスキルのスコアを計算する。生存人数が多いほど恩恵が大きいとみなして高くなる
        // aCandidate : 評価する候補
        // aSnap : パーティ状況スナップショット
        // aSituation : 解決済みの状況スコア
        // return : 算出されたスコア
        protected float ScoreSupport(PPActionCandidate aCandidate, PPPartyAIContext aSnap, PPAISituationScore aSituation)
        {
            var s = mProfile.SupportScore;
            float allies = Mathf.Clamp01(aSnap.AliveMembers.Count / s.MemberCountScore);
            return ScoreWeighted(
                mProfile.Weights.Support,
                aSituation.Support,
                s.BaseScore,
                s.MemberCountBias,
                allies,
                false,
                aCandidate.Cost);
        }

        // 回復スキルのスコアを計算する
        // 味方の最低 HP 割合から緊急度を求め、閾値未満なら回復不要として 0 を返す
        // 緊急度は 2 乗して扱うため、瀕死に近づくほど急激にスコアが跳ね上がる
        // aCandidate : 評価する候補
        // aSnap : パーティ状況スナップショット
        // aSituation : 解決済みの状況スコア
        // return : 算出されたスコア。閾値未満なら 0
        protected float ScoreHeal(PPActionCandidate aCandidate, PPPartyAIContext aSnap, PPAISituationScore aSituation)
        {
            var s = mProfile.HealScore;
            float severity = 1f - aSnap.LowestAllyHpRatio;
            if (severity < s.Threshold) return 0f;

            float urgency = severity * severity;
            return ScoreWeighted(
                mProfile.Weights.Heal,
                aSituation.Heal,
                0f,
                s.HpRatioBias,
                urgency,
                false,
                aCandidate.Cost);
        }

        // 消費コストによるスコア減少率を計算する
        // 基準コストに対する比率が大きいほど 1 から下がっていくため、
        // 同程度の効果なら安い行動が優先される
        // aCost : 評価する行動のコスト。無コストなら 1 を返す
        // return : スコアに掛ける 0～1 の効率係数
        protected float CostEfficiency(PPResourceCost aCost)
        {
            if(aCost == null || aCost.IsFree) return 1f;
            var cs = mProfile.CostScore;
            return 1f / (1f + mProfile.CostSensitivity * (aCost.Total / cs.ReferenceCost));
        }

        // 「今は撃たずに待ってリソースを溜めた方が良いか」を評価する
        // 今の残量では撃てない候補のうち最もスコアの高いものを探し、
        // 不足分をリソース増加トレンドで割って「あと何 Tick 待てば撃てるか」を見積もる
        // それが許容 Tick 数以内なら、その候補のスコアを待機スコアとして返す
        // （＝呼び出し元がこれと現在のベスト候補を比較し、上回らなければ行動を見送る）
        // aUnit : 対象ユニット
        // aSnap : パーティ状況スナップショット
        // aCandidates : このユニットの採点済み候補一覧
        // aBudget : 判定に使うリソース予算。確保済み分が反映されている
        // aSituation : 解決済みの状況スコア
        // return : 待機の価値を表すスコア。待つ意味がなければ 0
        protected float EvaluateWaitForUnit(PPBattleUnit aUnit, PPPartyAIContext aSnap, List<PPActionCandidate> aCandidates, PPResourceBudget aBudget, PPAISituationScore aSituation)
        {
            // 危機状態は溜め評価を放棄する
            if(aSnap.IsCrisis)
                return 0f;

            // もう少しあれば撃てる後方の中で最もスコアが高いものの探索
            var upcoming = aCandidates
                .Where(c => !aBudget.CanAfford(c.Cost))
                .OrderByDescending(c => c.Score)
                .FirstOrDefault();
            if(upcoming == null)
                return 0f;

            // リソース推移からもう少しで撃てるスキルがどのタイミングで撃てるのか予想する
            // 増加が見込めない場合は無限大となり、必ず「待たない」判断になる
            float shortfall = upcoming.Cost.Get(PPTypeAttributeDefinition.BaseIndex) - aBudget.Remaining(PPTypeAttribute.Normal);
            float gainPerTick = mTrend.AverageRecentGainPerTick;
            float ticksNeeded = gainPerTick > 0f ? shortfall / gainPerTick : float.PositiveInfinity;

            // AIプロファイルの警戒度が高いほど短いTick数でしか待たない(溜められると判断しない)
            // パーティ種別の忍耐係数とシチュエーションによる補正を掛けて待つことに意味があるか判断する
            float allowedTicks = Mathf.Lerp(6f, 1f, mProfile.Caution) * aSnap.PatienceCoefficient * aSituation.PatienceMultiplier;

            return ticksNeeded > allowedTicks ? 0f : upcoming.Score;
        }

        // ロールごとの実行順序を引く。同一ティック内でどの行動を先に処理するかの並び順になる
        // aRole : 行動ロール
        // return : プロファイルで設定された実行順序値
        protected int RoleOrder(PPBattleActionRole aRole)
        => aRole switch
        {
            PPBattleActionRole.Attack => mProfile.Order.Attack,
            PPBattleActionRole.Support => mProfile.Order.Support,
            PPBattleActionRole.Heal => mProfile.Order.Heal,
            _ => mProfile.Order.Default,
        };

        // 0～1 の確率で成否を判定する。内部では 100 分率の整数抽選に落として比較する
        // a01 : 成功確率（0～1）。範囲外は丸められる
        // aContext : 乱数供給元を含むバトルコンテキスト
        // return : 成功なら true
        protected static bool Chance(float a01, BattleContext aContext)
        {
            a01 = Mathf.Clamp01(a01);
            return aContext.Rules.RandomProvider.NextInt(100) < Mathf.RoundToInt(a01 * 100f);
        }
    }
}
