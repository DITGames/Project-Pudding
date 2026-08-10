/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyAIResourceStrategist.cs
 * @author hqrse
 * @date 2026/08/10
 * @brief 共有リソースの運用を中心に据えたパーティAI
 * =====================================*/

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CommandBattleCore;
using CustomConsole;
using UnityEngine;

namespace PPCore
{
    // 共有リソースの運用を中心に据えたパーティ AI
    //
    // リソースがパーティ共有である以上、あるユニットの消費は他の全ユニットの選択肢を直接奪う。
    // つまり行動決定は「各ユニットが何をするか」ではなく「限られた予算を何に割り当てるか」という配分問題になる。
    // このクラスはその配分を、次の順序で解く。
    //   1. 状況ルールから作戦（リソースの使い方の方針）を決める
    //   2. 保険・取り置き・支出上限を差し引いて予算を決め、リソース 1 点の価値 λ を求める
    //   3. 各ユニットに行動候補を出させ、戦況を反映した効用を付ける
    //   4. 効用密度の高い順に、予算の範囲で採用する
    //
    // 行動を選んだ結果として消費が決まるのではなく、先に支出方針を決めてから中身を埋めるのが要点。
    // λ を導入すると「効用 > λ × コスト」という行動ごとの独立した判定に分解でき、
    // 「何も λ を超えないなら貯める」も自動的に導かれるため、待機を特別扱いする必要がなくなる。
    //
    // 挙動のチューニングはコードではなく PPPartyAIProfileDefinition アセットで行う。
    // 乱数は必ず aContext.Rules.RandomProvider を経由すること（UnityEngine.Random は使わない）
    public class PPPartyAIResourceStrategist : IPPPartyCommandStrategist
    {
        // AI の性格・重み・閾値をまとめた設定アセット
        protected readonly PPPartyAIProfileDefinition mProfile;
        // 属性別の収入ペース。溜め判断と λ の算出に使う
        protected readonly PPIncomeTracker mTracker = new();
        // 同一ティック内で採用済みの行動の効果を記録する帳簿
        protected readonly PPTickLedger mLedger = new();
        // 効用評価器
        protected readonly PPActionUtilityEvaluator mEvaluator;
        // 予算計画層
        protected readonly PPPartyBudgetPlanner mPlanner;
        // 配分器
        protected readonly PPPartyAIAllocator mAllocator;

        // 収入区間を最後に確定させたターン数。思考周期ではなくティックに同期させるために持つ
        private int mLastCommittedTurn = -1;
        // 直近の思考で適用された状況ルール名。デバッグ表示用
        private readonly List<string> mResolvedRuleNames = new();
        // AI スコア未設定の警告を出し済みのスキル×ロール
        // 思考のたびに警告を出すと Custom Console が埋まって他のログを追えなくなるため、
        // 同じ組み合わせについては 1 回だけ知らせる
        private readonly HashSet<string> mWarnedMissingScores = new();

        // 直近の思考で適用された状況ルール名を連結したもの。デバッグ表示用
        public string LastResolvedRuleName { get; private set; } = "Default";

        // 特殊な敵を作る場合は、差し替えたい層だけをコンストラクタへ渡す
        // 生成を virtual メソッドに任せると、派生クラスのフィールド初期化前に呼ばれてしまうため、
        // 拡張点は注入で提供する
        // aProfile : AI プロファイル。null の場合は既定値のインスタンスを生成して使う
        // aEvaluator : 効用評価器。null なら既定の実装を使う
        // aPlanner : 予算計画層。λ の求め方を変えたい場合に差し替える。null なら既定の実装を使う
        // aAllocator : 配分器。採用の仕方を変えたい場合に差し替える。null なら既定の実装を使う
        public PPPartyAIResourceStrategist(PPPartyAIProfileDefinition aProfile,
            PPActionUtilityEvaluator aEvaluator = null,
            PPPartyBudgetPlanner aPlanner = null,
            PPPartyAIAllocator aAllocator = null)
        {
            mProfile = aProfile != null
                ? aProfile
                : ScriptableObject.CreateInstance<PPPartyAIProfileDefinition>();

            mEvaluator = aEvaluator ?? new PPActionUtilityEvaluator();
            mPlanner = aPlanner ?? new PPPartyBudgetPlanner();
            mAllocator = aAllocator ?? new PPPartyAIAllocator(mProfile, mEvaluator);
        }

