/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPEffectCategory.cs
 * @author hqrse
 * @date 2026/07/31
 * @brief エフェクトのゲーム固有分類
 * =====================================*/

using System;

namespace PPCore
{
    // bit 0-15 : 状態異常系
    // bit 16-47: パラメータ変動系
    // bit 48-  : 予備
    [Flags]
    public enum PPEffectCategory : long
    {
        None = 0,

        /* ---- 状態異常系 ---- */
        Poison = 1L << 0,
        Burn = 1L << 1,
        Paralyze = 1L << 2,

        /* ---- パラメータ変動系 ---- */
        AttackBuff = 1L << 16,
        AttackDebuff = 1L << 17,
        DefenseBuff = 1L << 18,
        DefenseDebuff = 1L << 19,
        SpeedBuff = 1L << 20,
        SpeedDebuff = 1L << 21,
        MaxHpBuff = 1L << 22,
        MaxHpDebuff = 1L << 23,
        CostBuff = 1L << 24,
        CostDebuff = 1L << 25,
        ActionCountBuff = 1L << 26,
        ActionCountDebuff = 1L << 27,

        /* ---- まとめ(解除スキルのマスクとしてそのまま使う) ---- */
        AllAilment = Poison | Burn | Paralyze,
        AllBuff = AttackBuff | DefenseBuff | SpeedBuff | MaxHpBuff | CostBuff | ActionCountBuff,
        AllDebuff = AttackDebuff | DefenseDebuff | SpeedDebuff | MaxHpDebuff | CostDebuff | ActionCountDebuff,
    }
}
