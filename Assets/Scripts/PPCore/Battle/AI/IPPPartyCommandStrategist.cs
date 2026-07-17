/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IPPPartyCommandStrategist.cs
 * @author hqrse
 * @date 2026/07/16
 * @brief パーティ全体を俯瞰して行動計画を立てる
 * =====================================*/
using CommandBattleCore;

namespace PPCore
{
    public interface IPPPartyCommandStrategist
    {
        PPPartyPlan PlanActions(BattleParty aSelf, BattleContext aContext);
    }
}