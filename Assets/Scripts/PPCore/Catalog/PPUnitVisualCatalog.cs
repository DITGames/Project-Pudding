/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitVisualCatalog.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief PPユニットビジュアルカタログ
 * =====================================*/
using UnityEngine;

namespace PPCore
{
    [CreateAssetMenu(menuName = "Project-Pudding/Catalog/PPUnitVisualCatalog")]
    public class PPUnitVisualCatalog : PPCatalogAsset<PPUnitVisualDefinition>
    {
        protected override string IdOf(PPUnitVisualDefinition aItem) => aItem.UnitId;
    }
}