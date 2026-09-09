/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPEffectCategory.cs
 * @author hqrse
 * @date 2026/07/31
 * @brief エフェクトのゲーム固有分類
 * =====================================*/

using System;

namespace PPCore
{
    // bit 0-15 : 状態異常系
    // bit 16-31: パラメータ変動系
    // Unity のシリアライズが long 裏付けの enum に対応していないため int を使う（[SerializeField] で編集可能にするため）
    [Flags]
    public enum PPEffectCategory
    {
        None = 0,

        /* ---- 状態異常系 ---- */
        Poison = 1 << 0,
        Burn = 1 << 1,
        Paralyze = 1 << 2,
        // 脆弱：被ダメージ増加
        Vulnerable = 1 << 3,
        // 混乱：行動のランダム化
        Confused = 1 << 4,
        // 封印：スキル使用不可
        Silenced = 1 << 5,

        /* ---- パラメータ変動系 ---- */
        AttackBuff = 1 << 16,
        AttackDebuff = 1 << 17,
        DefenseBuff = 1 << 18,
        DefenseDebuff = 1 << 19,
        SpeedBuff = 1 << 20,
        SpeedDebuff = 1 << 21,
        MaxHpBuff = 1 << 22,
        MaxHpDebuff = 1 << 23,
        CostBuff = 1 << 24,
        CostDebuff = 1 << 25,
        ActionCountBuff = 1 << 26,
        ActionCountDebuff = 1 << 27,

        /* ---- まとめ(解除スキルのマスクとしてそのまま使う) ---- */
        AllAilment = Poison | Burn | Paralyze | Vulnerable | Confused | Silenced,
        AllBuff = AttackBuff | DefenseBuff | SpeedBuff | MaxHpBuff | CostBuff | ActionCountBuff,
        AllDebuff = AttackDebuff | DefenseDebuff | SpeedDebuff | MaxHpDebuff | CostDebuff | ActionCountDebuff,
    }
}
