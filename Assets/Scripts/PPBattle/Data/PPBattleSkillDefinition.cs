/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleSkillDefinition.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief Pusherのスキルデータベース
 * =====================================*/
using CommandBattleCore;
using UnityEngine;

namespace PPBattle
{
    [CreateAssetMenu(fileName = "PPBattleSkillDefinition",
        menuName = "Project Pudding/PPBattleSkillDefinition")]
    public class PPBattleSkillDefinition : SkillDefinition
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