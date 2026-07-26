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
using System.Runtime.ExceptionServices;
using CommandBattleCore;
using CustomConsole;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Accessibility;
using UnityEngine.Analytics;

namespace PPCore
{
    public class PPPartyAIStrategistBase : IPPPartyCommandStrategist
    {
        // ユニットの行動希望
        private sealed class PPPartyWish
        {
            public PPBattleUnit Unit;
            public List<PPActionCandidate> Candidates;  // 採点済みの全候補
            public PPActionCandidate BestCandidate;     // ベスト選択
            public float Score;
        }
        
        private readonly PPPartyAIProfileDefinition mProfile;
        private readonly PPIncomTrendTracker mTrend = new();
        
        public string LastResolvedRuleName {get; private set;} = "Default";

        public PPPartyAIStrategistBase(PPPartyAIProfileDefinition aProfile)
        {
            mProfile = aProfile != null
                ? aProfile
                : ScriptableObject.CreateInstance<PPPartyAIProfileDefinition>();
        }

        /* プランの作成 */
        public PPPartyPlan PlanActions(BattleParty aSelf, BattleContext aContext)
        {
            if (aSelf is not PPBattleParty party)
                return PPPartyPlan.Wait;

            // パーティ情報収集
            var snap = PPPartyAIContext.Capture(party, aContext);
            if (snap.AliveMembers.Count == 0 || snap.AliveEnemies.Count == 0)
                return PPPartyPlan.Wait;
            
            // リソース推移のサンプリング
            mTrend.Sample(snap.Current(PPResourceType.Normal), mProfile.TrendSampleCount);
            
            // シチュエーション判断
            var situation = ResolveSituationRule(snap);
            var focusTarget = ChooseAttackTarget(snap, aContext);

            // 各ユニットの行動願望の収集(温存を考慮しない)
            var fullPoolBudget = new PPResourceBudget(party.ResourcePool, 0f);
            var wishes = new List<PPPartyWish>();

            foreach (var unit in snap.AliveMembers)
            {
                // 行動できないならスルー
                if((unit.CurrentRestrictions & ActionRestriction.CannotAct) != 0)
                    continue;
                
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
                var best = candidates
                    .Where(c => fullPoolBudget.CanAfford(c.Cost))
                    .OrderByDescending(c => c.Score)
                    .FirstOrDefault();

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
                
                float currentWait = EvaluateWaitForUnit(w.Unit, snap, w.Candidates, budget, situation);
                if(w.Score <= currentWait)
                    continue;
                budget.TrySpend(w.BestCandidate.Cost);
                picks.Add(new PPPartyActionAssignment(w.Unit, w.BestCandidate.BuildCommand(aContext), RoleOrder(w.BestCandidate.Role)));
            }
            
            return picks.Count == 0 ? PPPartyPlan.Wait : new PPPartyPlan(picks);
        }

        /* 状況別にシチュエーションを解決する */
        protected PPAISituationScore ResolveSituationRule(PPPartyAIContext aSnap)
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
        
        /* 攻撃対象の抽選 */
        protected PPBattleUnit ChooseAttackTarget(PPPartyAIContext aSnap, BattleContext aContext)
        {
            if (aSnap.AliveEnemies.Count == 0)
                return null;
            if (Chance(mProfile.FocusFire, aContext))
                return aSnap.LowestHpEnemy;
            int idx = aContext.Rules.RandomProvider.NextInt(aSnap.AliveEnemies.Count);
            return aSnap.AliveEnemies[idx];
        }
        
