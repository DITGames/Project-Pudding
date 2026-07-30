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
    /// <summary>
    /// スキル ID から見た目定義を引くカタログ。
    /// スキルの性能定義と見た目定義を別アセットに分けているため、両者は ID で対応付ける。
    /// </summary>
    [CreateAssetMenu(fileName = "PPSkillVisualCatalog", menuName = "Project-Pudding/Catalog/PPSkillVisualCatalog")]
    public class PPSkillVisualCatalog : PPCatalogAsset<PPSkillVisualDefinition>
    {
        /// <summary>スキル見た目定義が対応するスキル ID を返す。</summary>
        /// <param name="aItem">対象の見た目定義。</param>
        protected override string IdOf(PPSkillVisualDefinition aItem) => aItem.SkillId;
    }
}