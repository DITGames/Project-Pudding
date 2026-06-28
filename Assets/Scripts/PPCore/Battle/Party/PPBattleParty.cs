/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleParty.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief PPバトルパーティのベースクラス
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;

namespace PPCore
{
    public class PPBattleParty : BattleParty
    {
        public PPBattleResourcePool ResourcePool { get; }
        
        public Parameter CoinConversionRate { get; }

        public PPBattleParty(int aMaxCoin, float aBaseCoinRate, BattleSide aSide, IEnumerable<BattleUnit> aActiveMembers,
            IEnumerable<BattleUnit> aReserveMembers = null)
            : base(aSide, aActiveMembers, aReserveMembers)
        {
            ResourcePool = new PPBattleResourcePool(aMaxCoin);
            CoinConversionRate = new Parameter(aBaseCoinRate);
        }
    }
}