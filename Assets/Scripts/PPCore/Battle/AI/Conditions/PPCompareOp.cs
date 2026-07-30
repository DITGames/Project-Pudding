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
    // AI 条件アセットが共通で使う比較演算子
    // インスペクタ上で「HP割合が 30 以下なら」のような条件を組み立てるための選択肢
    public enum PPCompareOp
    {
        // 等しい
        [InspectorName("等しい")]
        Equal,
        // 等しくない
        [InspectorName("等しくない")]
        NotEqual,
        // 以上
        [InspectorName("以上")]
        GreaterOrEqual,
        // 以下
        [InspectorName("以下")]
        LessOrEqual,
        // より大きい
        [InspectorName("より大きい")]
        GreaterThan,
        // 未満
        [InspectorName("未満")]
        LessThan,
    }

    // 比較演算子を実際の判定へ落とし込むヘルパー
    // 全ての AI 条件アセットがここを経由することで、判定の挙動を揃えている
    public static class PPConditionMath
    {
        // 実数を比較する
        // 等値・非等値は浮動小数の誤差を吸収するため許容誤差込みで判定する
        // 許容誤差 0 のときは厳密な一致比較として振る舞う
        // aValue : 比較される値
        // aOp : 比較演算子
        // aThreshold : 閾値
        // aTolerance : 等値判定の許容誤差。既定は 0（厳密一致）
        // return : 条件を満たす場合 true
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

        // 整数を比較する。実数版へ委譲するため判定の挙動は共通
        // 生存数やターン数のように厳密一致が要るケースで使うため、許容誤差は既定 0
        // aValue : 比較される値
        // aOp : 比較演算子
        // aThreshold : 閾値
        // aTolerance : 等値判定の許容誤差。既定は 0（厳密一致）
        // return : 条件を満たす場合 true
        public static bool Compare(int aValue, PPCompareOp aOp, int aThreshold, int aTolerance = 0)
         => Compare((float)aValue, aOp, (float)aThreshold, (float)aTolerance);
    }
}
