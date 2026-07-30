/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillCatalog.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief スキルのカタログ
 * =====================================*/
using UnityEngine;

namespace PPCore
{
    // スキル ID からスキル定義を引くカタログ
    // スキル ID だけを保存しておき、実行時にここから定義を復元する用途に使う
    [CreateAssetMenu(menuName = "Project-Pudding/Catalog/PPSkillCatalog")]
    public class PPSkillCatalog : PPCatalogAsset<PPSkillDefinition>
    {
        // スキル定義の ID を返す
        // aItem : 対象のスキル定義
        protected override string IdOf(PPSkillDefinition aItem) => aItem.SkillId;
    }
}
