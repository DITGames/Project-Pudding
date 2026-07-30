/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IBattleResultChecker.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 勝敗チェッカー
 * =====================================*/

namespace CommandBattleCore
{
    /// <summary>
    /// 勝敗を判定するチェッカー。<see cref="BattleManager.ResultChecker"/> に差し込む。
    /// 「ボスだけ倒せば勝ち」「規定ターン生存で勝ち」といった条件を差し替えられるようにしてある。
    /// </summary>
    public interface IBattleResultChecker
    {
        /// <summary>
        /// 現在の状況から決着を判定する。ユニット撃破時やターン経過時など、頻繁に呼ばれる。
        /// </summary>
        /// <param name="aContext">バトルコンテキスト。</param>
        /// <returns>判定結果。決着していなければ継続中を返す。</returns>
        BattleResult CheckResult(BattleContext aContext);
    }

    /// <summary>
    /// 標準の勝敗チェッカー。逃走フラグを最優先で見て、次に両陣営の全滅状況で判定する。
    /// 同時全滅は引き分けとして扱う。
    /// </summary>
    public class DefaultBattleResultChecker : IBattleResultChecker
    {
        /// <summary>
        /// 逃走 → 相打ち → 勝利 → 敗北の順に判定し、いずれにも当てはまらなければ継続中を返す。
        /// </summary>
        /// <param name="aContext">バトルコンテキスト。</param>
        /// <returns>判定結果。</returns>
        public virtual BattleResult CheckResult(BattleContext aContext)
        {
            if (aContext.EscapeRequested)
                return new BattleResult(BattleResultType.Escaped);

            bool allyWiped = aContext.AllyParty.IsWiped();
            bool enemyWiped = aContext.EnemyParty.IsWiped();

            if (allyWiped && enemyWiped) return new BattleResult(BattleResultType.Draw);
            if (enemyWiped) return new BattleResult(BattleResultType.Victory);
            if (allyWiped) return new BattleResult(BattleResultType.Defeat);

            return BattleResult.InProgress;
        }
    }
}
