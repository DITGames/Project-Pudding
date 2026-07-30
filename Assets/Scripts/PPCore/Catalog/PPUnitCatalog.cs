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
    // ユニット ID からユニット定義を引くカタログ
    // パーティ編成やセーブデータの復元で、ID から実体を解決するのに使う
    [CreateAssetMenu(menuName = "Project-Pudding/Catalog/PPUnitCatalog")]
    public class PPUnitCatalog : PPCatalogAsset<PPUnitDefinition>
    {
        // ユニット定義の ID を返す
        // aItem : 対象のユニット定義
        protected override string IdOf(PPUnitDefinition aItem) => aItem.UnitId;
    }
}
