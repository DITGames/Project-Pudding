/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPResourceCost.cs
 * @author hqrse
 * @date 2026/07/18
 * @brief リソースコスト
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Linq;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    [Serializable]
    public struct PPResourceAmount
    {
        [Label("リソース種別")]
        public PPResourceType Type;
        [Label("必要量")]
        public float Amount;
    }

    public sealed class PPResourceCost
    {
        private readonly float[] mAmounts;
        public float Total { get; }
        public bool IsFree => Total == 0;

        private PPResourceCost(float[] aAmounts, float aTotal)
        {
            mAmounts = aAmounts;
            Total = aTotal;
        }
        
        public float Get(int aIndex) => mAmounts[aIndex];
        public float Get(PPResourceType aType) => mAmounts[(int)aType];

        public bool CanPay(PPResourceType aType, float aAmount)
        {
            return mAmounts[(int)aType] >= aAmount;
        }

        public IReadOnlyList<PPResourceType> RelevantTypes()
        {
            List<PPResourceType> res = new List<PPResourceType>();
            for (int i = 0; i < mAmounts.Length; i++)
            {
                if (mAmounts[i] > 0)
                {
                    res.Add((PPResourceType)i);
                }
            }
            return res;
        }

        // コスト作成
        public static PPResourceCost From(IEnumerable<PPResourceAmount> aEntries)
        {
            var arr = new float[PPResource.TypeCount];
            float total = 0;
            if (aEntries != null)
            {
                foreach (var e in aEntries)
                {
                    float v = Mathf.Max(0f, e.Amount);
                    arr[(int)e.Type] = v;
                }
            }
            return new PPResourceCost(arr, total);
        }
        
        // 単一属性コスト
        public static PPResourceCost Single(PPResourceType aType, float aAmount)
        => From(new[]{new PPResourceAmount{Type = aType, Amount = aAmount}});
        
        // ノーマルコスト
        public static PPResourceCost BaseCost(float aAmount)
        => Single(PPResourceType.Normal, aAmount);
        
        // フリー
        public static readonly PPResourceCost Free = new PPResourceCost(new float[PPResource.TypeCount], 0f);
    }
}