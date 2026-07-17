/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPResourceBudget.cs
 * @author hqrse
 * @date 2026/07/16
 * @brief 共有プールを運用管理するためのクラス
 * =====================================*/
using UnityEngine;

namespace PPCore
{
    public sealed class PPResourceBudget
    {
        private float mRemaining;       // 計画内で使える残リソース量
        public float Remaining => mRemaining;
        public float Reserve { get; }   // 温存しておきたいリソース量

        public PPResourceBudget(float aAvailable, float aReserve)
        {
            Reserve = aReserve;
            mRemaining = aAvailable;
        }
        
        public bool CanAfford(float aCost) => aCost <= mRemaining;

        // 割り当てに成功した場合は仮想残量を減らす
        public bool TrySpend(float aCost)
        {
            if(!CanAfford(aCost))
                return false;
            
            mRemaining -= aCost;
            return true;
        }
    }
}