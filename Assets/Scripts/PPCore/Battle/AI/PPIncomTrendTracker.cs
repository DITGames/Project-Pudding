/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPIncomTrendTracker.cs
 * @author hqrse
 * @date 2026/07/23
 * @brief リソース推移のトラッカー
 * =====================================*/
using System;
using System.Collections.Generic;
using System.Linq;

namespace PPCore
{
    public sealed class PPIncomTrendTracker
    {
        private readonly Queue<float> mRecentGains = new();
        private float? mLastLevel;

        public void Sample(float aCurrentLevel, int aSampleCount)
        {
            if (mLastLevel.HasValue)
            {
                mRecentGains.Enqueue(Math.Max(0f, aCurrentLevel - mLastLevel.Value));
            }
            mLastLevel = aCurrentLevel;

            while (mRecentGains.Count > Math.Max(1, aSampleCount))
            {
                mRecentGains.Dequeue();
            }
        }
        
        public float AverageRecentGainPerTick
            => mRecentGains.Count == 0 ? 0f : mRecentGains.Average();
    }
}