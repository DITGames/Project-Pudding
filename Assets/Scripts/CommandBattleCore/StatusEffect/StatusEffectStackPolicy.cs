/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file StatusEffectStuckPolicy.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief ステータスエフェクトの重ね掛け挙動の定義
 * =====================================*/

namespace CommandBattleCore
{
    public enum StatusEffectStackPolicy
    {
        Stack,                  // 別で積む(複数同時処理が可能)
        Refresh,                // 継続時間のリフレッシュ
        StackCount,             // スタック数を加算する。スタック数によって効果を変えるときなどに便利
        StackCountAndRefresh,   // スタック数加算 + 継続時間リフレッシュ
        Ignore,                 // すでに付与されていた場合に新規付与をしない
        Replace,                // 既存付与を除去して置き換える
    }
}