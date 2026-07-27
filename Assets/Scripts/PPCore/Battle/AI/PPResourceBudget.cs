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
            mRemaining = new float[PPTypeAttributeDefinition.TypeCount];
            mMax = new float[PPTypeAttributeDefinition.TypeCount];
            for (int i = 0; i < PPTypeAttributeDefinition.TypeCount; i++)
            {
                var t = (PPTypeAttribute)i;
                float reserve = (i == PPTypeAttributeDefinition.BaseIndex) ? Mathf.Max(0f, aBaseReserve) : 0f;
                mRemaining[i] = Mathf.Max(0f, aPool.Current(t) - reserve);
                mMax[i] = aPool.Max(t);
            }
        }
        
        public float Remaining(PPTypeAttribute a)
        => mRemaining[(int)a];
        
        public float Fill(PPTypeAttribute a)
        => mMax[(int)a] > 0f ? mRemaining[(int)a] / mMax[(int)a] : 0f;

        public bool CanAfford(PPResourceCost aCost)
        {
            if(aCost == null || aCost.IsFree)
                return true;
            for (int i = 0; i < PPTypeAttributeDefinition.TypeCount; i++)
            {
                if (!aCost.CanPay((PPTypeAttribute)i, mRemaining[i] + 0.0001f))
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
            for (int i = 0; i < PPTypeAttributeDefinition.TypeCount; i++)
            {
                mRemaining[i] = Mathf.Max(0f, mRemaining[i] - aCost.Get(i));
            }
            return true;
        }
    }
}