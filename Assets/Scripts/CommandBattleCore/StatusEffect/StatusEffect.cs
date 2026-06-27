/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file StatusEffect.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 状態異常定義
 * 防御状態や毒などステータスの異変はすべて定義をまとめる
 * =====================================*/
using System;
using System.Collections.Generic;

namespace CommandBattleCore
{
    public class StatusEffect
    {
        public string EffectId { get; }
        public string DisplayName { get; }
        public List<ParameterModifier> Modifiers { get; } = new();
        public StatusEffectStackPolicy StackPolicy { get; set; } = StatusEffectStackPolicy.Stack;
        public int MaxStacks { get; set; } = 1;
        public int CurrentStacks { get; protected internal set; } = 1;
        public IDurationCondition DurationCondition { get; set; }
        public ActionRestriction Restriction { get; set; } = ActionRestriction.None;
        
        // 被ダメージ前介入
        public Action<BattleUnit, DamageInfo> ModifyIncomingDamage { get; set; }
        // ステータスエフェクト更新
        public Action<BattleUnit, BattleContext> OnTick { get; set; }
        // ステータスエフェクトのスタック更新
        public Action<BattleUnit, StatusEffect> OnStackChanged { get; set; }
        // ステータスエフェクト適用
        public Action<BattleUnit> OnApply { get; set; }
        // ステータスエフェクト除去
        public Action<BattleUnit> OnRemove { get; set; }
        
        public StatusEffect(string aEffectId, string aDisplayName, IDurationCondition aDurationCondition = null)
        {
            EffectId = aEffectId;
            DisplayName = aDisplayName;
            DurationCondition = aDurationCondition ?? new PermanentDurationCondition();
        }

        public StatusEffect AddModifier(string aParamId, ParameterModifierType aModifierType, float aValue,
            int aPriority = 0)
        {
            Modifiers.Add(new ParameterModifier(aModifierType, this, aValue, aPriority));
            mModifierTargets.Add(aParamId);
            return this;
        }

        protected readonly List<string> mModifierTargets = new();

        protected internal virtual void ApplyTo(BattleUnit aUnit)
        {
            for (int i = 0; i < Modifiers.Count; i++)
            {
                aUnit.Parameters.Get(mModifierTargets[i])?.AddModifier(Modifiers[i]);
            }
            OnApply?.Invoke(aUnit);
        }

        protected internal virtual void RemoveFrom(BattleUnit aUnit)
        {
            aUnit.Parameters.RemoveModifiersFromSource(this);
            OnRemove?.Invoke(aUnit);
        }
    }
}