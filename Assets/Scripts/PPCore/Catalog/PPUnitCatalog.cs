/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitCatalog.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief PPユニットのカタログ
 * =====================================*/
using UnityEngine;

namespace PPCore
{
    [CreateAssetMenu(menuName = "Project-Pudding/PPUnitCatalog")]
    public class PPUnitCatalog : PPCatalogAsset<PPUnitDefinition>
    {
        protected override string IdOf(PPUnitDefinition aItem) => aItem.UnitId;
    }
}