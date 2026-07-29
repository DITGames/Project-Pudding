/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file BattleState.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 基本ステート
 * =====================================*/

namespace CommandBattleCore
{
    /// <summary>
    /// バトル進行のステート。<see cref="BattleStateMachine"/> が保持し、<see cref="BattleManager"/> が遷移させる。
    /// <para>
    /// 通常の流れは BattleStart → CommandInput → ActionExecution → ResultCheck → BattleEnd。
    /// コマンドを 1 件実行するたびに ActionExecution → ResultCheck → CommandInput を往復し、
    /// 決着がついた時点で BattleEnd へ抜ける。
    /// </para>
    /// </summary>
    public enum BattleState
    {
        /// <summary>未初期化。バトル開始前の初期値。</summary>
        None = 0,
        /// <summary>バトル開始処理中。</summary>
        BattleStart = 1,
        /// <summary>コマンド入力待ち。プレイヤー入力および AI の思考を受け付ける。</summary>
        CommandInput = 2,
        /// <summary>コマンド実行中。演出待ちもこのステートに含まれる。</summary>
        ActionExecution = 3,
        /// <summary>勝敗判定中。</summary>
        ResultCheck = 4,
        /// <summary>バトル終了。以降コマンドは実行されない。</summary>
        BattleEnd = 5,
        // 拡張先で追加
    }
}
