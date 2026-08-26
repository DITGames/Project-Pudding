/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillGroup.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief スキルの同種グループ
 * =====================================*/

using UnityEngine;

namespace PPCore
{
    // スキルの同種グループ
    // AI が「今撃てる中で最も強いもの」と「待てば撃てる上位のもの」を比べる単位になる
    // 型階層（PPAttackSkillDefinition など）とは独立した軸で、
    // 同じ型のスキルでも別グループに属せるようにしている
    // 表示・分類用の PPSkillCategory とも別軸なので混同しないこと
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
        public const string NameAttack = "攻撃";
        public const string NameSupport = "支援";
        public const string NameHeal = "回復";

        // 同種グループを日本語表記へ変換する
        // aGroup : 変換するグループ
        // return : 日本語の表記。未知の値は空文字
        public static string ToDisplayString(PPSkillGroup aGroup)
            => aGroup switch
            {
                PPSkillGroup.Attack => NameAttack,
                PPSkillGroup.Support => NameSupport,
                PPSkillGroup.Heal => NameHeal,
                _ => "",
            };
    }
}
