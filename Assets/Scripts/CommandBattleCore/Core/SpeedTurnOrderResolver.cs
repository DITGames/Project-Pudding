/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file SpeedTurnOrderResolver.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 行動準制御の標準実装
 * =====================================*/
using System;
using System.Collections.Generic;
using System.Linq;

namespace CommandBattleCore
{
    public class SpeedTurnOrderResolver : ITurnOrderResolver
    {
        public float SpeedJitter { get; set; } = 0f;

        private readonly Random mRng;
        
        public SpeedTurnOrderResolver(Random aRng = null) => mRng = aRng ?? new Random();

        public List<BattleUnit> ResolveOrder(BattleContext aContext)
        {
            var all = aContext.AllyParty.GetAliveActiveMembers()
                .Concat(aContext.EnemyParty.GetAliveActiveMembers());

            return all
                .OrderByDescending(u => u.Parameters.Speed.CurrentValue + (SpeedJitter > 0f
                    ? aContext.Rules.RandomProvider.NextFloat() * SpeedJitter : 0f)).ToList();
        }
    }
}