/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IHitResolver.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 命中・クリティカルなどの拡張
 * =====================================*/
using UnityEngine;

namespace CommandBattleCore
{
    public enum HitResult
    {
        Hit,
        Miss,
        Critical,
    }
    
    public interface IHitResolver
    {
        HitResult Resolve(BattleUnit aSource, BattleUnit aTarget, DamageInfo aInfo, BattleContext aContext);
    }

    public class DefaultHitResolver : IHitResolver
    {
        public HitResult Resolve(BattleUnit aSource, BattleUnit aTarget, DamageInfo aInfo, BattleContext aContext)
            => HitResult.Hit;
    }

    public class StandardHitResolver : IHitResolver
    {
        public HitResult Resolve(BattleUnit aSource, BattleUnit aTarget, DamageInfo aInfo, BattleContext aContext)
        {
            float hitChance = 0.95f;
            if(aContext.Rules.RandomProvider.NextFloat() > hitChance) return HitResult.Miss;

            float critChance = 0.1f;
            return aContext.Rules.RandomProvider.NextFloat() < critChance ? HitResult.Critical : HitResult.Hit;
        }
    }
}