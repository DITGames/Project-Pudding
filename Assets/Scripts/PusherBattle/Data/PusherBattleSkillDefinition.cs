/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PusherBattleSkillDefinition.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief Pusherのスキルデータベース
 * =====================================*/
using CommandBattleCore;
using UnityEditor;
using UnityEngine;

namespace PusherBattle
{
    [CreateAssetMenu(fileName = "PusherBattleSkillDefinition",
        menuName = "Scriptable Objects/PusherBattleSkillDefinition")]
    public class PusherBattleSkillDefinition : SkillDefinition
    {
        [Header("Pusher")]
        [Label("消費コイン")]
        [SerializeField]
        protected int mRequiredCoin = 10;
        
        public int RequiredCoin => mRequiredCoin;

        // 一旦ベースと同じ 拡張があれば追加する
        public virtual PusherBattleSkill CreatePusherBattleSkill()
        {
            var skill = new PusherBattleSkill(mSkillId, mDisplayName, mTargetScope.CreateResolver(), BuildEffect());
            skill.SourceDefinition = this;
            skill.MaxCooldown = mMaxCooldown;
            skill.MaxUsesPerBattle = mMaxUsesPerBattle;
            skill.ResetForBattle();
            return skill;
        }
    }
}