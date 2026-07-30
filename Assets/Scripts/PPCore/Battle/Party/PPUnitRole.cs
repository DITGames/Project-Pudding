/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitRole.cs
 * @author hqrse
 * @date 2026/07/23
 * @brief ユニットに割り当てるパーティ内の役割
 * =====================================*/
using UnityEngine;

namespace PPCore
{
    // ユニットに割り当てるパーティ内の役割
    // パーティ AI が行動の優先順位を付ける際、この役割に対応する状況ウェイトが掛かる
    // PPBattleRole と選択肢は近いが、こちらは継承指定を持つ点が異なる
    public enum PPUnitRole
    {
        // パーティ側の既定ロールを継承する。ユニット個別に指定しない場合の既定値
        [InspectorName("継承")]
        Inherit = 0,
        // 役割なし。AI は 3 ロールの平均ウェイトで扱う
        [InspectorName("なし")]
        None,
        // 攻撃役
        [InspectorName("攻撃")]
        Attacker,
        // 支援役
        [InspectorName("サポート")]
        Supporter,
        // 回復役
        [InspectorName("回復")]
        Healer,
    }
}
