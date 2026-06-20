/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file BattleRules.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief バトルルール
 * =====================================*/

using System.Collections.Generic;
using UnityEngine;

namespace CommandBattleCore
{
    public class BattleRules
    {
        public IHitResolver HitResolver { get; set; } = new StandardHitResolver();
        public float CriticalMultiplier { get; set; } = 1.5f;
        public IRandomProvider RandomProvider { get; set; } = new DefaultRandomProvider();
        public ICastValidator CastValidator { get; set; } = new DefaultCastValidator();
        public List<ITargetFilter> TargetFilters { get; } = new();
        public IDeadTargetPolicy DeadTargetPolicy { get; set; } = new FirstAliveFallback();
    }
}