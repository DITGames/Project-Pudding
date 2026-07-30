/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file BattleResult.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief バトルのリザルト定義
 * =====================================*/

namespace CommandBattleCore
{
    // バトルの決着の種類。BattleResultType.InProgress 以外が返った時点で
    // BattleManager はバトルを終了させる
    public enum BattleResultType
    {
        // まだ決着していない
        InProgress,
        // 勝利。敵が全滅した
        Victory,
        // 敗北。味方が全滅した
        Defeat,
        // 引き分け。両陣営が同時に全滅した
        Draw,
        // 逃走による終了
        Escaped,
        // 拡張用。独自の終了条件を作る場合に使う
        Custom,
    }

    // バトルの結果
    // 現状は種別のみを持つが、リザルト画面向けの情報を足す場合はここを拡張する
    public class BattleResult
    {
        // 決着の種類
        public BattleResultType Type { get; }

        // type : 決着の種類
        public BattleResult(BattleResultType type)
        {
            Type = type;
        }

        // 「継続中」を表す共有インスタンス。毎回の判定で新規生成しないためのもの
        public static readonly BattleResult InProgress = new(BattleResultType.InProgress);
    }
}
