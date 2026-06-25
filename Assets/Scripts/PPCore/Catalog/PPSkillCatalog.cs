/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillCatalog.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief スキルのカタログ
 * =====================================*/
using PPBattle;
using UnityEngine;

namespace PPCore
{
    [CreateAssetMenu(menuName = "Project-Pudding/PPUnitSkillCatalog")]
    public class PPSkillCatalog : PPCatalogAsset<PPBattleSkillDefinition>
    {
        protected override string IdOf(PPBattleSkillDefinition aItem) => aItem.SkillId;
    }
}