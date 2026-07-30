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
    /// <summary>
    /// リソースの増加ペースを記録するトラッカー。
    /// <para>
    /// AI が「今は撃たずに待てば、あと何ティックで目当てのスキルが撃てるか」を
    /// 見積もるために使う。プッシャー由来の収入は落ちるコインの量で常に変動するため、
    /// 直近のサンプルを平均して均した値を使う。
    /// </para>
    /// <para>
    /// 保持するのは残量そのものではなく前回からの増分。
    /// リソースを消費して残量が減った場合は増分 0 として扱い、
    /// 「消費した」ことが「収入が落ちた」と誤解されないようにしている。
    /// </para>
    /// </summary>
    public sealed class PPIncomTrendTracker
    {
        /// <summary>直近の増分の履歴。古いものから順に捨てられる。</summary>
        private readonly Queue<float> mRecentGains = new();
        /// <summary>前回サンプリング時のリソース量。初回は増分を計算できないため null。</summary>
        private float? mLastLevel;

        /// <summary>
        /// 現在のリソース量をサンプリングし、前回からの増分を履歴へ積む。
        /// 履歴が指定件数を超えた分は古い方から捨てる。
        /// </summary>
        /// <param name="aCurrentLevel">現在のリソース量。</param>
        /// <param name="aSampleCount">保持するサンプル数。最低 1 件は保持する。</param>
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

        /// <summary>
        /// 直近サンプルの平均増加量（1 ティックあたり）。
        /// サンプルが無ければ 0 を返し、待機判定側では「増加が見込めない」と扱われる。
        /// </summary>
        public float AverageRecentGainPerTick
            => mRecentGains.Count == 0 ? 0f : mRecentGains.Average();
    }
}
