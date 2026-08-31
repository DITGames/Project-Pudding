/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAITreeHighlight.cs
 * @author hqrse
 * @date 2026/08/27
 * @brief 思考経路の強調表示の種類
 * =====================================*/

namespace PPCore
{
    // 思考経路をグラフ上で強調表示する際の種類
    // 通過しただけのノードと、実際に行動が確定したノードを見分けられるようにする
    public enum PPUnitAITreeHighlight
    {
        // 強調しない
        None,
        // 経路として通過した
        Passed,
        // ここで行動が確定した
        Decided,
    }
}
