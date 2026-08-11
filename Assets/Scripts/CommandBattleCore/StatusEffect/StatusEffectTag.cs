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
    // 基底型を int にしているのは Unity のシリアライザが 64bit 幅の enum を扱えないため
    // long にすると [SerializeField] に載せた時点でシリアライズ対象から外れる
    // タグが 32 個を超える場合は、int のまま別 enum へ分けるか保持方法を見直すこと
    [Flags]
    public enum StatusEffectTag : int
    {
        None = 0,
        // 有利な効果
        Buff = 1 << 0,
        // 不利な効果
        Debuff = 1 << 1,
        // 状態異常
        Ailment = 1 << 2,
        // パラメータ変動を伴う
        ParameterMod = 1 << 3,
        // 継続的な増減を伴う
        Periodic = 1 << 4,
        // 解除スキルで消せない
        Unremovable = 1 << 5,
    }
}
