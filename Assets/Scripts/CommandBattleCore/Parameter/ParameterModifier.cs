/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file ParameterModifier.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief パラメータ修飾子定義
 * =====================================*/

using UnityEngine;

namespace CommandBattleCore
{
    // パラメータ修飾子の適用方式
    // Parameter.RecalculateCurrentValue では Override が最優先され、無い場合に
    // 「(基礎値 + Add の合計) × max(0, 1 + Percent の合計) × Multiply の総乗」で現在値を求める
    // シリアライズ済みアセットは整数値で保持しているため、要素の追加は必ず末尾に行うこと
    public enum ParameterModifierType
    {
        // 基礎値に加算する。同種は合計される
        [InspectorName("加算")]
        Add,
        // 加算後の値に乗算する。同種は掛け合わされる
        [InspectorName("乗算")]
        Multiply,
        // 他を無視して値を上書きする。複数あれば優先度が最も高いものが勝つ
        [InspectorName("上書き")]
        Override,
        // 割合で増減させる（+20% は 0.2、-10% は -0.1）。同種は合計してから 1 を足した倍率として掛ける
        // 倍率は 0 未満にならないよう丸めるため、減少が 100% を超えても値が負になることはない
        [InspectorName("割合加算")]
        Percent,
    }

    // パラメータへ掛かる 1 件分の修飾子（バフ・デバフ・パッシブ補正など）
    // 生成後は変更されない不変オブジェクト。付与元を Source に持たせることで、
    // エフェクト解除時に Parameter.RemoveModifiersFromSource でまとめて剥がせるようにしている
    public sealed class ParameterModifier
    {
        // 適用方式（加算・乗算・上書き・割合加算）
        public ParameterModifierType Type { get; }
        // 修飾値。方式によって加算値・倍率・上書き値・割合（0.2 = +20%）のいずれかとして扱われる
        public float Value { get; }
        // この修飾子の付与元。解除時の照合に使うため参照比較できるものを渡す
        public object Source { get; }
        // Override 同士が競合したときの優先度。高い方が採用される
        public int Priority { get; }

        // aType : 適用方式
        // aSource : 付与元
        // aValue : 修飾値
        // aPriority : Override 競合時の優先度
        public ParameterModifier(ParameterModifierType aType, object aSource, float aValue, int aPriority = 0)
        {
            Type = aType;
            Source = aSource;
            Value = aValue;
            Priority = aPriority;
        }
    }
}