        // このティックでパーティが取る行動計画を組み立てる
        // aSelf : 思考主体のパーティ。PPBattleParty でなければ待機を返す
        // aContext : バトルコンテキスト
        // return : 採用された行動の割り当て。何も採用できなければ PPPartyPlan.Wait
        public PPPartyPlan PlanActions(BattleParty aSelf, BattleContext aContext)
        {
            if (aSelf is not PPBattleParty party)
                return PPPartyPlan.Wait;

            var snap = PPPartyAIContext.Capture(party, aContext);
            if (snap.AliveMembers.Count == 0 || snap.AliveEnemies.Count == 0)
                return PPPartyPlan.Wait;

            // 収入の観測はプールのイベント経由で行う。二重購読は Bind 側で弾かれる
            mTracker.Bind(party.ResourcePool);
            CommitElapsedIntervals(aContext);

            var doctrine = ResolveDoctrine(snap);
            var plan = mPlanner.Plan(snap, doctrine, mProfile, mTracker);

            mLedger.Clear();
            var candidates = GenerateCandidates(snap, aContext, doctrine);
            if (candidates.Count == 0)
                return PPPartyPlan.Wait;

            var picks = mAllocator.Allocate(candidates, plan, doctrine, mLedger, aContext);
            ReportThink(snap, aContext, doctrine, plan, candidates, picks.Count);

            return picks.Count == 0 ? PPPartyPlan.Wait : new PPPartyPlan(picks);
        }

        // リソースプールの購読を解除する。バトル終了時に呼び出して参照を残さないようにする
        public void Unbind() => mTracker.Unbind();

        // 経過したティックの分だけ収入区間を確定させる
        // 思考間隔ではなくティックを基準にすることで、思考間隔を変えても
        // 「1 区間あたりの収入」の意味が変わらないようにしている
        // aContext : バトルコンテキスト
        protected virtual void CommitElapsedIntervals(BattleContext aContext)
        {
            int sampleCount = Mathf.Max(1, mProfile.TrendSampleCount);

            // 初回は基準となるターンを覚えるだけにして、いきなり大量の空区間を積まない
            if (mLastCommittedTurn < 0)
            {
                mLastCommittedTurn = aContext.TurnCount;
                return;
            }

            int elapsed = Mathf.Clamp(aContext.TurnCount - mLastCommittedTurn, 0, sampleCount);
            for (int i = 0; i < elapsed; i++)
            {
                mTracker.CommitInterval(sampleCount);
            }
            mLastCommittedTurn = aContext.TurnCount;
        }

        // 状況ルールを評価して作戦を解決する
        // 既定作戦を起点に、成立したルールを優先度の小さい順へ重ねる
        // 全置換ではなく差分適用にしてあるため、ルールを 1 つ足しただけで
        // 既定の調整が黙って失われることはない
        // aSnap : 評価対象のパーティ状況スナップショット
        // return : 解決された作戦
        protected virtual PPAIDoctrine ResolveDoctrine(PPPartyAIContext aSnap)
        {
            var doctrine = PPAIDoctrine.From(mProfile.DefaultDoctrine);
            mResolvedRuleNames.Clear();

            if (mProfile.Rules != null)
            {
                var matched = mProfile.Rules
                    .Select((rule, index) => (rule, index))
                    .Where(t => IsRuleMatched(t.rule, aSnap))
                    .OrderBy(t => t.rule.Priority)
                    .ThenBy(t => t.index);

                foreach (var (rule, _) in matched)
                {
                    rule.Override?.ApplyTo(doctrine);
                    // 何を上書きしたルールなのかまで残す。名前だけだと調整時に効き目を追えない
                    string name = string.IsNullOrEmpty(rule.Name) ? "(Unnamed)" : rule.Name;
                    string overrides = rule.Override?.BuildOverrideSummary() ?? "(なし)";
                    mResolvedRuleNames.Add($"{name}[{overrides}]");
                }
            }

            LastResolvedRuleName = mResolvedRuleNames.Count == 0
                ? "Default"
                : string.Join(" → ", mResolvedRuleNames);

            CustomConsoleLog.Verbose("AI", $"適用ルール: {LastResolvedRuleName}");
            return doctrine;
        }

