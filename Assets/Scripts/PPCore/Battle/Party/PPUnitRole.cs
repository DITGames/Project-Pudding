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
    /// <summary>
    /// ユニットに割り当てるパーティ内の役割。
    /// パーティ AI が行動の優先順位を付ける際、この役割に対応する状況ウェイトが掛かる。
    /// <see cref="PPBattleRole"/> と選択肢は近いが、こちらは継承指定を持つ点が異なる。
    /// </summary>
    public enum PPUnitRole
    {
        /// <summary>パーティ側の既定ロールを継承する。ユニット個別に指定しない場合の既定値。</summary>
        [InspectorName("継承")]
        Inherit = 0,
        /// <summary>役割なし。AI は 3 ロールの平均ウェイトで扱う。</summary>
        [InspectorName("なし")]
        None,
        /// <summary>攻撃役。</summary>
        [InspectorName("攻撃")]
        Attacker,
        /// <summary>支援役。</summary>
        [InspectorName("サポート")]
        Supporter,
        /// <summary>回復役。</summary>
        [InspectorName("回復")]
        Healer,
    }
}
