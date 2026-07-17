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
    public enum PPSkillType
    {
        Attack,
        Support,
        Heal,
        Special,
    }
    
    [CreateAssetMenu(fileName = "PPBattleSkillDefinition", menuName = "Project-Pudding/Definition/PPSkillDefinition")]
    public class PPSkillDefinition : SkillDefinition
    {
        [Header("PPSkill")]
        [Label("スキルタイプ")]
        [SerializeField] protected PPSkillType mSkillType;
        [Label("消費リソース")]
        [SerializeField] protected float mRequiredResource = 10;
        
        public PPSkillType SkillType => mSkillType;
        public float RequiredResource => mRequiredResource;

        // 一旦ベースと同じ 拡張があれば追加する
        public virtual PPBattleSkill CreatePusherBattleSkill()
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