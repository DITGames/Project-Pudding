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
    [CreateAssetMenu(menuName = "CommandBattleCore/SkillDefinition", fileName = "NewSkill")]
    public class SkillDefinition : ScriptableObject
    {
        public enum SkillEffectType
        {
            Damage, Heal,
        }

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
        [Label("スキルタイプ")]
        [SerializeField] protected SkillEffectType mSkillEffectType = SkillEffectType.Damage;
        [Label("ダメージタグ")]
        [SerializeField] protected DamageTags mDamageTags = DamageTags.None;
        [Label("スキルパワー")]
        [SerializeField] protected float mPower = 10f;
        [Label("クールタイム")]
        [SerializeField] protected int mMaxCooldown = 0;
        [Label("最大使用回数")]
        [SerializeField] protected int mMaxUsesPerBattle = 0;
        [Label("クリティカル発生?")]
        [SerializeField] protected bool mIsOccurCritical = false;

        public string SkillId => mSkillId;
        public string DisplayName => mDisplayName;
        public string Description => mDescription;
        public TargetScope TargetScope => mTargetScope;
        public SkillEffectType SkillEffect => mSkillEffectType;

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
            return mSkillEffectType switch
            {
                SkillEffectType.Heal => (src, targets, ctx) =>
                {
                    foreach (var t in targets) t.ApplyHeal(mPower);
                },
                _ => (src, targets, ctx) =>
                {
                    foreach (var t in targets)
                    {
                        float dmg = Math.Max(1f, src.Parameters.Attack.CurrentValue + mPower
                                                 - t.Parameters.Defense.CurrentValue * 0.5f);
                        
                        var damageInfo = new DamageInfo(src,t, dmg, mDamageTags, this);
                        var hit = ctx.Rules.HitResolver.Resolve(src, t, damageInfo, ctx);

                        if (hit == HitResult.Miss)
                        {
                            damageInfo.IsMiss = true;
                            damageInfo.Amount = 0f;
                        }

                        if (hit == HitResult.Critical && mIsOccurCritical)
                        {
                            damageInfo.IsCritical = true;
                            damageInfo.Amount *= ctx.Rules.CriticalMultiplier;
                        }
                        
                        t.ApplyDamage(damageInfo);
                    }
                },
            };
        }
    }
}