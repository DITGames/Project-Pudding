/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillDefinition.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief PPのスキル定義のベースクラス
 * =====================================*/
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    [CreateAssetMenu(fileName = "PPBattleSkillDefinition",
        menuName = "Project-Pudding/PPSkillDefinition")]
    public class PPSkillDefinition : SkillDefinition
    {
        [Header("Pusher")]
        [Label("消費コイン")]
        [SerializeField]
        protected int mRequiredCoin = 10;
        
        public int RequiredCoin => mRequiredCoin;

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