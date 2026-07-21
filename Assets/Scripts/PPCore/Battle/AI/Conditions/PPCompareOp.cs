/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPCompareOp.cs
 * @author hqrse
 * @date 2026/07/21
 * @brief 条件比較で共通利用する演算子とヘルパー
 * =====================================*/

using UnityEngine;

namespace PPCore
{
    public enum PPCompareOp
    {
        [InspectorName("等しい")]
        Equal,
        [InspectorName("等しくない")]
        NotEqual,
        [InspectorName("以上")]
        GreaterOrEqual,
        [InspectorName("以下")]
        LessOrEqual,
        [InspectorName("より大きい")]
        GreaterThan,
        [InspectorName("未満")]
        LessThan,
    }

    public static class PPConditionMath
    {
        public static bool Compare(float aValue, PPCompareOp aOp, float aThreshold, float aTolerance = 0f)
        => aOp switch
        {
            PPCompareOp.Equal => Mathf.Abs(aValue - aThreshold) < aTolerance,
            PPCompareOp.NotEqual => Mathf.Abs(aValue - aThreshold) >= aTolerance,
            PPCompareOp.GreaterOrEqual => aValue >= aThreshold,
            PPCompareOp.LessOrEqual => aValue <= aThreshold,
            PPCompareOp.GreaterThan => aValue > aThreshold,
            PPCompareOp.LessThan => aValue < aThreshold,
            _ => false
        };
        
        public static bool Compare(int aValue, PPCompareOp aOp, int aThreshold, int aTolerance = 0)
         => Compare((float)aValue, aOp, (float)aThreshold, (float)aTolerance);
    }
}