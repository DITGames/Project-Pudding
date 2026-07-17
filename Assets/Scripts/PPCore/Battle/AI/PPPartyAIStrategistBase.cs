/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyAIStrategistBase.cs
 * @author hqrse
 * @date 2026/07/16
 * @brief パーティ戦略構築のベースクラス
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using CommandBattleCore;
using UnityEngine;
using UnityEngine.Accessibility;
using UnityEngine.Analytics;

namespace PPCore
{
    public class PPPartyAIStrategistBase : IPPPartyCommandStrategist
    {
        private readonly PPPartyAIProfileDefinition mProfile;

        public PPPartyAIStrategistBase(PPPartyAIProfileDefinition aProfile)
        {
            mProfile = aProfile != null
                ? aProfile
                : ScriptableObject.CreateInstance<PPPartyAIProfileDefinition>();
        }

        public PPPartyPlan PlanActions(BattleParty aSelf, BattleContext aContext)
        {
            if (aSelf is not PPBattleParty party)
                return null;

            var snap = PPPartyAIContext.Capture(party, aContext);
            if (snap.AliveMembers.Count == 0 || snap.AliveEnemies.Count == 0)
                return PPPartyPlan.Wait;

            var budget = new PPResourceBudget(snap.CurrentResources, mProfile.ReserveResources);

            // 候補の生成
            var candidates = GenerateCandidates(snap, aContext);
            if (candidates.Count == 0)
                return PPPartyPlan.Wait;

            // スコア集計
            foreach (var candidate in candidates)
            {
                candidate.Score = Evaluate(candidate, snap);
            }

            float waitScore = EvaluateWait(snap);

            candidates.Sort((x, y) => y.Score.CompareTo(x.Score));

            var picks = new List<PPPartyActionAssignment>();
            var usedUnits = new HashSet<BattleUnit>();
            var usedRoles = new HashSet<PPBattleRole>();

            foreach (var candidate in candidates)
            {
                // 1行動あたりの最大数に達してるか
                if (picks.Count >= Mathf.Max(1, mProfile.MaxActionsPerTick))
                    break;
                // 溜めたほうがいいか
                if (candidate.Score <= waitScore)
                    break;
                // ユニットが行動済み?
                if (usedUnits.Contains(candidate.Unit))
                    continue;
                // ロールを分散する
                if (picks.Count > 0 && usedRoles.Contains(candidate.Role))
                    continue;
                // コストを超過してないか
                if (!budget.TrySpend(candidate.Cost))
                    continue;

                picks.Add(new PPPartyActionAssignment(candidate.Unit, candidate.BuildCommand(aContext),
                    RoleOrder(candidate.Role)));
                usedUnits.Add(candidate.Unit);
                usedRoles.Add(candidate.Role);
            }

            return picks.Count == 0 ? PPPartyPlan.Wait : new PPPartyPlan(picks);
        }

        // 行動の候補生成
        protected List<PPActionCandidate> GenerateCandidates(PPPartyAIContext aSnap, BattleContext aContext)
        {
            var list = new List<PPActionCandidate>();
            var attackTarget = ChooseAttackTarget(aSnap, aContext);

            foreach (var unit in aSnap.AliveMembers)
            {
                if ((unit.CurrentRestrictions & ActionRestriction.CannotAct) != 0) continue;

                // 通常攻撃
                if (attackTarget != null)
                {
                    float atkCost = unit.PPParameters.Get(PPParameterSet.ParameterIdAttackCost)?.CurrentValue ?? 0f;
                    var u = unit;
                    var tgt = attackTarget;
                    list.Add(new PPActionCandidate
                    {
                        Unit = u,
                        Role = PPBattleRole.Attacker,
                        Cost = atkCost,
                        Skill = null,
                        Target = tgt,
                        BuildCommand = _ => new PPAttackCommand(u, new SingleEnemyResolver(tgt)),
                    });
                }

                // スキル
                foreach (var skill in unit.Skills)
                {
                    if (!aContext.Rules.CastValidator.Validate(unit, skill, aContext).CanCast)
                        continue;
                    if (skill.SourceDefinition is not PPSkillDefinition def)
                        continue;

                    var role = RoleOf(def);
                    var target = ResolveSkillTarget(role, aSnap, attackTarget);
                    var u = unit;
                    var s = skill as PPBattleSkill;
                    var scope = def.TargetScope;
                    var chosen = target as PPBattleUnit;

                    list.Add(new PPActionCandidate
                    {
                        Unit = u,
                        Role = role,
                        Cost = def.RequiredResource,
                        Skill = s,
                        Target = chosen,
                        BuildCommand = _ => new PPSkillCommand(u, s, BuildSkillResolver(scope, chosen)),
                    });
                }
            }

            return list;
        }

        // 攻撃対象抽選
        protected PPBattleUnit ChooseAttackTarget(PPPartyAIContext aSnap, BattleContext aContext)
        {
            if (aSnap.AliveEnemies.Count == 0)
                return null;
            if (Chance(mProfile.FocusFire, aContext))
                return aSnap.LowestHpEnemy;
            int idx = aContext.Rules.RandomProvider.NextInt(aSnap.AliveEnemies.Count);
            return aSnap.AliveEnemies[idx];
        }

        // スキルのターゲット解決
        protected static BattleUnit ResolveSkillTarget(PPBattleRole aRole, PPPartyAIContext aSnap, BattleUnit aTarget)
            => aRole switch
            {
                PPBattleRole.Healer => aSnap.LowestHpRatioAlly,
                PPBattleRole.Attacker => aTarget,
                _ => null,
            };

