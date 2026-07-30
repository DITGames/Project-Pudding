/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IBattleResultChecker.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 勝敗チェッカー
 * =====================================*/

namespace CommandBattleCore
{
    // 勝敗を判定するチェッカー。BattleManager.ResultChecker に差し込む
    // 「ボスだけ倒せば勝ち」「規定ターン生存で勝ち」といった条件を差し替えられるようにしてある
    public interface IBattleResultChecker
    {
        // 現在の状況から決着を判定する。ユニット撃破時やターン経過時など、頻繁に呼ばれる
        // aContext : バトルコンテキスト
        // return : 判定結果。決着していなければ継続中を返す
        BattleResult CheckResult(BattleContext aContext);
    }

    // 標準の勝敗チェッカー。逃走フラグを最優先で見て、次に両陣営の全滅状況で判定する
    // 同時全滅は引き分けとして扱う
    public class DefaultBattleResultChecker : IBattleResultChecker
    {
        // 逃走 → 相打ち → 勝利 → 敗北の順に判定し、いずれにも当てはまらなければ継続中を返す
        // aContext : バトルコンテキスト
        // return : 判定結果
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
