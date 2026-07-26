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
    [CreateAssetMenu(fileName = "PPBattleSkillDefinition", menuName = "Project-Pudding/Definition/PPSkillDefinition")]
    public class PPSkillDefinition : SkillDefinition
    {
        [Header("PPSkill")]
        [Label("スキルタイプ")]
        [SerializeField] protected PPBattleSkillRole mBattleSkillRole;
        [Label("消費リソース")]
        [SerializeField] protected PPResourceAmount[] mCost;
        private PPResourceCost mCachedCost;
        
        public PPBattleSkillRole BattleSkillRole => mBattleSkillRole;
        public PPResourceCost Cost => mCachedCost ??= PPResourceCost.From(mCost);

        // 一旦ベースと同じ 拡張があれば追加する
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