/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ICommandDecider.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief コマンド決定の責務を切り出すインターフェース
 * プレイヤーユニットも敵ユニットも同じ仕組みで扱える
 * =====================================*/
using UnityEngine;

namespace CommandBattleCore
{
    public interface ICommandDecider
    {
        BattleCommandBase DecideCommand(BattleUnit aSelf, BattleContext aContext);
    }
}