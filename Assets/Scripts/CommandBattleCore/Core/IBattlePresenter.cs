/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IBattlePresenter.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief コマンド実行後の演出インターフェース
 * =====================================*/

using System.Threading;
using System.Threading.Tasks;

namespace CommandBattleCore
{
    // コマンド実行の前後に演出を差し込むためのインターフェース
    // BattleManager.ExecuteNextCommandAsync が実行の前後で await するため、
    // 演出が終わるまでバトル進行が待たされる
    // バトルロジック側が演出の中身を一切知らずに済むよう、待機だけを約束する形にしてある
    // キャンセルトークンは演出スキップに使われる。実装側は OperationCanceledException を
    // 投げて中断してよい（呼び出し側が飲み込んで進行を継続する）
    public interface IBattlePresenter
    {
        // コマンド実行前演出。詠唱やカットインなどに使う
        // aCmd : これから実行されるコマンド
        // aContext : バトルコンテキスト
        // aCt : スキップ用のキャンセルトークン
        ValueTask PlayPreExecute(BattleCommandBase aCmd, BattleContext aContext, CancellationToken aCt);

        // コマンド実行後演出。ヒット演出やダメージポップアップなどに使う
        // aCmd : 実行し終えたコマンド
        // aContext : バトルコンテキスト
        // aCt : スキップ用のキャンセルトークン
        ValueTask PlayPostExecute(BattleCommandBase aCmd, BattleContext aContext, CancellationToken aCt);
    }
}
