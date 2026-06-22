/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PusherBattleParty.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief Pusherのバトルパーティベース
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;

namespace PusherBattle
{
    public class PusherBattleParty : BattleParty
    {
        public PusherBattleResourcePool ResourcePool { get; }

        public PusherBattleParty(int aMaxCoin, BattleSide aSide, IEnumerable<BattleUnit> aActiveMembers,
            IEnumerable<BattleUnit> aReserveMembers = null)
            : base(aSide, aActiveMembers, aReserveMembers)
        {
            ResourcePool = new PusherBattleResourcePool(aMaxCoin);
        }
    }
}