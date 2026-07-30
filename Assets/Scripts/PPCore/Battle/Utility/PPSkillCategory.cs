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
    // スキルの種別。UI 表示の分類と、ダメージ情報へ引き継がれる性質の区別に使う
    // AI が使う PPBattleSkillRole とは目的が異なる別軸の分類
    public enum PPSkillCategory
    {
        [InspectorName(PPSkillCategoryDefinition.NamePhysical)]
        Physical = 0,
        [InspectorName(PPSkillCategoryDefinition.NameSpecial)]
        Special = 1,
        // 味方を強化する支援
        [InspectorName(PPSkillCategoryDefinition.NameSupport)]
        Support = 2,
        // 敵を弱体化する妨害
        [InspectorName(PPSkillCategoryDefinition.NameDebuff)]
        Debuff = 3,
        [InspectorName(PPSkillCategoryDefinition.NameHeal)]
        Heal = 4,
    }

    // スキル種別の日本語表示名を集約した定数群。表示文字列をハードコードせずここを参照する
    public static class PPSkillCategoryDefinition
    {
        public const string NamePhysical = "物理";
        public const string NameSpecial = "特殊";
        public const string NameSupport = "支援";
        public const string NameDebuff = "妨害";
        public const string NameHeal = "回復";
    }
}
