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
    public struct HitInfo
    {
        public HitResult mResult;
        public CriticalInfo mCriticalInfo;
    }
    
    public enum HitResult
    {
        Hit,
        Miss,
    }

    public struct CriticalInfo
    {
        public bool IsCritical;
        public float CriticalMultiplier; 
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
            if (aContext.Rules.RandomProvider.NextFloat() > hitChance) return HitResult.Miss;
            else return HitResult.Hit;
        }
    }
    
    public interface ICriticalResolver
    {
        CriticalInfo Resolve(BattleUnit aSource, BattleUnit aTarget, DamageInfo aInfo, BattleContext aContext);
    }

    public class StandardCriticalResolver : ICriticalResolver
    {
        public CriticalInfo Resolve(BattleUnit aSource, BattleUnit aTarget, DamageInfo aInfo, BattleContext aContext)
        {
            CriticalInfo info = new CriticalInfo();
            info.IsCritical = false;
            info.CriticalMultiplier = 1.2f;
            float criticalChance = 0.1f;
            if (aContext.Rules.RandomProvider.NextFloat() < criticalChance) info.IsCritical = true;
            return info;
        }
    }
}