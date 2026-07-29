/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ParameterModifier.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief パラメータ修飾子定義
 * =====================================*/

namespace CommandBattleCore
{
    /// <summary>
    /// パラメータ修飾子の適用方式。
    /// <see cref="Parameter.RecalculateCurrentValue"/> では Override が最優先され、
    /// 無い場合に「(基礎値 + Add の合計) × Multiply の総乗」で現在値を求める。
    /// </summary>
    public enum ParameterModifierType
    {
        /// <summary>基礎値に加算する。同種は合計される。</summary>
        Add,
        /// <summary>加算後の値に乗算する。同種は掛け合わされる。</summary>
        Multiply,
        /// <summary>他を無視して値を上書きする。複数あれば優先度が最も高いものが勝つ。</summary>
        Override,
    }

    /// <summary>
    /// パラメータへ掛かる 1 件分の修飾子（バフ・デバフ・パッシブ補正など）。
    /// <para>
    /// 生成後は変更されない不変オブジェクト。付与元を <see cref="Source"/> に持たせることで、
    /// エフェクト解除時に <see cref="Parameter.RemoveModifiersFromSource"/> で
    /// まとめて剥がせるようにしている。
    /// </para>
    /// </summary>
    public sealed class ParameterModifier
    {
        /// <summary>適用方式（加算・乗算・上書き）。</summary>
        public ParameterModifierType Type { get; }
        /// <summary>修飾値。方式によって加算値・倍率・上書き値のいずれかとして扱われる。</summary>
        public float Value { get; }
        /// <summary>この修飾子の付与元。解除時の照合に使うため参照比較できるものを渡す。</summary>
        public object Source { get; }
        /// <summary>Override 同士が競合したときの優先度。高い方が採用される。</summary>
        public int Priority { get; }

        /// <param name="aType">適用方式。</param>
        /// <param name="aSource">付与元。</param>
        /// <param name="aValue">修飾値。</param>
        /// <param name="aPriority">Override 競合時の優先度。</param>
        public ParameterModifier(ParameterModifierType aType, object aSource, float aValue, int aPriority = 0)
        {
            Type = aType;
            Source = aSource;
            Value = aValue;
            Priority = aPriority;
        }
    }
}
