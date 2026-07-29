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
    /// <summary>
    /// スキルの種別。UI 表示の分類と、ダメージ情報へ引き継がれる性質の区別に使う。
    /// AI が使う <see cref="PPBattleSkillRole"/> とは目的が異なる別軸の分類。
    /// </summary>
    public enum PPSkillCategory
    {
        /// <summary>物理攻撃。</summary>
        [InspectorName(PPSkillCategoryDefinition.NamePhysical)]
        Physical = 0,
        /// <summary>特殊攻撃。</summary>
        [InspectorName(PPSkillCategoryDefinition.NameSpecial)]
        Special = 1,
        /// <summary>味方を強化する支援。</summary>
        [InspectorName(PPSkillCategoryDefinition.NameSupport)]
        Support = 2,
        /// <summary>敵を弱体化する妨害。</summary>
        [InspectorName(PPSkillCategoryDefinition.NameDebuff)]
        Debuff = 3,
        /// <summary>回復。</summary>
        [InspectorName(PPSkillCategoryDefinition.NameHeal)]
        Heal = 4,
    }

    /// <summary>
    /// スキル種別の日本語表示名を集約した定数群。表示文字列をハードコードせずここを参照する。
    /// </summary>
    public static class PPSkillCategoryDefinition
    {
        /// <summary>物理の表示名。</summary>
        public const string NamePhysical = "物理";
        /// <summary>特殊の表示名。</summary>
        public const string NameSpecial = "特殊";
        /// <summary>支援の表示名。</summary>
        public const string NameSupport = "支援";
        /// <summary>妨害の表示名。</summary>
        public const string NameDebuff = "妨害";
        /// <summary>回復の表示名。</summary>
        public const string NameHeal = "回復";
    }
}
