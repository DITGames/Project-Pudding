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
        private readonly float[] mRemaining;
        private readonly float[] mMax;

        public PPResourceBudget(PPBattleResourcePool aPool, float aBaseReserve = 0f)
        {
            mRemaining = new float[PPResource.TypeCount];
            mMax = new float[PPResource.TypeCount];
            for (int i = 0; i < PPResource.TypeCount; i++)
            {
                var t = (PPResourceType)i;
                float reserve = (i == PPResource.BaseIndex) ? Mathf.Max(0f, aBaseReserve) : 0f;
                mRemaining[i] = Mathf.Max(0f, aPool.Current(t) - reserve);
                mMax[i] = aPool.Max(t);
            }
        }
        
        public float Remaining(PPResourceType aType)
        => mRemaining[(int)aType];
        
        public float Fill(PPResourceType aType)
        => mMax[(int)aType] > 0f ? mRemaining[(int)aType] / mMax[(int)aType] : 0f;

        public bool CanAfford(PPResourceCost aCost)
        {
            if(aCost == null || aCost.IsFree)
                return true;
            for (int i = 0; i < PPResource.TypeCount; i++)
            {
                if (!aCost.CanPay((PPResourceType)i, mRemaining[i] + 0.0001f))
                {
                    return false;
                }
            }
            return true;
        }

        // 割り当てに成功した場合は仮想残量を減らす
        public bool TrySpend(PPResourceCost aCost)
        {
            if(aCost == null || aCost.IsFree)
                return true;
            if(!CanAfford(aCost))
                return false;
            for (int i = 0; i < PPResource.TypeCount; i++)
            {
                mRemaining[i] = Mathf.Max(0f, mRemaining[i] - aCost.Get(i));
            }
            return true;
        }
    }
}