/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IBattleResultChecker.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 勝敗チェッカー
 * =====================================*/
using UnityEngine;

namespace CommandBattleCore
{
    public interface IBattleResultChecker
    {
        BattleResult CheckResult(BattleContext aContext);
    }

    public class DefaultBattleResultChecker : IBattleResultChecker
    {
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