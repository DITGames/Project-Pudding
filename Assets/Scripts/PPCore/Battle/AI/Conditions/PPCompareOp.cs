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
    /// <summary>
    /// AI 条件アセットが共通で使う比較演算子。
    /// インスペクタ上で「HP割合が 30 以下なら」のような条件を組み立てるための選択肢。
    /// </summary>
    public enum PPCompareOp
    {
        /// <summary>等しい。</summary>
        [InspectorName("等しい")]
        Equal,
        /// <summary>等しくない。</summary>
        [InspectorName("等しくない")]
        NotEqual,
        /// <summary>以上。</summary>
        [InspectorName("以上")]
        GreaterOrEqual,
        /// <summary>以下。</summary>
        [InspectorName("以下")]
        LessOrEqual,
        /// <summary>より大きい。</summary>
        [InspectorName("より大きい")]
        GreaterThan,
        /// <summary>未満。</summary>
        [InspectorName("未満")]
        LessThan,
    }

    /// <summary>
    /// 比較演算子を実際の判定へ落とし込むヘルパー。
    /// 全ての AI 条件アセットがここを経由することで、判定の挙動を揃えている。
    /// </summary>
    public static class PPConditionMath
    {
        /// <summary>
        /// 実数を比較する。
        /// <para>
        /// 等値・非等値は浮動小数の誤差を吸収するため許容誤差込みで判定する。
        /// 許容誤差 0 のときは厳密な一致比較として振る舞う。
        /// </para>
        /// </summary>
        /// <param name="aValue">比較される値。</param>
        /// <param name="aOp">比較演算子。</param>
        /// <param name="aThreshold">閾値。</param>
        /// <param name="aTolerance">等値判定の許容誤差。既定は 0（厳密一致）。</param>
        /// <returns>条件を満たす場合 true。</returns>
        public static bool Compare(float aValue, PPCompareOp aOp, float aThreshold, float aTolerance = 0f)
        => aOp switch
        {
            PPCompareOp.Equal => Mathf.Abs(aValue - aThreshold) <= aTolerance,
            PPCompareOp.NotEqual => Mathf.Abs(aValue - aThreshold) > aTolerance,
            PPCompareOp.GreaterOrEqual => aValue >= aThreshold,
            PPCompareOp.LessOrEqual => aValue <= aThreshold,
            PPCompareOp.GreaterThan => aValue > aThreshold,
            PPCompareOp.LessThan => aValue < aThreshold,
            _ => false
        };

        /// <summary>
        /// 整数を比較する。実数版へ委譲するため判定の挙動は共通。
        /// 生存数やターン数のように厳密一致が要るケースで使うため、許容誤差は既定 0。
        /// </summary>
        /// <param name="aValue">比較される値。</param>
        /// <param name="aOp">比較演算子。</param>
        /// <param name="aThreshold">閾値。</param>
        /// <param name="aTolerance">等値判定の許容誤差。既定は 0（厳密一致）。</param>
        /// <returns>条件を満たす場合 true。</returns>
        public static bool Compare(int aValue, PPCompareOp aOp, int aThreshold, int aTolerance = 0)
         => Compare((float)aValue, aOp, (float)aThreshold, (float)aTolerance);
    }
}
