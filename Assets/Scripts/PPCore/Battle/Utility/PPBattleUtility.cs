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
    // 本作の属性
    // 添字として配列と対応付けるため、値は 0 から連番で固定されている
    // Normal は属性というより「無属性」の位置づけで、相性判定では常に等倍になる
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

        // 相性判定の対象になる属性かどうか。Normal のみ false を返す
        // a : 判定する属性
        public static bool IsAttribute(PPTypeAttribute a) => a != PPTypeAttribute.Normal;
    }
}
