/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ActionRestriction.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 行動制限の種類
 * =====================================*/
using System;

namespace CommandBattleCore
{
    /// <summary>
    /// ステータスエフェクトが課す行動制限の種類。
    /// <para>
    /// 1 体に複数のエフェクトが同時に掛かるため、ビットフラグとして合成できるようにしてある。
    /// <see cref="BattleUnit.CurrentRestrictions"/> が全エフェクト分を OR で合成した現在値を返す。
    /// </para>
    /// </summary>
    [Flags]
    public enum ActionRestriction
    {
        /// <summary>制限なし。</summary>
        None = 0,
        /// <summary>行動不可。麻痺、睡眠など。</summary>
        CannotAct = 1 << 0,
        /// <summary>行動のランダム化。混乱、魅了など。</summary>
        Confused = 1 << 1,
        /// <summary>スキル使用不可。沈黙など。</summary>
        Silenced = 1 << 2,
        /// <summary>逃走不可。</summary>
        CannotEscape = 1 << 3,
        /// <summary>メンバー入れ替え不可。</summary>
        CannotSwap = 1 << 4,
    }
}
