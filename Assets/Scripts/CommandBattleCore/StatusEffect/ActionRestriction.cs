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
    // ステータスエフェクトが課す行動制限の種類
    // 1 体に複数のエフェクトが同時に掛かるため、ビットフラグとして合成できるようにしてある
    // BattleUnit.CurrentRestrictions が全エフェクト分を OR で合成した現在値を返す
    [Flags]
    public enum ActionRestriction
    {
        // 制限なし
        None = 0,
        // 行動不可。麻痺、睡眠など
        CannotAct = 1 << 0,
        // 行動のランダム化。混乱、魅了など
        Confused = 1 << 1,
        // スキル使用不可。沈黙など
        Silenced = 1 << 2,
        // 逃走不可
        CannotEscape = 1 << 3,
        // メンバー入れ替え不可
        CannotSwap = 1 << 4,
    }
}
