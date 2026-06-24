/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleResourcePool.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief バトルで使用されるリソース定義
 * =====================================*/
using CommandBattleCore;

namespace PPBattle
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
        public void AddCoinResource(int aCount)
        {
            CoinResource.Recover(aCount);
        }
        // 攻撃前にカウント数を満たしているかチェック
        public bool CheckConsumeCoinResource(int aCount)
        {
            return CoinResource.Current >= aCount;
        }
        // コイン消費の試行 消費に成功すれば攻撃可能
        public bool TryConsumeCoin(int aCount)
        {
            return CoinResource.TryConsume(aCount);
        }
    }
}