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
    public interface IBattlePresenter
    {
        // コマンド実行前演出 詠唱やカットインなどに使う
        ValueTask PlayPreExecute(BattleCommandBase aCmd, BattleContext aContext, CancellationToken aCt);
        // コマンド実行後演出 ヒット演出やポップアップなどに使う
        ValueTask PlayPostExecute(BattleCommandBase aCmd, BattleContext aContext, CancellationToken aCt);
    }
}