/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitCatalog.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief ユニットのカタログ
 * =====================================*/
using UnityEngine;

namespace PPCore
{
    /// <summary>
    /// ユニット ID からユニット定義を引くカタログ。
    /// パーティ編成やセーブデータの復元で、ID から実体を解決するのに使う。
    /// </summary>
    [CreateAssetMenu(menuName = "Project-Pudding/Catalog/PPUnitCatalog")]
    public class PPUnitCatalog : PPCatalogAsset<PPUnitDefinition>
    {
        /// <summary>ユニット定義の ID を返す。</summary>
        /// <param name="aItem">対象のユニット定義。</param>
        protected override string IdOf(PPUnitDefinition aItem) => aItem.UnitId;
    }
}