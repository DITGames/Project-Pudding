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
    // 状態異常の種別
    // 「毒と炎だけを解除する」のような複数種をまとめた指定ができるようビットフラグにしてある
    // 種類が増えても足りるよう long を基底型にしている
    [Flags]
    public enum PPStatusEffectCategory : long
    {
        None = 0,
        Poison = 1L << 0,
        Burn = 1L << 1,
        Paralyze = 1L << 2,
    }

    // 種別を持つステータスエフェクト
    // 基底の StatusEffect に Category を足しただけで、
    // これにより PPEffectCureSkillDefinition が種別を指定した一括解除を行える
    public class PPStatusEffect : StatusEffect
    {
        // この状態異常の種別。解除スキルの対象判定に使う
        public PPStatusEffectCategory Category { get; set; } = PPStatusEffectCategory.None;

        // aEffectId : エフェクトID
        // aDisplayName : UI表示名
        // aDurationCondition : 持続条件。null なら永続
        public PPStatusEffect(string aEffectId, string aDisplayName, IDurationCondition aDurationCondition = null)
            : base(aEffectId, aDisplayName, aDurationCondition)
        {

        }
    }
}
