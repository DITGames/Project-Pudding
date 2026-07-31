/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file StatusEffectTag.cs
 * @author hqrse
 * @date 2026/07/31
 * @brief ゲームに依存しないステータスエフェクトの汎用分類
 * =====================================*/

using System;

namespace CommandBattleCore
{
    // 「毒」「火傷」といった具体名はゲーム側(Category)が持つ
    // ここにはどのゲームでも意味が変わらない分類だけを置く
    [Flags]
    public enum StatusEffectTag : long
    {
        None = 0,
        // 有利な効果
        Buff = 1L << 0,
        // 不利な効果
        Debuff = 1L << 1,
        // 状態異常
        Ailment = 1L << 2,
        // パラメータ変動を伴う
        ParameterMod = 1L << 3,
        // 継続的な増減を伴う
        Periodic = 1L << 4,
        // 解除スキルで消せない
        Unremovable = 1L << 5,
    }
}
