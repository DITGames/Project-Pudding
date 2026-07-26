/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IRandomProvider.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief バトル中の全乱数の供給コア
 * =====================================*/
using UnityEngine;

namespace CommandBattleCore
{
    public interface IRandomProvider
    {
        int NextInt(int aMaxExclusive);
        int NextInt(int aMinInclusive, int aMaxExclusive);
        float NextFloat();
        float NextFloat(float aMinInclusive, float aMaxExclusive);
        bool NextBool(float aTrueChance);
    }

    public class DefaultRandomProvider : IRandomProvider
    {
        protected readonly System.Random mRng;
        
        public DefaultRandomProvider(int? aSeed = null)
            => mRng = aSeed.HasValue ? new System.Random(aSeed.Value) : new System.Random();
        
        public int NextInt(int aMaxExclusive) => mRng.Next(aMaxExclusive);
        public int NextInt(int aMinInclusive, int aMaxExclusive)  => mRng.Next(aMinInclusive, aMaxExclusive);
        public float NextFloat() => (float)mRng.NextDouble();
        public float NextFloat(float aMinInclusive, float aMaxExclusive) => aMinInclusive + (float)mRng.NextDouble() * (aMaxExclusive - aMinInclusive);
        public bool NextBool(float aTrueChance) => mRng.NextDouble() < aTrueChance;
    }
}