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
    public enum PPUnitRole
    {
        [InspectorName("継承")]
        Inherit = 0,
        [InspectorName("なし")]
        None,
        [InspectorName("攻撃")]
        Attacker,
        [InspectorName("サポート")]
        Supporter,
        [InspectorName("回復")]
        Healer,
    }
}