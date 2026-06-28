/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitVisualCatalog.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief PPユニットビジュアルのカタログ
 * =====================================*/
using UnityEngine;

namespace PPCore
{
    [CreateAssetMenu(menuName = "Project-Pudding/PPUnitVisualCatalog")]
    public class PPUnitVisualCatalog : PPCatalogAsset<PPUnitVisualDefinition>
    {
        protected override string IdOf(PPUnitVisualDefinition aItem) => aItem.UnitId;
    }
}