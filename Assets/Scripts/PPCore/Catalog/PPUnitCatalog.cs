/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitCatalog.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief ユニットのカタログ
 * =====================================*/
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    [CreateAssetMenu(menuName = "Project-Pudding/PPUnitCatalog")]
    public class PPUnitCatalog : PPCatalogAsset<UnitDefinition>
    {
        protected override string IdOf(UnitDefinition aItem) => aItem.UnitId;
    }
}