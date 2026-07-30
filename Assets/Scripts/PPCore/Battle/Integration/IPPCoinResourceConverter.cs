/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ICoinResourceConverter.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief コインのリソース化コンバーター
 * =====================================*/

namespace PPCore
{
    /// <summary>
    /// コインの枚数をバトルのリソース量へ変換する規則。
    /// 「まとめて落とすほど効率が上がる」といった非線形な変換へ差し替えられるよう分離してある。
    /// </summary>
    public interface IPPCoinResourceConverter
    {
        /// <summary>
        /// コイン枚数をリソース量へ変換する。
        /// </summary>
        /// <param name="aCoinCount">獲得したコインの枚数。</param>
        /// <param name="aRate">パーティの変換係数。</param>
        /// <returns>加算するリソース量。</returns>
        float Convert(int aCoinCount, float aRate);
    }

    /// <summary>
    /// 枚数に係数を掛けるだけの線形変換。既定の実装。
    /// </summary>
    public class PPLinearCoinResourceConverter : IPPCoinResourceConverter
    {
        /// <summary>枚数 × 係数をそのまま返す。</summary>
        /// <param name="aCoinCount">獲得したコインの枚数。</param>
        /// <param name="aRate">パーティの変換係数。</param>
        public float Convert(int aCoinCount, float aRate) => aCoinCount * aRate;
    }
}
