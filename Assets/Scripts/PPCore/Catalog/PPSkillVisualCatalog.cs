/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillVisualCatalog.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief スキルビジュアルカタログ
 * =====================================*/

using UnityEngine;

namespace PPCore
{
    // スキル ID から見た目定義を引くカタログ
    // スキルの性能定義と見た目定義を別アセットに分けているため、両者は ID で対応付ける
    [CreateAssetMenu(fileName = "PPSkillVisualCatalog", menuName = "Project-Pudding/Catalog/PPSkillVisualCatalog")]
    public class PPSkillVisualCatalog : PPCatalogAsset<PPSkillVisualDefinition>
    {
        // スキル見た目定義が対応するスキル ID を返す
        // aItem : 対象の見た目定義
        protected override string IdOf(PPSkillVisualDefinition aItem) => aItem.SkillId;
    }
}
