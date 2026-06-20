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
    [Flags]
    public enum ActionRestriction
    {
        None = 0,
        CannotAct = 1 << 0,     // 麻痺、睡眠など  行動不可
        Confused = 1 << 1,      // 混乱、魅了など  行動ランダム化など
        Silenced = 1 << 2,      // 沈黙など       スキル使用不可など
        CannotEscape = 1 << 3,  // 逃走不可
        CannotSwap = 1 << 4,    // 入れ替え不可
    }
}