        // ルールが成立しているかを判定する。条件は AND 判定で、1 つでも満たさなければ不成立
        // aRule : 判定するルール
        // aSnap : パーティ状況スナップショット
        // return : 成立している場合 true
        protected static bool IsRuleMatched(PPPartyAISituationRule aRule, PPPartyAIContext aSnap)
        {
            if (aRule?.Conditions == null || aRule.Conditions.Count == 0)
                return false;

            foreach (var condition in aRule.Conditions)
            {
                if (condition == null || !condition.Evaluate(aSnap))
                    return false;
            }
            return true;
        }

        // 全ユニット分の行動候補を生成し、効用まで求める
        // 候補はユニットごとにまとめず、パーティ全体でフラットな 1 本のリストとして返す
        // aSnap : パーティ状況スナップショット
        // aContext : バトルコンテキスト
        // aDoctrine : 解決済みの作戦
        // return : 効用計算済みの候補一覧
        protected virtual List<PPActionCandidate> GenerateCandidates(PPPartyAIContext aSnap,
            BattleContext aContext, PPAIDoctrine aDoctrine)
        {
            var candidates = new List<PPActionCandidate>();

            foreach (var unit in aSnap.AliveMembers)
            {
                if ((unit.CurrentRestrictions & ActionRestriction.CannotAct) != 0)
                    continue;

                // 対象候補は TargetFilters を通して取得する
                // 直接スナップショットから選ぶと挑発・対象不可などのフィルタを無視してしまい、
                // AI が想定した対象と実際に殴る対象が食い違う
                var enemies = aContext.ResolveTargets(unit, new AllEnemiesResolver());
                var allies = aContext.ResolveTargets(unit, new AllAlliesResolver());

                float intelligence = ResolveIntelligence(unit);
                var focusTarget = ChooseAttackTarget(enemies, intelligence, aContext);

                AddNormalAttackCandidate(candidates, unit, focusTarget, aContext);
                AddSkillCandidates(candidates, unit, focusTarget, allies, enemies, aContext);
            }

            foreach (var candidate in candidates)
            {
                mEvaluator.Evaluate(candidate, aDoctrine, mLedger);
            }
            return candidates;
        }

        // 通常攻撃の候補を追加する
        // aCandidates : 追加先
        // aUnit : 行動するユニット
        // aTarget : 攻撃対象。null なら追加しない
        // aContext : バトルコンテキスト
        protected virtual void AddNormalAttackCandidate(List<PPActionCandidate> aCandidates,
            PPBattleUnit aUnit, PPBattleUnit aTarget, BattleContext aContext)
        {
            if (aTarget == null)
                return;

            float attackCost = aUnit.ExtraParameters.Get(PPParameterSet.ParameterIdAttackCost)?.CurrentValue ?? 0f;
            // 通常攻撃はスキル定義を持たないため、基礎 AI スコアは拡張ルール側の値を使う
            float baseScore = (aContext.Rules as PPBattleRules)?.NormalAttackAIScore ?? 0f;

            // ラムダに載せるためローカルへ退避する
            var unit = aUnit;
            var target = aTarget;

            aCandidates.Add(new PPActionCandidate
            {
                Unit = unit,
                Role = PPBattleSkillRole.Attack,
                Cost = PPResourceCost.BaseCost(attackCost),
                Skill = null,
                Target = target,
                AIScore = baseScore,
                Estimate = PPEffectEstimate.FromDamage(PPDamageUtility.ResolveAttackDamage(unit, target)),
                BuildCommand = _ => new PPAttackCommand(unit, new SingleEnemyResolver(target)),
            });
        }

