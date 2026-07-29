/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file BattleResult.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief バトルのリザルト定義
 * =====================================*/

namespace CommandBattleCore
{
    /// <summary>
    /// バトルの決着の種類。<see cref="BattleResultType.InProgress"/> 以外が返った時点で
    /// <see cref="BattleManager"/> はバトルを終了させる。
    /// </summary>
    public enum BattleResultType
    {
        /// <summary>まだ決着していない。</summary>
        InProgress,
        /// <summary>勝利。敵が全滅した。</summary>
        Victory,
        /// <summary>敗北。味方が全滅した。</summary>
        Defeat,
        /// <summary>引き分け。両陣営が同時に全滅した。</summary>
        Draw,
        /// <summary>逃走による終了。</summary>
        Escaped,
        /// <summary>拡張用。独自の終了条件を作る場合に使う。</summary>
        Custom,
    }

    /// <summary>
    /// バトルの結果。
    /// 現状は種別のみを持つが、リザルト画面向けの情報を足す場合はここを拡張する。
    /// </summary>
    public class BattleResult
    {
        /// <summary>決着の種類。</summary>
        public BattleResultType Type { get; }

        /// <param name="type">決着の種類。</param>
        public BattleResult(BattleResultType type)
        {
            Type = type;
        }

        /// <summary>「継続中」を表す共有インスタンス。毎回の判定で新規生成しないためのもの。</summary>
        public static readonly BattleResult InProgress = new(BattleResultType.InProgress);
    }
}
