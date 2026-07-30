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
    // リソースの増加ペースを記録するトラッカー
    // AI が「今は撃たずに待てば、あと何ティックで目当てのスキルが撃てるか」を
    // 見積もるために使う。プッシャー由来の収入は落ちるコインの量で常に変動するため、
    // 直近のサンプルを平均して均した値を使う
    // 保持するのは残量そのものではなく前回からの増分
    // リソースを消費して残量が減った場合は増分 0 として扱い、
    // 「消費した」ことが「収入が落ちた」と誤解されないようにしている
    public sealed class PPIncomTrendTracker
    {
        // 直近の増分の履歴。古いものから順に捨てられる
        private readonly Queue<float> mRecentGains = new();
        // 前回サンプリング時のリソース量。初回は増分を計算できないため null
        private float? mLastLevel;

        // 現在のリソース量をサンプリングし、前回からの増分を履歴へ積む
        // 履歴が指定件数を超えた分は古い方から捨てる
        // aCurrentLevel : 現在のリソース量
        // aSampleCount : 保持するサンプル数。最低 1 件は保持する
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

        // 直近サンプルの平均増加量（1 ティックあたり）
        // サンプルが無ければ 0 を返し、待機判定側では「増加が見込めない」と扱われる
        public float AverageRecentGainPerTick
            => mRecentGains.Count == 0 ? 0f : mRecentGains.Average();
    }
}
