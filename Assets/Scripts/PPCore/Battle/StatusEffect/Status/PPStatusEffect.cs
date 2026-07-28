/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPStatusEffect.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief PPCore固有のステータスエフェクト
 * =====================================*/
using System;
using CommandBattleCore;

namespace PPCore
{
    [Flags]
    public enum PPStatusEffectCategory : long
    {
        None = 0,
        Poison = 1L << 0,
        Burn = 1L << 1,
        Paralyze = 1L << 2,
    }
    
    public class PPStatusEffect : StatusEffect
    {
        public PPStatusEffectCategory Category { get; set; } = PPStatusEffectCategory.None;

        public PPStatusEffect(string aEffectId, string aDisplayName, IDurationCondition aDurationCondition = null)
            : base(aEffectId, aDisplayName, aDurationCondition)
        {
            
        }
    }
}