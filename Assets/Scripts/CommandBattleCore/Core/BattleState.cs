/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file BattleState.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 基本ステート
 * =====================================*/

namespace CommandBattleCore
{
    // バトル進行のステート。BattleStateMachine が保持し、BattleManager が遷移させる
    // 通常の流れは BattleStart → CommandInput → ActionExecution → ResultCheck → BattleEnd
    // コマンドを 1 件実行するたびに ActionExecution → ResultCheck → CommandInput を往復し、
    // 決着がついた時点で BattleEnd へ抜ける
    public enum BattleState
    {
        // 未初期化。バトル開始前の初期値
        None = 0,
        // バトル開始処理中
        BattleStart = 1,
        // コマンド入力待ち。プレイヤー入力および AI の思考を受け付ける
        CommandInput = 2,
        // コマンド実行中。演出待ちもこのステートに含まれる
        ActionExecution = 3,
        // 勝敗判定中
        ResultCheck = 4,
        // バトル終了。以降コマンドは実行されない
        BattleEnd = 5,
        // 拡張先で追加
    }
}
