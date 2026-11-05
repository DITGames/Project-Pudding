/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitRole.cs
 * @author hqrse
 * @date 2026/11/06
 * @brief ユニットのロールと表示名の定義
 * =====================================*/

using UnityEngine;

namespace PPCore
{
    // ユニットのロール。既定ツリーの選択だけでなく UI 表示や編成でも使う前提でユニット定義に持たせる
    // 値はアセットへ数値のまま保存されるため明示的に振り、追加するときは末尾へ足す
    // Balance が 0 なので、ロールを持たない既存のユニット定義は移行なしでバランス型として読まれる
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
        public const string NameBalance = "バランス型";
        public const string NameAttacker = "攻撃型";
        public const string NameSupporter = "支援型";
        public const string NameDisruptor = "妨害型";
        public const string NameHealer = "回復型";

        // ロールを日本語表記へ変換する
        // aRole : 対象のロール
        // return : 日本語の表記
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
}
