/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillVisualDefinition.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief PPスキルビジュアル定義
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    [CreateAssetMenu(fileName = "PPSkillVisualDefinition", menuName = "Project-Pudding/Definition/PPSkillVisualDefinition")]
    public class PPSkillVisualDefinition : ScriptableObject
    {
        [Label("スキルID")] 
        public string SkillId;
        [Label("アイコン")]
        public Sprite SkillIcon;
    }
}