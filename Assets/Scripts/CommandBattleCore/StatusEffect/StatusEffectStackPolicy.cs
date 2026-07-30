/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file StatusEffectStuckPolicy.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief ステータスエフェクトの重ね掛け挙動の定義
 * =====================================*/

namespace CommandBattleCore
{
    // 同一 ID のステータスエフェクトを重ねて付与したときの挙動
    // BattleUnit.AddStatusEffect がこの値を見て分岐する
    public enum StatusEffectStackPolicy
    {
        // 別で積む(複数同時処理が可能)
        Stack,
        // 継続時間のリフレッシュ
        Refresh,
        // スタック数を加算する。スタック数によって効果を変えるときなどに便利
        StackCount,
        // スタック数加算 + 継続時間リフレッシュ
        StackCountAndRefresh,
        // すでに付与されていた場合に新規付与をしない
        Ignore,
        // 既存付与を除去して置き換える
        Replace,
    }
}
