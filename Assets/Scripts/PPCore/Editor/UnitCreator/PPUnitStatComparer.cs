/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitStatComparer.cs
 * @author hqrse
 * @date 2026/09/26
 * @brief 下書きと既存ユニットの能力比較・ランキングをUIとMCPで共通に評価する
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace PPCore
{
    // 1レベル分の比較結果。配列は StatNames と同じ並び（0=HP〜4=きようさ）
    public sealed class PPUnitStatComparison
    {
        public int Level { get; }
        public float[] Draft { get; }
        public float[] Target { get; }
        // 下書き − 比較対象
        public float[] Diff { get; }

        public PPUnitStatComparison(int aLevel, float[] aDraft, float[] aTarget)
        {
            Level = aLevel;
            Draft = aDraft;
            Target = aTarget;
            Diff = aDraft.Select((aValue, aIndex) => aValue - aTarget[aIndex]).ToArray();
        }
    }

    // ランキングの1行。下書きの行は Unit が保存前のインスタンスになる
    public sealed class PPUnitRankingEntry
    {
        public int Rank { get; }
        public PPUnitDefinition Unit { get; }
        public float Value { get; }
        public bool IsDraft { get; }

        public PPUnitRankingEntry(int aRank, PPUnitDefinition aUnit, float aValue, bool aIsDraft)
        {
            Rank = aRank;
            Unit = aUnit;
            Value = aValue;
            IsDraft = aIsDraft;
        }
    }

    public static class PPUnitStatComparer
    {
        public static int StatCount => PPUnitCreationDraft.StatNames.Length;
        // MCPで能力を指定するキー。StatNames と同じ並び
        public static readonly string[] StatKeys = { "hp", "strength", "guard", "agility", "dexterity" };

        // 5能力の育成値。計算はランタイムと同じ評価式に任せる
        public static float[] Evaluate(PPUnitDefinition aUnit, int aLevel)
        {
            var stats = aUnit.EvaluateStats(aLevel);
            return new[] { stats.MaxHP, stats.Attack, stats.Defense, stats.Speed, aUnit.EvaluateDexterity(aLevel) };
        }

        // 編集ビューの系列（初期・指定・最大）のレベルと表示名。MCP の levels 省略時の既定も同じ3点
        public static int[] SeriesLevels(PPUnitCreationDraft aDraft) => new[] { 1, aDraft.PreviewLevel, aDraft.MaxLevel };

        public static string[] LevelLabels(PPUnitCreationDraft aDraft) =>
            new[] { "初期 (Lv1)", "指定 (Lv" + aDraft.PreviewLevel + ")", "最大 (Lv" + aDraft.MaxLevel + ")" };

        // 比較対象も下書きと同じレベルで評価する
        public static PPUnitStatComparison Compare(PPUnitCreationDraft aDraft, PPUnitDefinition aTarget, int aLevel)
            => new(aLevel, Evaluate(aDraft.Unit, aLevel), Evaluate(aTarget, aLevel));

        // Assets配下の全ユニット定義。サブアセットの定義も含める
        public static List<PPUnitDefinition> FindUnits() => AssetDatabase.FindAssets("t:" + nameof(PPUnitDefinition), new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath).Distinct().SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<PPUnitDefinition>().ToList();

        // 値の降順に並べ、同値は同順位（1,2,2,4）にする。生成済み下書きの出力アセットと編集モードの元ユニットは、下書きの行と重複するため除く
        public static List<PPUnitRankingEntry> Rank(PPUnitCreationDraft aDraft, IEnumerable<PPUnitDefinition> aUnits, int aStat, int aLevel)
        {
            var created = aDraft.IsCreated ? aDraft.CreatedPaths[0] : null;
            var rows = aUnits.Where(aUnit => aUnit != null && aUnit != aDraft.Unit && AssetDatabase.GetAssetPath(aUnit) != created && (!aDraft.IsEditing || aUnit != aDraft.Source))
                .Select(aUnit => (unit: aUnit, draft: false)).Append((unit: aDraft.Unit, draft: true))
                .Select(aRow => (aRow.unit, aRow.draft, value: Evaluate(aRow.unit, aLevel)[aStat]))
                .OrderByDescending(aRow => aRow.value).ThenByDescending(aRow => aRow.draft).ThenBy(aRow => aRow.unit.UnitId, StringComparer.Ordinal)
                .ToList();
            var result = new List<PPUnitRankingEntry>();
            for (var i = 0; i < rows.Count; i++)
            {
                var rank = i > 0 && rows[i].value == rows[i - 1].value ? result[i - 1].Rank : i + 1;
                result.Add(new PPUnitRankingEntry(rank, rows[i].unit, rows[i].value, rows[i].draft));
            }
            return result;
        }
    }
}
