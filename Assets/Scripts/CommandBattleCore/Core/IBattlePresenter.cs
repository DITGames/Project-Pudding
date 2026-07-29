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
    /// <summary>
    /// コマンド実行の前後に演出を差し込むためのインターフェース。
    /// <para>
    /// <see cref="BattleManager.ExecuteNextCommandAsync"/> が実行の前後で await するため、
    /// 演出が終わるまでバトル進行が待たされる。
    /// バトルロジック側が演出の中身を一切知らずに済むよう、待機だけを約束する形にしてある。
    /// </para>
    /// <para>
    /// キャンセルトークンは演出スキップに使われる。実装側は
    /// <see cref="System.OperationCanceledException"/> を投げて中断してよい
    /// （呼び出し側が飲み込んで進行を継続する）。
    /// </para>
    /// </summary>
    public interface IBattlePresenter
    {
        /// <summary>
        /// コマンド実行前演出。詠唱やカットインなどに使う。
        /// </summary>
        /// <param name="aCmd">これから実行されるコマンド。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        /// <param name="aCt">スキップ用のキャンセルトークン。</param>
        ValueTask PlayPreExecute(BattleCommandBase aCmd, BattleContext aContext, CancellationToken aCt);

        /// <summary>
        /// コマンド実行後演出。ヒット演出やダメージポップアップなどに使う。
        /// </summary>
        /// <param name="aCmd">実行し終えたコマンド。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        /// <param name="aCt">スキップ用のキャンセルトークン。</param>
        ValueTask PlayPostExecute(BattleCommandBase aCmd, BattleContext aContext, CancellationToken aCt);
    }
}