        /* ユニット単体の行動候補の収集 */
        protected List<PPActionCandidate> GenerateCandidatesForUnit(PPBattleUnit aUnit, PPPartyAIContext aSnap, PPBattleUnit aFocusTarget, BattleContext aContext)
        {
            var list = new List<PPActionCandidate>();
            
            // 通常攻撃
            if (aFocusTarget != null)
            {
                float atkCost = aUnit.PPParameters.Get(PPParameterSet.ParameterIdAttackCost)?.CurrentValue ?? 0f;
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

        /* スキルのターゲット解決 */
        protected static BattleUnit ResolveSkillTarget(PPBattleActionRole aRole, PPPartyAIContext aSnap, BattleUnit aTarget)
            => aRole switch
            {
                PPBattleActionRole.Heal => aSnap.LowestHpRatioAlly,
                PPBattleActionRole.Attack => aTarget,
                _ => null,
            };

        /* スキルのロール取得 */
        protected static PPBattleActionRole RoleOf(PPSkillDefinition aDef)
            => aDef.BattleSkillRole switch
            {
                PPBattleSkillRole.Attack => PPBattleActionRole.Attack,
                PPBattleSkillRole.Heal => PPBattleActionRole.Heal,
                PPBattleSkillRole.Support => PPBattleActionRole.Support,
                _ => PPBattleActionRole.None,
            };

        /* スキルターゲット作成 */
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
        
        /* ユニットのロール解決 */
        protected static PPUnitRole ResolveUnitRole(PPBattleUnit aUnit) => aUnit.AssignedRole;
        
        /* シチュエーション別ロールのウェイト解決 */
        protected static float SituationWeightFor(PPUnitRole aRole, PPAISituationScore aSituation)
            => aRole switch
            {
                PPUnitRole.Attacker => aSituation.Attack,
                PPUnitRole.Supporter => aSituation.Support,
                PPUnitRole.Healer => aSituation.Heal,
                _ => (aSituation.Attack + aSituation.Support + aSituation.Heal) / 3f,   // 未割り当ては平均値を返却
            };

        /* アクションとユニットのロール別ウェイトを解決 */
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

        /* スコア評価 */
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

        /* スコアウェイト計算のベース */
        protected float ScoreWeighted(float aWeight, float aSituationMul, float aBaseScore, float aBias, float aFactor,
            bool aUseAggression, PPResourceCost aCost, float aAggressionMultiplier = 1f)
        {
            float raw = aBaseScore + aBias * aFactor;
            float aggr = aUseAggression ? mProfile.Aggression * aAggressionMultiplier : 1f;
            return aWeight * aSituationMul * raw * aggr * CostEfficiency(aCost);
        }

        /* 通常攻撃スコア計算 */
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

        /* 攻撃スキルスコア計算 */
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

        /* サポートスコア計算 */
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

        /* 回復スコアけさん */
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

        // 消費コストによるスコア減少率計算
        protected float CostEfficiency(PPResourceCost aCost)
        {
            if(aCost == null || aCost.IsFree) return 1f;
            var cs = mProfile.CostScore;
            return 1f / (1f + mProfile.CostSensitivity * (aCost.Total / cs.ReferenceCost));
        }

        /* 待機スコア計算 */
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
            float shortfall = upcoming.Cost.Get(PPResource.BaseIndex) - aBudget.Remaining(PPResourceType.Normal);
            float gainPerTick = mTrend.AverageRecentGainPerTick;
            float ticksNeeded = gainPerTick > 0f ? shortfall / gainPerTick : float.PositiveInfinity;
            
            // AIプロファイルの警戒度が高いほど短いTick数でしか待たない(溜められると判断しない)
            // パーティ種別の忍耐係数とシチュエーションによる補正を掛けて待つことに意味があるか判断する
            float allowedTicks = Mathf.Lerp(6f, 1f, mProfile.Caution) * aSnap.PatienceCoefficient * aSituation.PatienceMultiplier;
            
            return ticksNeeded > allowedTicks ? 0f : upcoming.Score;
        }
        
        /* ロールごとの実行順序判定 */
        protected int RoleOrder(PPBattleActionRole aRole)
        => aRole switch
        {
            PPBattleActionRole.Attack => mProfile.Order.Attack,
            PPBattleActionRole.Support => mProfile.Order.Support,
            PPBattleActionRole.Heal => mProfile.Order.Heal,
            _ => mProfile.Order.Default,
        };
        
        /* 0-1判定 */
        protected static bool Chance(float a01, BattleContext aContext)
        {
            a01 = Mathf.Clamp01(a01);
            return aContext.Rules.RandomProvider.NextInt(100) < Mathf.RoundToInt(a01 * 100f);
        }
    }
}