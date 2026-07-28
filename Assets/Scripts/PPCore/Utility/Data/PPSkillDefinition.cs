/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillDefinition.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief スキル定義
 * =====================================*/

using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // スキル発動時にエフェクトを誰に付与するか
    public enum PPEffectApplyTarget
    {
        [InspectorName("対象")]
        Target,
        [InspectorName("発動者")]
        Self,
    }
    
    // 付与するエフェクトと適用対象
    public struct PPSkillEffectEntry
    {
        [Label("エフェクト")]
        public PPEffectDefinition Effect;
        [Label("対象")]
        public PPEffectApplyTarget ApplyTarget;
    }
    
    public abstract class PPSkillDefinition : SkillDefinition
    {
        [Header("拡張")]
        [Label("スキルタイプ")]
        [SerializeField]protected PPBattleSkillRole mBattleSkillRole;
        [Label("種別")]
        [SerializeField]protected PPSkillCategory mCategory = PPSkillCategory.Physical;
        [Label("属性")]
        [SerializeField]protected PPTypeAttribute mAttribute = PPTypeAttribute.Normal;
        [Label("消費リソース")]
        [SerializeField] protected PPResourceAmount[] mCost;
        private PPResourceCost mCachedCost;
        
        [Header("エフェクト")]
        [Label("付与するエフェクト")]
        [SerializeField]protected PPSkillEffectEntry[] mEffectEntries;
        
        public float Power => mPower;
        public PPBattleSkillRole BattleSkillRole => mBattleSkillRole;
        public PPSkillCategory Category => mCategory;
        public PPTypeAttribute Attribute => mAttribute;
        public PPResourceCost Cost => mCachedCost ??= PPResourceCost.From(mCost);
        
        public override BattleSkill CreateRuntimeSkill()
        {
            var skill = new PPBattleSkill(mSkillId, mDisplayName, mTargetScope.CreateResolver(), BuildEffect());
            skill.SourceDefinition = this;
            skill.MaxCooldown = mMaxCooldown;
            skill.MaxUsesPerBattle = mMaxUsesPerBattle;
            skill.ResetForBattle();
            return skill;
        }

        private Action<BattleUnit, List<BattleUnit>, BattleContext> BuildEffectWithEntries()
        {
            var mainEffect = BuildEffect();
            return (src, targets, ctx) =>
            {
                mainEffect?.Invoke(src, targets, ctx);
                ApplyEffectEntries(src, targets, ctx);
            };
        }

        private void ApplyEffectEntries(BattleUnit aSource, List<BattleUnit> aTargets, BattleContext aContext)
        {
            if(mEffectEntries == null)
                return;

            foreach (var entry in mEffectEntries)
            {
                if(entry.Effect == null)
                    continue;

                if (entry.ApplyTarget == PPEffectApplyTarget.Self)
                {
                    aSource.AddStatusEffect(entry.Effect.CreateRuntimeStatusEffect(aSource, aSource, aContext));
                    continue;
                }

                foreach (var tgt in aTargets)
                {
                    tgt.AddStatusEffect(entry.Effect.CreateRuntimeStatusEffect(aSource, tgt, aContext));
                }
            }
        }
    }
}