/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillActionPriority.cs
 * @author hqrse
 * @date 2026/08/26
 * @brief 行動の優先度
 * =====================================*/

using UnityEngine;

namespace PPCore
{
    // 1 ティック内で行動を並べるときの優先度
    // 同じ優先度どうしは速度（＋ジッター）で順序が決まる
    // 値の大小がそのまま並び順になるよう、先攻ほど小さい値にしている
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
        public const string NameFirst = "先攻";
        public const string NameNormal = "通常";
        public const string NameLast = "後攻";

        // 優先度を日本語表記へ変換する
        // aPriority : 変換する優先度
        // return : 日本語の表記。未知の値は空文字
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