        // スキルの候補を追加する
        // リソース不足だけは候補として残す。ここで弾いてしまうと、
        // 「今は買えないが溜めれば撃てる」スキルが検討対象から消え、溜めの判断が成立しなくなる
        // aCandidates : 追加先
        // aUnit : 行動するユニット
        // aFocusTarget : このユニットが狙うと決めた攻撃対象
        // aAllies : フィルタ適用済みの味方候補
        // aEnemies : フィルタ適用済みの敵候補
        // aContext : バトルコンテキスト
        protected virtual void AddSkillCandidates(List<PPActionCandidate> aCandidates, PPBattleUnit aUnit,
            PPBattleUnit aFocusTarget, List<BattleUnit> aAllies, List<BattleUnit> aEnemies, BattleContext aContext)
        {
            foreach (var skill in aUnit.Skills)
            {
                var validation = aContext.Rules.CastValidator.Validate(aUnit, skill, aContext);
                if (!validation.CanCast && validation.Reason != CastFailReason.NotEnoughResource)
                    continue;
                if (skill.SourceDefinition is not PPSkillDefinition definition)
                    continue;

                var unit = aUnit;
                var battleSkill = skill as PPBattleSkill;
                var scope = definition.TargetScope;

                foreach (var role in RoleFlags(definition.BattleSkillRole))
                {
                    float score = definition.RoleScores.Get(role);
                    // スコア未設定は効用 0 となり採用されない
                    // 「設定し忘れたので静かに動かない」を見逃さないよう警告を出しつつ、
                    // 候補としては残してデバッグウィンドウから却下理由を追えるようにする
                    if (score <= 0f)
                    {
                        WarnMissingScoreOnce(definition, role);
                    }

                    var target = ResolveSkillTarget(role, definition, unit, aFocusTarget, aAllies, aEnemies, aContext);
                    var resolver = BuildSkillResolver(scope, target);
                    var chosen = target as PPBattleUnit;

                    aCandidates.Add(new PPActionCandidate
                    {
                        Unit = unit,
                        Role = role,
                        Cost = definition.Cost,
                        Skill = battleSkill,
                        Target = chosen,
                        AIScore = score,
                        Estimate = definition.EstimateFor(unit, chosen, aContext),
                        BuildCommand = _ => new PPSkillCommand(unit, battleSkill, resolver),
                    });
                }
            }
        }

        // AI スコア未設定の警告を、同じスキル×ロールにつき 1 回だけ出す
        // aDefinition : 対象のスキル定義
        // aRole : 対象のロール
        protected void WarnMissingScoreOnce(PPSkillDefinition aDefinition, PPBattleSkillRole aRole)
        {
            string key = $"{aDefinition.SkillId}:{aRole}";
            if (!mWarnedMissingScores.Add(key))
                return;

            CustomConsoleLog.Warning("AI",
                $"{aDefinition.DisplayName}の{aRole}ロールにAIスコアが設定されていないため、この行動は選ばれません。");
        }

        // aRoles に含まれる単一フラグをすべて列挙する（None は含めない）
        // aRoles : 判定対象のロールフラグ
        // return : 立っているロール単体の列挙
        protected static IEnumerable<PPBattleSkillRole> RoleFlags(PPBattleSkillRole aRoles)
        {
            foreach (PPBattleSkillRole role in System.Enum.GetValues(typeof(PPBattleSkillRole)))
            {
                if (role == PPBattleSkillRole.None) continue;
                if ((aRoles & role) != 0) yield return role;
            }
        }

