/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleParty.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief Pusherのバトルパーティベース
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;

namespace PPBattle
{
    public class PPBattleParty : BattleParty
    {
        public PPBattleResourcePool ResourcePool { get; }

        public PPBattleParty(int aMaxCoin, BattleSide aSide, IEnumerable<BattleUnit> aActiveMembers,
            IEnumerable<BattleUnit> aReserveMembers = null)
            : base(aSide, aActiveMembers, aReserveMembers)
        {
            ResourcePool = new PPBattleResourcePool(aMaxCoin);
        }
    }
}