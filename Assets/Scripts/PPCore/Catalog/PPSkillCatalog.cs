/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillCatalog.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief PPスキルのカタログ
 * =====================================*/
using PPCore;
using UnityEngine;

namespace PPCore
{
    [CreateAssetMenu(menuName = "Project-Pudding/PPUnitSkillCatalog")]
    public class PPSkillCatalog : PPCatalogAsset<PPSkillDefinition>
    {
        protected override string IdOf(PPSkillDefinition aItem) => aItem.SkillId;
    }
}