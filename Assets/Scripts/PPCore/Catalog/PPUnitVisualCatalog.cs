/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitVisualCatalog.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief ユニットビジュアルカタログ
 * =====================================*/

using UnityEngine;

namespace PPCore
{
    // ユニット ID から見た目定義を引くカタログ
    // ユニットの性能定義と見た目定義を別アセットに分けているため、両者は ID で対応付ける
    [CreateAssetMenu(menuName = "Project-Pudding/Catalog/PPUnitVisualCatalog")]
    public class PPUnitVisualCatalog : PPCatalogAsset<PPUnitVisualDefinition>
    {
        // ユニット見た目定義が対応するユニット ID を返す
        // aItem : 対象の見た目定義
        protected override string IdOf(PPUnitVisualDefinition aItem) => aItem.UnitId;
    }
}
