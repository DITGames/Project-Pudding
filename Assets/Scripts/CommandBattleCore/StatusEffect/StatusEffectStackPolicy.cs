/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file StatusEffectStuckPolicy.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief ステータスエフェクトの重ね掛け挙動の定義
 * =====================================*/

namespace CommandBattleCore
{
    /// <summary>
    /// 同一 ID のステータスエフェクトを重ねて付与したときの挙動。
    /// <see cref="BattleUnit.AddStatusEffect"/> がこの値を見て分岐する。
    /// </summary>
    public enum StatusEffectStackPolicy
    {
        /// <summary>別で積む(複数同時処理が可能)</summary>
        Stack,
        /// <summary>継続時間のリフレッシュ</summary>
        Refresh,
        /// <summary>スタック数を加算する。スタック数によって効果を変えるときなどに便利</summary>
        StackCount,
        /// <summary>スタック数加算 + 継続時間リフレッシュ</summary>
        StackCountAndRefresh,
        /// <summary>すでに付与されていた場合に新規付与をしない</summary>
        Ignore,
        /// <summary>既存付与を除去して置き換える</summary>
        Replace,
    }
}
