/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleResourcePool.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief バトルで使用されるリソース定義
 * =====================================*/
using CommandBattleCore;

namespace PPCore
{
    public class PPBattleResourcePool
    {
        // 行動用リソース
        private readonly ResourceParameter[] mResourcePools;

        public PPBattleResourcePool(int aMaxPerType)
        {
            mResourcePools = new ResourceParameter[PPResource.TypeCount];
            for (int i = 0; i < mResourcePools.Length; i++)
            {
                var p = new ResourceParameter(aMaxPerType);
                p.Damage(aMaxPerType);  // 初期値0に設定
                mResourcePools[i] = p;
            }
        }
        
        // リソース取得
        public ResourceParameter Pool(PPResourceType aType) => mResourcePools[(int)aType];
        // リソース現在値取得
        public float Current(PPResourceType aType) => mResourcePools[(int)aType].Current;
        // リソース最大値取得
        public float Max(PPResourceType aType) => mResourcePools[(int)aType].Max.CurrentValue;
        // リソース追加
        public void Add(PPResourceType aType, float aAmount) => mResourcePools[(int)aType].Recover(aAmount);

        // 消費可能かのチェック(事前チェックに使用)
        public bool CanPay(PPResourceCost aCost)
        {
            if(aCost == null || aCost.IsFree)
                return true;
            for (int i = 0; i < PPResource.TypeCount; i++)
            {
                float need = aCost.Get(i);
                if(need > 0f && mResourcePools[i].Current + 0.0001f < need)
                    return false;
            }
            return true;
        }

        // 実消費
        public bool TryPay(PPResourceCost aCost)
        {
            if(aCost == null || aCost.IsFree)
                return true;
            if(!CanPay(aCost))
                return false;
            for (int i = 0; i < PPResource.TypeCount; i++)
            {
                float need = aCost.Get(i);
                if (need > 0f)
                {
                    mResourcePools[i].Damage(need);
                }
            }
            return true;
        }
    }
}