/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ICoinResourceConverter.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief コインのリソース化コンバーター
 * =====================================*/

namespace PPCore
{
    // コインの枚数をバトルのリソース量へ変換する規則
    // 「まとめて落とすほど効率が上がる」といった非線形な変換へ差し替えられるよう分離してある
    public interface IPPCoinResourceConverter
    {
        // コイン枚数をリソース量へ変換する
        // aCoinCount : 獲得したコインの枚数
        // aRate : パーティの変換係数
        // return : 加算するリソース量
        float Convert(int aCoinCount, float aRate);
    }

    // 枚数に係数を掛けるだけの線形変換。既定の実装
    public class PPLinearCoinResourceConverter : IPPCoinResourceConverter
    {
        // 枚数 × 係数をそのまま返す
        // aCoinCount : 獲得したコインの枚数
        // aRate : パーティの変換係数
        public float Convert(int aCoinCount, float aRate) => aCoinCount * aRate;
    }
}
