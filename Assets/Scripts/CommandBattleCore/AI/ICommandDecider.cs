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
    // ユニット単体のコマンド決定を担うインターフェース
    // 「誰が決めるか」（プレイヤー入力か AI か）を BattleUnit から切り離すためのもの
    // バトル側は決め方を問わず、返ってきたコマンドをキューへ積むだけで済む
    public interface ICommandDecider
    {
        // このユニットが取る行動を決める
        // aSelf : 行動を決めるユニット
        // aContext : バトルコンテキスト
        // return : 実行するコマンド
        BattleCommandBase DecideCommand(BattleUnit aSelf, BattleContext aContext);
    }
}
