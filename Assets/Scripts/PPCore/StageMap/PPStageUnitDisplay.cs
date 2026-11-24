/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageUnitDisplay.cs
 * @author hqrse
 * @date 2026/11/21
 * @brief ステージマップの画面で使う、ユニットの表示用の文字と色
 * =====================================*/

using UnityEngine;

namespace PPCore
{
    // ユニットの属性・ステータスを、ステージマップの画面に出す文字と色へ変える
    public static class PPStageUnitDisplay
    {
        /* 問い合わせ */

        public static string AttributeName(PPTypeAttribute aAttribute) => aAttribute switch
        {
            PPTypeAttribute.Fire => PPTypeAttributeDefinition.TypeFire,
            PPTypeAttribute.Water => PPTypeAttributeDefinition.TypeWater,
            PPTypeAttribute.Earth => PPTypeAttributeDefinition.TypeEarth,
            PPTypeAttribute.Shine => PPTypeAttributeDefinition.TypeShine,
            PPTypeAttribute.Dark => PPTypeAttributeDefinition.TypeDark,
            _ => PPTypeAttributeDefinition.TypeNormal,
        };

        // 属性ごとの文字の色。リッチテキストの color タグに入れる 16 進の形で返す
        public static string AttributeColorCode(PPTypeAttribute aAttribute) => aAttribute switch
        {
            PPTypeAttribute.Fire => "#F0675A",
            PPTypeAttribute.Water => "#5BC0EB",
            PPTypeAttribute.Earth => "#E8B04B",
            PPTypeAttribute.Shine => "#F5E27A",
            PPTypeAttribute.Dark => "#A78BFA",
            _ => "#C8CCD8",
        };

        // 属性名を属性の色で囲んだリッチテキスト
        public static string ColoredAttribute(PPUnitDefinition aUnit)
            => $"<color={AttributeColorCode(aUnit.TypeAttribute)}>{AttributeName(aUnit.TypeAttribute)}</color>";

        public static string NameAndLevel(PPUnitDefinition aUnit, int aLevel) => $"{aUnit.DisplayName}  Lv{aLevel}";

        // ユニットのアイコン。カタログが無いか、見た目定義が無ければ null
        public static Sprite ResolveIcon(PPUnitVisualCatalog aCatalog, PPUnitDefinition aUnit)
            => aCatalog != null && aUnit != null ? aCatalog.Resolve(aUnit.UnitId)?.UnitIcon : null;

        // レベルに応じたステータスを 1 行にまとめる。数値は小数を切り捨てて出す
        public static string StatsLine(PPUnitDefinition aUnit, int aLevel)
        {
            var stats = aUnit.EvaluateStats(aLevel);
            return $"{PPUnitAbilityDefinition.NameHp} {stats.MaxHP:0}   {PPUnitAbilityDefinition.NameStrength} {stats.Attack:0}   "
                + $"{PPUnitAbilityDefinition.NameGuard} {stats.Defense:0}\n{PPUnitAbilityDefinition.NameAgility} {stats.Speed:0}   "
                + $"{PPUnitAbilityDefinition.NameDexterity} {aUnit.EvaluateDexterity(aLevel):0}";
        }
    }
}
