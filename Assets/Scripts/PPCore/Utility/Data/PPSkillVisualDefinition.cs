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
    /// <summary>
    /// スキルの見た目に関する定義（ScriptableObject）。
    /// 性能定義（<see cref="PPSkillDefinition"/>）とはスキル ID で対応付ける。
    /// 解決は <see cref="PPSkillVisualCatalog"/> が行う。
    /// </summary>
    [CreateAssetMenu(fileName = "PPSkillVisualDefinition", menuName = "Project-Pudding/Definition/PPSkillVisualDefinition")]
    public class PPSkillVisualDefinition : ScriptableObject
    {
        /// <summary>対応するスキル ID。カタログでの解決キー。</summary>
        [Label("スキルID")]
        public string SkillId;
        /// <summary>UI に出すアイコン。</summary>
        [Label("アイコン")]
        public Sprite SkillIcon;
    }
}
