/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPParameterEffect.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief PPCore固有のパラメータエフェクト
 * =====================================*/
using System;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    [Flags]
    public enum PPParameterEffectCategory : long
    {
        [InspectorName("なし")]
        None = 0,
        [InspectorName(PPParameterEffectCategoryDefinition.NameAttack + "バフ")]
        AttackBuff = 1L << 0,
        [InspectorName(PPParameterEffectCategoryDefinition.NameAttack + "デバフ")]
        AttackDebuff =  1L << 1,
        [InspectorName(PPParameterEffectCategoryDefinition.NameDefense + "バフ")]
        DefenseBuff = 1L << 2,
        [InspectorName(PPParameterEffectCategoryDefinition.NameDefense + "デバフ")]
        DefenseDebuff =  1L << 3,
        [InspectorName(PPParameterEffectCategoryDefinition.NameSpeed + "バフ")]
        SpeedBuff =  1L << 4,
        [InspectorName(PPParameterEffectCategoryDefinition.NameSpeed + "デバフ")]
        SpeedDebuff =  1L << 5,
        [InspectorName(PPParameterEffectCategoryDefinition.NameHp + "バフ")]
        HpBuff = 1L << 6,
        [InspectorName(PPParameterEffectCategoryDefinition.NameHp + "デバフ")]
        HpDebuff =  1L << 7,
        [InspectorName(PPParameterEffectCategoryDefinition.NameCost + "バフ")]
        CostBuff =  1L << 8,
        [InspectorName(PPParameterEffectCategoryDefinition.NameCost + "デバフ")]
        CostDebuff =  1L << 9,
    }

    public static class PPParameterEffectCategoryDefinition
    {
        public const string NameAttack = "攻撃";
        public const string NameDefense = "防御";
        public const string NameSpeed = "素早さ";
        public const string NameHp = "HP";
        public const string NameCost = "コスト";
    }
    
    public class PPParameterEffect : StatusEffect
    {
        public PPParameterEffectCategory Category { get; set; } = PPParameterEffectCategory.None;

        public PPParameterEffect(string aEffectId, string aDisplayName, IDurationCondition aDurationCondition = null)
            : base(aEffectId, aDisplayName, aDurationCondition)
        {
            
        }

        protected internal override void ApplyTo(BattleUnit aUnit)
        {
            if (aUnit is PPBattleUnit ppUnit)
            {
                for (int i = 0; i < Modifiers.Count; i++)
                {
                    var param = ppUnit.ResolveParameter(mModifierTargets[i]);
                    param?.AddModifier(Modifiers[i]);
                }
            }
            OnApply?.Invoke(aUnit);
        }

        protected internal override void RemoveFrom(BattleUnit aUnit)
        {
            aUnit.Parameters.RemoveModifiersFromSource(this);
            if (aUnit is PPBattleUnit ppUnit)
            {
                ppUnit.ExtraParameters.RemoveModifiesFromSource(this);                
            }
            OnRemove?.Invoke(aUnit);
        }
    }
}