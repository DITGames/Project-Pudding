/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file 
 * @author hqrse
 * @date 2026/06/13
 * @brief 
 * =====================================*/
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CommandBattleCore
{
    public class SkillDefinition : ScriptableObject
    {
        [Header("スキル")]
        [Label("スキルID")]
        [SerializeField] protected string mSkillId;
        [Label("表示名")]
        [SerializeField] protected string mDisplayName;
        [TextArea]
        [Label("説明")]
        [SerializeField] protected string mDescription;

        [Header("詳細")]
        [Label("ターゲット選択")]
        [SerializeField] protected TargetScope mTargetScope = TargetScope.SingleEnemy;
        [Label("スキルパワー")]
        [SerializeField] protected float mPower = 10f;
        [Label("クールタイム")]
        [SerializeField] protected int mMaxCooldown = 0;
        [Label("最大使用回数")]
        [SerializeField] protected int mMaxUsesPerBattle = 0;

        public string SkillId => mSkillId;
        public string DisplayName => mDisplayName;
        public string Description => mDescription;
        public TargetScope TargetScope => mTargetScope;

        public virtual BattleSkill CreateRuntimeSkill()
        {
            var skill = new BattleSkill(mSkillId, mDisplayName, mTargetScope.CreateResolver(), BuildEffect());
            skill.SourceDefinition = this;
            skill.MaxCooldown = mMaxCooldown;
            skill.MaxUsesPerBattle = mMaxUsesPerBattle;
            skill.ResetForBattle();
            return skill;
        }

        // スキル実行時のエフェクト生成
        protected virtual Action<BattleUnit, List<BattleUnit>, BattleContext> BuildEffect()
        {
            return null;
        }
    }
}