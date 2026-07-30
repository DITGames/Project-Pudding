/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillVisualDefinition.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief スキルビジュアル定義
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // スキルの見た目に関する定義（ScriptableObject）
    // 性能定義（PPSkillDefinition）とはスキル ID で対応付ける
    // 解決は PPSkillVisualCatalog が行う
    [CreateAssetMenu(fileName = "PPSkillVisualDefinition", menuName = "Project-Pudding/Definition/PPSkillVisualDefinition")]
    public class PPSkillVisualDefinition : ScriptableObject
    {
        // 対応するスキル ID。カタログでの解決キー
        [Label("スキルID")]
        public string SkillId;
        [Label("アイコン")]
        public Sprite SkillIcon;
    }
}