        // スキルのロール取得
        protected static PPBattleRole RoleOf(PPSkillDefinition aDef)
            => aDef.SkillType switch
            {
                PPSkillType.Attack => PPBattleRole.Attacker,
                PPSkillType.Heal => PPBattleRole.Healer,
                PPSkillType.Support => PPBattleRole.Supporter,
                _ => PPBattleRole.None
            };

        // スキル対象作成
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

        // スコア評価
        protected float Evaluate(PPActionCandidate aCandidate, PPPartyAIContext aSnap)
            => aCandidate.Role switch
            {
                PPBattleRole.Attacker when aCandidate.Skill == null => ScoreBasicAttack(aCandidate, aSnap),
                PPBattleRole.Attacker => ScoreSkillAttack(aCandidate, aSnap),
                PPBattleRole.Supporter => ScoreSupport(aCandidate, aSnap),
                PPBattleRole.Healer => ScoreHeal(aCandidate, aSnap),
                _ => 0f
            };

        // 通常攻撃スコア評価
        protected float ScoreBasicAttack(PPActionCandidate aCandidate, PPPartyAIContext aSnap)
        {
            var attackScore = mProfile.AttackScore;

            // HPが削れている敵ほど高スコアとして扱う
            float finishBias = 1f - PPPartyAIContext.HpRatio(aCandidate.Target);
            float aggr = mProfile.Aggression;
            return mProfile.Weights.Attack * (attackScore.BaseScore + attackScore.HpRatioBias * finishBias) * aggr *
                   CostEfficiency(aCandidate.Cost);
        }

        // スキルスコア評価
        protected float ScoreSkillAttack(PPActionCandidate aCandidate, PPPartyAIContext aSnap)
        {
            var skillScore = mProfile.SkillScore;

            float finishBias = aCandidate.Target != null
                ? 1f - PPPartyAIContext.HpRatio(aCandidate.Target)
                : skillScore.RangeSkillScore;
            float aggr = mProfile.Aggression;
            float value = mProfile.Weights.Skill * (skillScore.BaseScore + skillScore.HpRatioBias * finishBias) * aggr;

            float threshold = aCandidate.Cost + Mathf.Max(1f, mProfile.SkillThreshold);
            if (aSnap.CurrentResources < threshold)
                value *= skillScore.ResourceRatioBias;
            return value * CostEfficiency(aCandidate.Cost);
        }

        // サポートスコア評価
        protected float ScoreSupport(PPActionCandidate aCandidate, PPPartyAIContext aSnap)
        {
            var supportScore = mProfile.SupportScore;
            float allies = Mathf.Clamp01(aSnap.AliveEnemies.Count / supportScore.MemberCountSocre);
            return mProfile.Weights.Support * (supportScore.BaseScore + supportScore.MemberCountBias + allies) *
                   CostEfficiency(aCandidate.Cost);
        }

        // 回復スコア評価
        protected float ScoreHeal(PPActionCandidate aCandidate, PPPartyAIContext aSnap)
        {
            var healScore = mProfile.HealScore;
            float severity = 1f - aSnap.LowestAllyHpRatio;
            if (severity < healScore.Threshold) return 0f;
            float urgency = severity * severity * healScore.HpRatioBias;
            return mProfile.Weights.Heal * urgency * CostEfficiency(aCandidate.Cost);
        }

        // コストによるスコア減少率
        protected float CostEfficiency(float aCost)
        {
            var costScore = mProfile.CostScore;
            return aCost <= 0f
                ? 1f
                : Mathf.Clamp(costScore.HighCostDecreaseRate / (costScore.HighCostDecreaseRate + aCost),
                    costScore.MinScore, 1f);
        }

        // 溜めスコア
        protected float EvaluateWait(PPPartyAIContext aSnap)
        {
            // パーティの傾向が温存型のほど評価を高くする
            float patience = mProfile.WaitBias * (1f - mProfile.Aggression);

            float saveUrge = 0f;
            foreach (var unit in aSnap.AliveMembers)
            {
                foreach (var skill in unit.Skills)
                {
                    if (skill.SourceDefinition is not PPSkillDefinition def)
                        continue;
                    if (RoleOf(def) != PPBattleRole.Attacker)
                        continue;

                    float threshold = def.RequiredResource * Mathf.Max(1f, mProfile.SkillThreshold);
                    if (threshold <= 0f || aSnap.CurrentResources >= threshold)
                        continue;

                    float fill = aSnap.CurrentResources / threshold;
                    saveUrge = Mathf.Max(saveUrge, fill);
                }
            }

            var waitScore = mProfile.WaitScore;
            return patience * (waitScore.BaseScore + waitScore.SaveBias * saveUrge);
        }
        
        // ロールごとの実行順序判定
        protected int RoleOrder(PPBattleRole aRole)
        => aRole switch
        {
            PPBattleRole.Attacker => mProfile.Order.Attack,
            PPBattleRole.Supporter => mProfile.Order.Support,
            PPBattleRole.Healer => mProfile.Order.Heal,
            _ => mProfile.Order.Default,
        };
        
        // 0-1判定判定
        protected static bool Chance(float a01, BattleContext aContext)
        {
            a01 = Mathf.Clamp01(a01);
            return aContext.Rules.RandomProvider.NextInt(100) < Mathf.RoundToInt(a01 * 100f);
        }
    }
}