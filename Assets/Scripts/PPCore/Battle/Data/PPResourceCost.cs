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
        public PPTypeAttribute Type;
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
        public float Get(PPTypeAttribute a) => mAmounts[(int)a];

        public bool CanPay(PPTypeAttribute a, float aAmount)
        {
            return mAmounts[(int)a] >= aAmount;
        }

        public IReadOnlyList<PPTypeAttribute> RelevantTypes()
        {
            List<PPTypeAttribute> res = new List<PPTypeAttribute>();
            for (int i = 0; i < mAmounts.Length; i++)
            {
                if (mAmounts[i] > 0)
                {
                    res.Add((PPTypeAttribute)i);
                }
            }
            return res;
        }

        // コスト作成
        public static PPResourceCost From(IEnumerable<PPResourceAmount> aEntries)
        {
            var arr = new float[PPTypeAttributeDefinition.TypeCount];
            float total = 0;
            if (aEntries != null)
            {
                foreach (var e in aEntries)
                {
                    float v = Mathf.Max(0f, e.Amount);
                    arr[(int)e.Type] += v;
                    total += v;
                }
            }
            return new PPResourceCost(arr, total);
        }
        
        // 単一属性コスト
        public static PPResourceCost Single(PPTypeAttribute a, float aAmount)
        => From(new[]{new PPResourceAmount{Type = a, Amount = aAmount}});
        
        // ノーマルコスト
        public static PPResourceCost BaseCost(float aAmount)
        => Single(PPTypeAttribute.Normal, aAmount);
        
        // フリー
        public static readonly PPResourceCost Free = new PPResourceCost(new float[PPTypeAttributeDefinition.TypeCount], 0f);
    }
}