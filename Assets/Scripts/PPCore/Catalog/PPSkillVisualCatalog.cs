/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillVisualCatalog.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief PPスキルビジュアルカタログ
 * =====================================*/
using UnityEngine;

namespace PPCore
{
    [CreateAssetMenu(fileName = "PPSkillVisualCatalog", menuName = "Project-Pudding/Catalog/PPSkillVisualCatalog")]
    public class PPSkillVisualCatalog : PPCatalogAsset<PPSkillVisualDefinition>
    {
        protected override string IdOf(PPSkillVisualDefinition aItem) => aItem.SkillId;
    }
}