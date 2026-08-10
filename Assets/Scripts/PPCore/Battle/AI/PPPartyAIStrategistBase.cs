/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyAIStrategistBase.cs
 * @author hqrse
 * @date 2026/07/16
 * @brief パーティ戦略構築のベースクラス
 * =====================================*/

using System;
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
    // PPPartyAIProfileDefinition アセット（性格）と PPSkillDefinition のロール別AIスコア（スキルの強さ）で行う
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
                {
                    CustomConsoleLog.Warning("AI", $"{unit.DisplayName}は実行可能な行動候補がなく待機します。");
                    continue;
                }

                // 行動のスコア評価
                foreach (var c in candidates)
                {
                    c.Score = Evaluate(c, situation);
                }

                // 上位3件のスコアを可視化用に出力する
                foreach (var (c, rank) in candidates.OrderByDescending(x => x.Score).Take(3).Select((c, i) => (c, i + 1)))
                {
                    CustomConsoleLog.Verbose("AI",
                        $"{unit.DisplayName} 候補{rank}: {c.Role} {(c.Skill != null ? c.Skill.DisplayName : "通常攻撃")} -> {c.Target?.DisplayName ?? "-"} score={c.Score:F2}");
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
        // 複数ロールを持つスキルは、ロールごとに個別の候補として競わせる
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
                // 通常攻撃はスキル定義を持たないため、基礎AIスコアは PPBattleRules 側の値を使う
                float normalAttackScore = (aContext.Rules as PPBattleRules)?.NormalAttackAIScore ?? 0f;
                list.Add(new PPActionCandidate
                {
                    Unit = u,
                    Role = PPBattleSkillRole.Attack,
                    Cost = PPResourceCost.BaseCost(atkCost),
                    Skill = null,
                    Target = tgt,
                    AIScore = normalAttackScore,
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

                var u = aUnit;
                var s = skill as PPBattleSkill;
                var scope = def.TargetScope;

                // チェックされているロールの数だけ、個別の候補として生成する
                foreach (var role in RoleFlags(def.BattleSkillRole))
                {
                    var target = ResolveSkillTarget(role, aSnap, aFocusTarget);
                    var chosen = target as PPBattleUnit;
                    var resolver = BuildSkillResolver(scope, target);
                    list.Add(new PPActionCandidate
                    {
                        Unit = u,
                        Role = role,
                        Cost = def.Cost,
                        Skill = s,
                        Target = chosen,
                        AIScore = def.RoleScores.Get(role),
                        BuildCommand = _ => new PPSkillCommand(u, s, resolver),
                    });
                }
            }

            return list;
        }

        // aRoles に含まれる単一フラグをすべて列挙する（None は含めない）
        // ロールが増えても Enum.GetValues で拾えるため、このメソッドの修正は不要
        // aRoles : 判定対象のロールフラグ
        // return : 立っているロール単体の列挙
        private static IEnumerable<PPBattleSkillRole> RoleFlags(PPBattleSkillRole aRoles)
        {
            foreach (PPBattleSkillRole role in Enum.GetValues(typeof(PPBattleSkillRole)))
            {
                if (role == PPBattleSkillRole.None) continue;
                if ((aRoles & role) != 0) yield return role;
            }
        }

        // スキルのロールに応じて対象を決める。回復なら最も HP 割合の低い味方、
        // 攻撃なら狙うと決めた敵、それ以外（サポート・スペシャル等）は対象指定なし
        // （BuildSkillResolver がスコープ既定のリゾルバへフォールバックする）
        // aRole : スキルの行動ロール
        // aSnap : パーティ状況スナップショット
        // aTarget : 攻撃時に使う対象
        // return : 対象ユニット。指定不要なら null
        protected static BattleUnit ResolveSkillTarget(PPBattleSkillRole aRole, PPPartyAIContext aSnap, BattleUnit aTarget)
            => aRole switch
            {
                PPBattleSkillRole.Heal => aSnap.LowestHpRatioAlly,
                PPBattleSkillRole.Attack => aTarget,
                _ => null,
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

        // ユニットの配置ロール（PPUnitRole）を、シチュエーション係数が使うスキルロール（PPBattleSkillRole）へ対応付ける
        // aRole : ユニットの配置ロール
        // return : 対応するスキルロール。対応が無ければ null
        private static PPBattleSkillRole? MapUnitRole(PPUnitRole aRole)
            => aRole switch
            {
                PPUnitRole.Attacker => PPBattleSkillRole.Attack,
                PPUnitRole.Supporter => PPBattleSkillRole.Support,
                PPUnitRole.Healer => PPBattleSkillRole.Heal,
                _ => null,
            };

        // ユニットの配置ロールに対応する状況係数を引く
        // パーティ内での行動の優先順位付けに掛かる
        // aRole : ユニットの配置ロール
        // aSituation : 解決済みの状況スコア
        // return : 対応するロールの状況係数。未割り当ての場合は登録済み係数の平均
        protected static float SituationWeightFor(PPUnitRole aRole, PPAISituationScore aSituation)
        {
            var mapped = MapUnitRole(aRole);
            return mapped.HasValue
                ? aSituation.Roles.Resolve(mapped.Value, 1f)
                : aSituation.Roles.Average(1f);
        }

        // 行動候補のスコアを評価する
        // 最終スコア = AIScore（ロール別。スキル定義側 or 通常攻撃は PPBattleRules 側）
        //            × シチュエーション係数（ロール別。状況ルールから解決）
        //            × ロール別重み（プロファイル側の性格）
        // 知能によるノイズは SelectBestCandidate 側で別途載せる
        // aCandidate : 評価する候補
        // aSituation : 解決済みの状況スコア
        // return : この候補の最終スコア
        protected float Evaluate(PPActionCandidate aCandidate, PPAISituationScore aSituation)
        {
            float situationCoefficient = aSituation.Roles.Resolve(aCandidate.Role, 1f);
            float roleWeight = mProfile.Weights.Resolve(aCandidate.Role, 1f);
            return aCandidate.AIScore * situationCoefficient * roleWeight;
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
        protected int RoleOrder(PPBattleSkillRole aRole)
        => aRole switch
        {
            PPBattleSkillRole.Attack => mProfile.Order.Attack,
            PPBattleSkillRole.Support => mProfile.Order.Support,
            PPBattleSkillRole.Heal => mProfile.Order.Heal,
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
