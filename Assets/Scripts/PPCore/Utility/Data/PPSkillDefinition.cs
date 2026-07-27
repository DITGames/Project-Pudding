/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillDefinition.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief スキル定義
 * =====================================*/
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
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
    }
}