        // スキルのロールに応じて対象を決める
        // 回復なら最も HP 割合の低い味方、攻撃なら狙うと決めた敵、
        // サポートはスコープに応じて味方／敵から効果の重複しない相手を選ぶ
        // 範囲スコープとロール未分類は対象指定なし（BuildSkillResolver がスコープ既定へフォールバックする）
        // aRole : スキルの行動ロール
        // aDefinition : 対象のスキル定義
        // aSource : スキル発動者
        // aFocusTarget : 攻撃時に使う対象
        // aAllies : フィルタ適用済みの味方候補
        // aEnemies : フィルタ適用済みの敵候補
        // aContext : バトルコンテキスト
        // return : 対象ユニット。指定不要なら null
        protected virtual BattleUnit ResolveSkillTarget(PPBattleSkillRole aRole, PPSkillDefinition aDefinition,
            PPBattleUnit aSource, PPBattleUnit aFocusTarget, List<BattleUnit> aAllies, List<BattleUnit> aEnemies,
            BattleContext aContext)
            => aRole switch
            {
                PPBattleSkillRole.Heal => FindLowestHpRatio(aAllies),
                PPBattleSkillRole.Attack => aFocusTarget,
                PPBattleSkillRole.Support => aDefinition.TargetScope switch
                {
                    TargetScope.SingleAlly => ChooseSupportTarget(aDefinition, aSource, aAllies, aContext),
                    TargetScope.SingleEnemy => ChooseSupportTarget(aDefinition, aSource, aEnemies, aContext),
                    _ => null,
                },
                _ => null,
            };

