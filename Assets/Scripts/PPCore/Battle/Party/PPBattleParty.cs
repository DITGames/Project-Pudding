/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleParty.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief バトルパーティのベースクラス
 * =====================================*/
using System.Collections.Generic;
using CommandBattleCore;

namespace PPCore
{
    public class PPBattleParty : BattleParty
    {
        public PPBattleResourcePool ResourcePool { get; }
        
        public PPItemInventory Inventory { get; }
        
        public Parameter CoinConversionRate { get; }
        
        public IPPPartyCommandStrategist Strategist { get; set; }

        public PPBattleParty(int aMaxCoin, float aBaseCoinRate, BattleSide aSide, IEnumerable<BattleUnit> aActiveMembers,
            IEnumerable<BattleUnit> aReserveMembers = null, IReadOnlyDictionary<PPItemDefinition, int> aItems = null)
            : base(aSide, aActiveMembers, aReserveMembers)
        {
            ResourcePool = new PPBattleResourcePool(aMaxCoin);
            CoinConversionRate = new Parameter(aBaseCoinRate);
            Inventory = new PPItemInventory(aItems);
        }
    }
}