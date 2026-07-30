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
    /// <summary>
    /// 状態異常の種別。
    /// 「毒と炎だけを解除する」のような複数種をまとめた指定ができるようビットフラグにしてある。
    /// 種類が増えても足りるよう long を基底型にしている。
    /// </summary>
    [Flags]
    public enum PPStatusEffectCategory : long
    {
        /// <summary>種別なし。</summary>
        None = 0,
        /// <summary>毒。</summary>
        Poison = 1L << 0,
        /// <summary>火傷。</summary>
        Burn = 1L << 1,
        /// <summary>麻痺。</summary>
        Paralyze = 1L << 2,
    }

    /// <summary>
    /// 種別を持つステータスエフェクト。
    /// 基底の <see cref="StatusEffect"/> に <see cref="Category"/> を足しただけで、
    /// これにより <see cref="PPEffectCureSkillDefinition"/> が種別を指定した一括解除を行える。
    /// </summary>
    public class PPStatusEffect : StatusEffect
    {
        /// <summary>この状態異常の種別。解除スキルの対象判定に使う。</summary>
        public PPStatusEffectCategory Category { get; set; } = PPStatusEffectCategory.None;

        /// <param name="aEffectId">エフェクトID。</param>
        /// <param name="aDisplayName">UI表示名。</param>
        /// <param name="aDurationCondition">持続条件。null なら永続。</param>
        public PPStatusEffect(string aEffectId, string aDisplayName, IDurationCondition aDurationCondition = null)
            : base(aEffectId, aDisplayName, aDurationCondition)
        {

        }
    }
}
