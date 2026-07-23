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
using CustomConsole;
using UnityEngine;
using UnityEngine.Accessibility;
using UnityEngine.Analytics;

namespace PPCore
{
    public class PPPartyAIStrategistBase : IPPPartyCommandStrategist
    {
        private readonly PPPartyAIProfileDefinition mProfile;
        private PPResourceBudget mScoreBudget;
        
        public string LastResolvedRuleName {get; private set;} = "Default";

        public PPPartyAIStrategistBase(PPPartyAIProfileDefinition aProfile)
        {
            mProfile = aProfile != null
                ? aProfile
                : ScriptableObject.CreateInstance<PPPartyAIProfileDefinition>();
        }

        public PPPartyPlan PlanActions(BattleParty aSelf, BattleContext aContext)
        {
            if (aSelf is not PPBattleParty party)
                return PPPartyPlan.Wait;

            var snap = PPPartyAIContext.Capture(party, aContext);
            if (snap.AliveMembers.Count == 0 || snap.AliveEnemies.Count == 0)
                return PPPartyPlan.Wait;

            var budget = new PPResourceBudget(party.ResourcePool, mProfile.BaseReserve);
            mScoreBudget = budget;
            
            var situation = ResolveSituation(snap);

            // 候補の生成
            var candidates = GenerateCandidates(snap, aContext);
            if (candidates.Count == 0)
                return PPPartyPlan.Wait;

            // スコア集計
            foreach (var candidate in candidates)
            {
                candidate.Score = Evaluate(candidate, snap, situation);
            }

            float waitScore = EvaluateWait(snap, mScoreBudget);

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

        protected PPAISituationScore ResolveSituation(PPPartyAIContext aSnap)
        {
            var resolved = mProfile.DefaultScore;
            LastResolvedRuleName = "Default";
            foreach (var rule in mProfile.Rules)
            {
                if(rule == null || rule.Conditions == null || rule.Conditions.Count == 0)
                    continue;
                
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
                        Cost = PPResourceCost.BaseCost(atkCost),
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
                        Cost = def.Cost,
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
        protected float Evaluate(PPActionCandidate aCandidate, PPPartyAIContext aSnap, PPAISituationScore aScore)
            => aCandidate.Role switch
            {
                PPBattleRole.Attacker when aCandidate.Skill == null => ScoreBasicAttack(aCandidate, aSnap, aScore),
                PPBattleRole.Attacker => ScoreSkillAttack(aCandidate, aSnap, aScore),
                PPBattleRole.Supporter => ScoreSupport(aCandidate, aSnap, aScore),
                PPBattleRole.Healer => ScoreHeal(aCandidate, aSnap, aScore),
                _ => 0f
            };

        protected float ScoreWeighted(float aWeight, float aSituationMul, float aBaseScore, float aBias, float aFactor,
            bool aUseAggression, PPResourceCost aCost)
        {
            float raw = aBaseScore + aBias * aFactor;
            float aggr = aUseAggression ? mProfile.Aggression : 1f;
            return aWeight * aSituationMul * raw * aggr * CostEfficiency(aCost);
        }

        // 通常攻撃スコア評価
        protected float ScoreBasicAttack(PPActionCandidate aCandidate, PPPartyAIContext aSnap, PPAISituationScore aSituation)
        {
            var s = mProfile.AttackScore;
            float finishBias = 1f - PPPartyAIContext.HpRatio(aCandidate.Target);
            return ScoreWeighted(mProfile.Weights.Attack, aSituation.Attack, s.BaseScore, s.HpRatioBias, finishBias, true, aCandidate.Cost);
        }

        // スキルスコア評価
        protected float ScoreSkillAttack(PPActionCandidate aCandidate, PPPartyAIContext aSnap, PPAISituationScore aSituation)
        {
            var s = mProfile.SkillScore;

            float finishBias = aCandidate.Target != null
                ? 1f - PPPartyAIContext.HpRatio(aCandidate.Target)
                : s.RangeSkillScore;
            
            float score = ScoreWeighted(mProfile.Weights.Skill, aSituation.Skill, s.BaseScore, s.HpRatioBias, finishBias, true, aCandidate.Cost);

            if (!IsSkillResourceReady(aCandidate.Cost, aSnap))
            {
                score *= s.ResourceRatioBias;
            }
            return score;
        }

        protected bool IsSkillResourceReady(PPResourceCost aCost, PPPartyAIContext aSnap)
        {
            float mult = Mathf.Max(1f, mProfile.SkillThreshold);
            for (int i = 0; i < PPResource.TypeCount; i++)
            {
                float need = aCost.Get(i);
                if(need > 0f && aSnap.Current((PPResourceType)i) < need * mult)
                    return false;
            }
            return true;
        }

        // サポートスコア評価
        protected float ScoreSupport(PPActionCandidate aCandidate, PPPartyAIContext aSnap, PPAISituationScore aSituation)
        {
            var s = mProfile.SupportScore;
            float allies = Mathf.Clamp01(aSnap.AliveMembers.Count / s.MemberCountScore);
            return ScoreWeighted(mProfile.Weights.Support, aSituation.Support, s.BaseScore, s.MemberCountBias, allies, false, aCandidate.Cost);
        }

        // 回復スコア評価
        protected float ScoreHeal(PPActionCandidate aCandidate, PPPartyAIContext aSnap, PPAISituationScore aSituation)
        {
            var s = mProfile.HealScore;
            float severity = 1f - aSnap.LowestAllyHpRatio;
            if (severity < s.Threshold) return 0f;
            
            float urgency = severity * severity;
            return ScoreWeighted(mProfile.Weights.Heal, aSituation.Heal, 0f, s.HpRatioBias, urgency, false, aCandidate.Cost);
        }

        // コストによるスコア減少率
        protected float CostEfficiency(PPResourceCost aCost)
        {
            if(aCost == null || aCost.IsFree)
                return 1f;
            if(!mScoreBudget.CanAfford(aCost))
                return 0f;
            
            var cs = mProfile.CostScore;
            
            // コストの重い行動は評価を下げる
            float discount = Mathf.Clamp01(mProfile.CostSensitivity * (aCost.Total / cs.ReferenceCost));
            return Mathf.Max(cs.MinScore, 1f - discount);
        }

        // 溜めスコア
        protected float EvaluateWait(PPPartyAIContext aSnap, PPResourceBudget aBudget)
        {
            // パーティの傾向が温存型のほど評価を高くする
            float patience = mProfile.WaitBias * (1f - mProfile.Aggression);
            float mult = Mathf.Max(1f, mProfile.SkillThreshold);
            float saveUrge = 0f;
            foreach (var unit in aSnap.AliveMembers)
            {
                foreach (var skill in unit.Skills)
                {
                    if (skill.SourceDefinition is not PPSkillDefinition def)
                        continue;
                    // 攻撃以外は溜め対象に入れない
                    if (RoleOf(def) != PPBattleRole.Attacker)
                        continue;
                    var cost = def.Cost;
                    // すでに発動可能ならスルー
                    if(cost.IsFree || aBudget.CanAfford(cost))
                        continue;

                    float worst = 1f;
                    for (int i = 0; i < PPResource.TypeCount; i++)
                    {
                        float need = cost.Get(i) * mult;
                        if(need < 0f)
                            continue;
                        worst = Mathf.Min(worst, Mathf.Clamp01(aBudget.Remaining((PPResourceType)i) / need));
                    }
                    saveUrge = Mathf.Max(saveUrge, worst);
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