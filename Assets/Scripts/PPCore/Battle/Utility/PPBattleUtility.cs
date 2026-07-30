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
    // ユニットに割り当てる戦闘ロール。パーティ AI が行動の優先順位を決める際の分類
    public enum PPBattleRole
    {
        [InspectorName(PPBattleUtilityDefinition.RoleNameAttack)]
        Attacker,
        [InspectorName(PPBattleUtilityDefinition.RoleNameSupport)]
        Supporter,
        [InspectorName(PPBattleUtilityDefinition.RoleNameHeal)]
        Healer,
        [InspectorName("なし")]
        None,
    }

    // スキル定義側が持つロール。AI が候補を PPBattleActionRole へ変換する際の入力になる
    public enum PPBattleSkillRole
    {
        [InspectorName(PPBattleUtilityDefinition.RoleNameAttack)]
        Attack,
        [InspectorName(PPBattleUtilityDefinition.RoleNameSupport)]
        Support,
        [InspectorName(PPBattleUtilityDefinition.RoleNameHeal)]
        Heal,
        // 特殊スキル。上記に当てはまらないもの
        [InspectorName("スペシャル")]
        Special,
    }

    // AI の行動候補が持つロール。スコア関数の振り分けと実行順序の決定に使う
    public enum PPBattleActionRole
    {
        [InspectorName(PPBattleUtilityDefinition.RoleNameAttack)]
        Attack,
        [InspectorName(PPBattleUtilityDefinition.RoleNameSupport)]
        Support,
        [InspectorName(PPBattleUtilityDefinition.RoleNameHeal)]
        Heal,
        // 該当なし。スコア 0 として扱われる
        [InspectorName("なし")]
        None,
    }

    // ロールの日本語表示名を集約した定数群
    // 3 つのロール enum で同じ文言を使うため、ハードコードせずここを参照する
    public static class PPBattleUtilityDefinition
    {
        public const string RoleNameAttack = "攻撃";
        public const string RoleNameSupport = "サポート";
        public const string RoleNameHeal = "回復";
    }

    // 本作の属性
    // リソースプールの添字としてそのまま使うため、値は 0 から連番で固定されている
    // Normal は属性というより「無属性の基準リソース」の位置づけで、相性判定では常に等倍になる
    public enum PPTypeAttribute
    {
        // ノーマル。基準リソースであり、相性判定では常に等倍
        [InspectorName(PPTypeAttributeDefinition.TypeNormal)]
        Normal = 0,
        // 火。土に強く水に弱い
        [InspectorName(PPTypeAttributeDefinition.TypeFire)]
        Fire = 1,
        // 水。火に強く土に弱い
        [InspectorName(PPTypeAttributeDefinition.TypeWater)]
        Water = 2,
        // 土。水に強く火に弱い
        [InspectorName(PPTypeAttributeDefinition.TypeEarth)]
        Earth = 3,
        // 光。闇と相互に弱点を突き合う
        [InspectorName(PPTypeAttributeDefinition.TypeShine)]
        Shine = 4,
        // 闇。光と相互に弱点を突き合う
        [InspectorName(PPTypeAttributeDefinition.TypeDark)]
        Dark = 5,
    }

    // 属性に関する定数とヘルパー。表示名をハードコードせずここを参照する
    public static class PPTypeAttributeDefinition
    {
        // 属性の総数。リソースプールの配列長として使う（Normal を含む）
        public const int TypeCount = 6;
        // Normal を除いた、相性判定の対象になる属性の数
        public const int AttributeCount = 5;
        // 基準リソース（Normal）のインデックス
        public const int BaseIndex = (int)PPTypeAttribute.Normal;

        public const string TypeNormal = "ノーマル";
        public const string TypeFire = "火";
        public const string TypeWater = "水";
        public const string TypeEarth = "土";
        public const string TypeShine = "光";
        public const string TypeDark = "闇";

        // 相性判定の対象になる属性かどうか。Normal のみ false を返す
        // a : 判定する属性
        public static bool IsAttribute(PPTypeAttribute a) => a != PPTypeAttribute.Normal;
    }
}
