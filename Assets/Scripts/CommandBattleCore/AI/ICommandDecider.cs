/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ICommandDecider.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief コマンド決定の責務を切り出すインターフェース
 * プレイヤーユニットも敵ユニットも同じ仕組みで扱える
 * =====================================*/

namespace CommandBattleCore
{
    /// <summary>
    /// ユニット単体のコマンド決定を担うインターフェース。
    /// <para>
    /// 「誰が決めるか」（プレイヤー入力か AI か）を <see cref="BattleUnit"/> から切り離すためのもの。
    /// バトル側は決め方を問わず、返ってきたコマンドをキューへ積むだけで済む。
    /// </para>
    /// </summary>
    public interface ICommandDecider
    {
        /// <summary>
        /// このユニットが取る行動を決める。
        /// </summary>
        /// <param name="aSelf">行動を決めるユニット。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        /// <returns>実行するコマンド。</returns>
        BattleCommandBase DecideCommand(BattleUnit aSelf, BattleContext aContext);
    }
}
