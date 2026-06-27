/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleResourcePool.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief PPバトルで使用されるリソース定義
 * =====================================*/
using CommandBattleCore;

namespace PPCore
{
    public class PPBattleResourcePool
    {
        // コイン
        public ResourceParameter CoinResource;

        public PPBattleResourcePool(int aMax)
        {
            CoinResource = new ResourceParameter(aMax);
            CoinResource.Damage(aMax);  // 0にリセット
        }

        // コイン獲得時に呼ぶ、またはイベントでコールバック
        public void AddCoinResource(float aAmount)
        {
            CoinResource.Recover(aAmount);
        }
        // 攻撃前にカウント数を満たしているかチェック
        public bool CanConsumeAttackResource(float aCount)
        {
            return CoinResource.Current >= aCount;
        }
        // コイン消費の試行 消費に成功すれば攻撃可能
        public bool TryConsumeAttackResource(float aCount)
        {
            return CoinResource.TryConsume(aCount);
        }
    }
}