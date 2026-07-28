/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillCategory.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief スキルの種別
 * =====================================*/

using UnityEngine;

namespace PPCore
{
    public enum PPSkillCategory
    {
        [InspectorName(PPSkillCategoryDefinition.NamePhysical)]
        Physical = 0,
        [InspectorName(PPSkillCategoryDefinition.NameSpecial)]
        Special = 1,
        [InspectorName(PPSkillCategoryDefinition.NameSupport)]
        Support = 2,
        [InspectorName(PPSkillCategoryDefinition.NameDebuff)]
        Debuff = 3,
        [InspectorName(PPSkillCategoryDefinition.NameHeal)]
        Heal = 4,
    }

    public static class PPSkillCategoryDefinition
    {
        public const string NamePhysical = "物理";
        public const string NameSpecial = "特殊";
        public const string NameSupport = "支援";
        public const string NameDebuff = "妨害";
        public const string NameHeal = "回復";
    }
}