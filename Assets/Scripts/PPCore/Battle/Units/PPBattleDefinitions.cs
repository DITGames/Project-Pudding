/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleDefinitions.cs
 * @author hqrse
 * @date 2026/07/23
 * @brief バトル共通の列挙と表示名定数（属性・ロール・スキル分類）
 * =====================================*/

using UnityEngine;

namespace PPCore
{
    // 本作の属性。添字として配列と対応付けるため、値は 0 から連番で固定する
    // Normal は無属性の位置づけで、相性判定では常に等倍になる
    public enum PPTypeAttribute
    {
        // ノーマル。無属性であり、相性判定では常に等倍
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
        /* 定数 */

        // 属性の総数。属性別の配列長として使う（Normal を含む）
        public const int TypeCount = 6;
        // Normal を除いた、相性判定の対象になる属性の数
        public const int AttributeCount = 5;
        // 無属性（Normal）のインデックス
        public const int BaseIndex = (int)PPTypeAttribute.Normal;
        public const string TypeNormal = "ノーマル";
        public const string TypeFire = "火";
        public const string TypeWater = "水";
        public const string TypeEarth = "土";
        public const string TypeShine = "光";
        public const string TypeDark = "闇";

        /* 問い合わせ */

        // 相性判定の対象になる属性かどうか。Normal のみ false を返す
        // a : 判定する属性
        public static bool IsAttribute(PPTypeAttribute a) => a != PPTypeAttribute.Normal;
    }

    // ユニットのロール。既定ツリーの選択のほか UI 表示や編成でも使う
    // アセットへ数値で保存されるため、値は明示的に振り、追加するときは末尾へ足す
    public enum PPUnitRole
    {
        [InspectorName(PPUnitRoleDefinition.NameBalance)]
        Balance = 0,
        [InspectorName(PPUnitRoleDefinition.NameAttacker)]
        Attacker = 1,
        [InspectorName(PPUnitRoleDefinition.NameSupporter)]
        Supporter = 2,
        [InspectorName(PPUnitRoleDefinition.NameDisruptor)]
        Disruptor = 3,
        [InspectorName(PPUnitRoleDefinition.NameHealer)]
        Healer = 4,
    }

    // ロールの表示名をまとめた定義。表示名をハードコードせずここを参照する
    public static class PPUnitRoleDefinition
    {
        /* 定数 */

        public const string NameBalance = "バランス型";
        public const string NameAttacker = "攻撃型";
        public const string NameSupporter = "支援型";
        public const string NameDisruptor = "妨害型";
        public const string NameHealer = "回復型";

        /* 説明文 */

        // ロールを日本語表記へ変換する
        public static string ToDisplayString(PPUnitRole aRole)
            => aRole switch
            {
                PPUnitRole.Balance => NameBalance,
                PPUnitRole.Attacker => NameAttacker,
                PPUnitRole.Supporter => NameSupporter,
                PPUnitRole.Disruptor => NameDisruptor,
                PPUnitRole.Healer => NameHealer,
                _ => "",
            };
    }

    // スキルの種別。UI 表示の分類と、ダメージ情報へ引き継がれる性質の区別に使う
    // AI が使うスキルタグ(PPSkillTagDefinition)とは目的が異なる別軸の分類
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

    // スキルの同種グループ。AI が「今撃てる中で最も強いもの」と「待てば撃てる上位のもの」を比べる単位になる
    // 表示・分類用の PPSkillCategory とは別軸
    public enum PPSkillGroup
    {
        // 敵にダメージを与える系統
        [InspectorName("攻撃")]
        Attack = 0,
        // 味方を強化する・敵を弱体化する系統
        [InspectorName("支援")]
        Support = 1,
        // 味方の HP・状態異常を回復する系統
        [InspectorName("回復")]
        Heal = 2,
    }

    // 同種グループの日本語表示名を集約した定数群。表示文字列をハードコードせずここを参照する
    public static class PPSkillGroupDefinition
    {
        /* 定数 */

        public const string NameAttack = "攻撃";
        public const string NameSupport = "支援";
        public const string NameHeal = "回復";

        /* 説明文 */

        // 同種グループを日本語表記へ変換する
        public static string ToDisplayString(PPSkillGroup aGroup)
            => aGroup switch
            {
                PPSkillGroup.Attack => NameAttack,
                PPSkillGroup.Support => NameSupport,
                PPSkillGroup.Heal => NameHeal,
                _ => "",
            };
    }

    // 同時に行動できるユニットを並べるときの優先度。先攻ほど小さい値にしている
    // 同じ優先度どうしは速度（＋ジッター）で順序が決まる
    public enum PPSkillActionPriority
    {
        [InspectorName("先攻")]
        First = 0,
        [InspectorName("通常")]
        Normal = 1,
        [InspectorName("後攻")]
        Last = 2,
    }

    // 行動優先度の日本語表示名を集約した定数群。表示文字列をハードコードせずここを参照する
    public static class PPSkillActionPriorityDefinition
    {
        /* 定数 */

        public const string NameFirst = "先攻";
        public const string NameNormal = "通常";
        public const string NameLast = "後攻";

        /* 説明文 */

        // 優先度を日本語表記へ変換する
        public static string ToDisplayString(PPSkillActionPriority aPriority)
            => aPriority switch
            {
                PPSkillActionPriority.First => NameFirst,
                PPSkillActionPriority.Normal => NameNormal,
                PPSkillActionPriority.Last => NameLast,
                _ => "",
            };
    }
}