        // サポート（バフ・デバフ）の対象を選ぶ
        // 同じ効果が既に付いている相手を避けることで、無駄な掛け直しを防ぐ
        // 全員に付与済みの場合は先頭の候補を返し、効用計算側で重複として減点させる
        // （ここで null を返すと範囲スコープ扱いになり、対象状態を一切見なくなってしまう）
        // aDefinition : 対象のスキル定義
        // aSource : スキル発動者
        // aCandidates : フィルタ適用済みの対象候補
        // aContext : バトルコンテキスト
        // return : 対象ユニット。候補が無ければ null
        protected virtual BattleUnit ChooseSupportTarget(PPSkillDefinition aDefinition, PPBattleUnit aSource,
            List<BattleUnit> aCandidates, BattleContext aContext)
        {
            if (aCandidates == null || aCandidates.Count == 0)
                return null;

            BattleUnit first = null;
            foreach (var candidate in aCandidates)
            {
                if (candidate == null || !candidate.IsAlive)
                    continue;

                first ??= candidate;

                var estimate = aDefinition.EstimateFor(aSource, candidate, aContext);
                // 付与効果を持たないスキルは重複の概念が無いので、そのまま最初の候補を使う
                if (string.IsNullOrEmpty(estimate.StatusEffectId))
                    return candidate;

                if (!candidate.ActiveStatusEffects.Exists(e => e.EffectId == estimate.StatusEffectId))
                    return candidate;
            }
            return first;
        }

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
                _ => aScope.CreateResolver(),
            };
        }

        // 攻撃対象を抽選する。知能が高いほど HP の低い相手（とどめを刺しやすい相手）を選ぶ
        // aEnemies : フィルタ適用済みの敵候補
        // aIntelligence : この選択に使う知能値（0〜1）
        // aContext : 乱数供給元を含むバトルコンテキスト
        // return : 攻撃対象。候補が無ければ null
        protected virtual PPBattleUnit ChooseAttackTarget(List<BattleUnit> aEnemies, float aIntelligence,
            BattleContext aContext)
        {
            if (aEnemies == null || aEnemies.Count == 0)
                return null;

            if (Chance(aIntelligence, aContext))
            {
                return FindLowestHp(aEnemies) as PPBattleUnit;
            }

            int index = aContext.Rules.RandomProvider.NextInt(aEnemies.Count);
            return aEnemies[index] as PPBattleUnit;
        }

        // HP 実数値が最も低い生存ユニットを探す
        // aUnits : 探索対象
        // return : 該当ユニット。候補が無ければ null
        protected static BattleUnit FindLowestHp(List<BattleUnit> aUnits)
        {
            BattleUnit found = null;
            float lowest = float.MaxValue;
            foreach (var unit in aUnits)
            {
                if (unit == null || !unit.IsAlive) continue;
                float hp = unit.Parameters.Hp.CurrentValue;
                if (hp < lowest)
                {
                    lowest = hp;
                    found = unit;
                }
            }
            return found;
        }

        // HP 割合が最も低い生存ユニットを探す
        // aUnits : 探索対象
        // return : 該当ユニット。候補が無ければ null
        protected static BattleUnit FindLowestHpRatio(List<BattleUnit> aUnits)
        {
            BattleUnit found = null;
            float lowest = float.MaxValue;
            foreach (var unit in aUnits)
            {
                if (unit == null || !unit.IsAlive) continue;
                float max = unit.Parameters.Hp.Max.CurrentValue;
                float ratio = max <= 0f ? 0f : unit.Parameters.Hp.CurrentValue / max;
                if (ratio < lowest)
                {
                    lowest = ratio;
                    found = unit;
                }
            }
            return found;
        }

        // 実行時の知能値を解決する
        // ユニット個別の値が設定されていればそれを、0（未設定）ならプロファイルの値を継承する
        // aUnit : 対象ユニット
        // return : 0〜1 に丸めた知能値
        protected float ResolveIntelligence(PPBattleUnit aUnit)
            => aUnit.Intelligence > 0f ? Mathf.Clamp01(aUnit.Intelligence) : Mathf.Clamp01(mProfile.Intelligence);

        // 0〜1 の確率で成否を判定する。内部では 100 分率の整数抽選に落として比較する
        // a01 : 成功確率（0〜1）。範囲外は丸められる
        // aContext : 乱数供給元を含むバトルコンテキスト
        // return : 成功なら true
        protected static bool Chance(float a01, BattleContext aContext)
        {
            a01 = Mathf.Clamp01(a01);
            return aContext.Rules.RandomProvider.NextInt(100) < Mathf.RoundToInt(a01 * 100f);
        }

        // 思考の内訳をデバッグウィンドウへ通知する
        // Conditional によりエディタ以外では呼び出しごと消えるため、
        // レポートの組み立てコストはプレイヤービルドに一切かからない
        // aSnap : パーティ状況スナップショット
        // aContext : バトルコンテキスト
        // aDoctrine : 解決済みの作戦
        // aPlan : 予算計画
        // aCandidates : 全候補
        // aAdoptedCount : 採用された行動数
        [Conditional("UNITY_EDITOR")]
        protected virtual void ReportThink(PPPartyAIContext aSnap, BattleContext aContext, PPAIDoctrine aDoctrine,
            PPPartyBudgetPlan aPlan, List<PPActionCandidate> aCandidates, int aAdoptedCount)
        {
            // 陣営はパーティではなくユニット側が持つため、生存メンバーから引く
            // 呼び出し元で生存 0 の場合は既に返しているので、ここでは必ず 1 体以上いる
            var report = new PPPartyAIThinkReport
            {
                Side = aSnap.AliveMembers[0].Side,
                TurnCount = aContext.TurnCount,
                Timestamp = Time.time,
                ResolvedRules = LastResolvedRuleName,
                DoctrineSummary = $"支出上限={aDoctrine.SpendCapRatio:0%} λ倍率={aDoctrine.LambdaMultiplier:0.##} 忍耐={aDoctrine.PatienceMultiplier:0.##}",
                BudgetSummary = aPlan.BuildSummary(),
                AdoptedCount = aAdoptedCount,
            };

            foreach (var candidate in aCandidates)
            {
                report.Candidates.Add(new PPPartyAIThinkCandidateEntry
                {
                    UnitName = candidate.Unit?.DisplayName ?? "-",
                    ActionName = candidate.DisplayName,
                    TargetName = candidate.Target?.DisplayName ?? "-",
                    Role = candidate.Role,
                    Utility = candidate.Utility,
                    CostTotal = candidate.Cost?.Total ?? 0f,
                    LambdaCost = aPlan.LambdaCostOf(candidate.Cost),
                    IsAdopted = candidate.RejectReason == PPActionRejectReason.None,
                    IsFallback = candidate.IsFallback,
                    RejectReason = candidate.RejectReason,
                });
            }

            PPPartyAIDebugHub.Report(report);
        }
    }
}
