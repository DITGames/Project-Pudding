/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleUtility.cs
 * @author hqrse
 * @date 2026/07/23
 * @brief バトル汎用
 * =====================================*/

using UnityEngine;

namespace PPCore
{
    /// <summary>
    /// ユニットに割り当てる戦闘ロール。パーティ AI が行動の優先順位を決める際の分類。
    /// </summary>
    public enum PPBattleRole
    {
        /// <summary>攻撃役。</summary>
        [InspectorName(PPBattleUtilityDefinition.RoleNameAttack)]
        Attacker,
        /// <summary>支援役。</summary>
        [InspectorName(PPBattleUtilityDefinition.RoleNameSupport)]
        Supporter,
        /// <summary>回復役。</summary>
        [InspectorName(PPBattleUtilityDefinition.RoleNameHeal)]
        Healer,
        /// <summary>ロール未指定。</summary>
        [InspectorName("なし")]
        None,
    }

    /// <summary>
    /// スキル定義側が持つロール。AI が候補を <see cref="PPBattleActionRole"/> へ変換する際の入力になる。
    /// </summary>
    public enum PPBattleSkillRole
    {
        /// <summary>攻撃スキル。</summary>
        [InspectorName(PPBattleUtilityDefinition.RoleNameAttack)]
        Attack,
        /// <summary>支援スキル。</summary>
        [InspectorName(PPBattleUtilityDefinition.RoleNameSupport)]
        Support,
        /// <summary>回復スキル。</summary>
        [InspectorName(PPBattleUtilityDefinition.RoleNameHeal)]
        Heal,
        /// <summary>特殊スキル。上記に当てはまらないもの。</summary>
        [InspectorName("スペシャル")]
        Special,
    }

    /// <summary>
    /// AI の行動候補が持つロール。スコア関数の振り分けと実行順序の決定に使う。
    /// </summary>
    public enum PPBattleActionRole
    {
        /// <summary>攻撃行動。</summary>
        [InspectorName(PPBattleUtilityDefinition.RoleNameAttack)]
        Attack,
        /// <summary>支援行動。</summary>
        [InspectorName(PPBattleUtilityDefinition.RoleNameSupport)]
        Support,
        /// <summary>回復行動。</summary>
        [InspectorName(PPBattleUtilityDefinition.RoleNameHeal)]
        Heal,
        /// <summary>該当なし。スコア 0 として扱われる。</summary>
        [InspectorName("なし")]
        None,
    }

    /// <summary>
    /// ロールの日本語表示名を集約した定数群。
    /// 3 つのロール enum で同じ文言を使うため、ハードコードせずここを参照する。
    /// </summary>
    public static class PPBattleUtilityDefinition
    {
        /// <summary>攻撃ロールの表示名。</summary>
        public const string RoleNameAttack = "攻撃";
        /// <summary>支援ロールの表示名。</summary>
        public const string RoleNameSupport = "サポート";
        /// <summary>回復ロールの表示名。</summary>
        public const string RoleNameHeal = "回復";
    }

    /// <summary>
    /// 本作の属性。
    /// <para>
    /// リソースプールの添字としてそのまま使うため、値は 0 から連番で固定されている。
    /// <see cref="Normal"/> は属性というより「無属性の基準リソース」の位置づけで、
    /// 相性判定では常に等倍になる。
    /// </para>
    /// </summary>
    public enum PPTypeAttribute
    {
        /// <summary>ノーマル。基準リソースであり、相性判定では常に等倍。</summary>
        [InspectorName(PPTypeAttributeDefinition.TypeNormal)]
        Normal = 0,
        /// <summary>火。土に強く水に弱い。</summary>
        [InspectorName(PPTypeAttributeDefinition.TypeFire)]
        Fire = 1,
        /// <summary>水。火に強く土に弱い。</summary>
        [InspectorName(PPTypeAttributeDefinition.TypeWater)]
        Water = 2,
        /// <summary>土。水に強く火に弱い。</summary>
        [InspectorName(PPTypeAttributeDefinition.TypeEarth)]
        Earth = 3,
        /// <summary>光。闇と相互に弱点を突き合う。</summary>
        [InspectorName(PPTypeAttributeDefinition.TypeShine)]
        Shine = 4,
        /// <summary>闇。光と相互に弱点を突き合う。</summary>
        [InspectorName(PPTypeAttributeDefinition.TypeDark)]
        Dark = 5,
    }

    /// <summary>
    /// 属性に関する定数とヘルパー。表示名をハードコードせずここを参照する。
    /// </summary>
    public static class PPTypeAttributeDefinition
    {
        /// <summary>属性の総数。リソースプールの配列長として使う（Normal を含む）。</summary>
        public const int TypeCount = 6;
        /// <summary>Normal を除いた、相性判定の対象になる属性の数。</summary>
        public const int AttributeCount = 5;
        /// <summary>基準リソース（Normal）のインデックス。</summary>
        public const int BaseIndex = (int)PPTypeAttribute.Normal;

        /// <summary>ノーマルの表示名。</summary>
        public const string TypeNormal = "ノーマル";
        /// <summary>火の表示名。</summary>
        public const string TypeFire = "火";
        /// <summary>水の表示名。</summary>
        public const string TypeWater = "水";
        /// <summary>土の表示名。</summary>
        public const string TypeEarth = "土";
        /// <summary>光の表示名。</summary>
        public const string TypeShine = "光";
        /// <summary>闇の表示名。</summary>
        public const string TypeDark = "闇";

        /// <summary>
        /// 相性判定の対象になる属性かどうか。Normal のみ false を返す。
        /// </summary>
        /// <param name="a">判定する属性。</param>
        public static bool IsAttribute(PPTypeAttribute a) => a != PPTypeAttribute.Normal;
    }
}
