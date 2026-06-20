/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file BattleState.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 基本ステート
 * =====================================*/

namespace CommandBattleCore
{
    public enum BattleState
    {
        None = 0,
        BattleStart = 1,
        CommandInput = 2,
        ActionExecution = 3,
        ResultCheck = 4,
        BattleEnd = 5,
        // 拡張先で追加
    }
}