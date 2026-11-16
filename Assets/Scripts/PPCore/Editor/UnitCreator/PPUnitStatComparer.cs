/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitStatComparer.cs
 * @author hqrse
 * @date 2026/09/26
 * @brief ユニットのステータス比較と成長テンプレート
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

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

    public static class PPUnitGrowthTemplate
    {
        public static readonly string[] Names = { "線形", "早熟", "晩成", "S字" };

        // 正規化したテンプレートを倍率曲線へ変換する
        public static AnimationCurve Create(int aKind, int aLevel, float aRatio)
        {
            if (aLevel == 1) return AnimationCurve.Constant(1, 1, 1);
            var keys = new Keyframe[9];
            for (var i = 0; i < keys.Length; i++)
            {
                var t = i / 8f;
                var value = aKind switch { 1 => 2 * t - t * t, 2 => t * t, 3 => t * t * (3 - 2 * t), _ => t };
                var slope = aKind switch { 1 => 2 - 2 * t, 2 => 2 * t, 3 => 6 * t * (1 - t), _ => 1 };
                var tangent = slope * (aRatio - 1) / (aLevel - 1);
                keys[i] = new Keyframe(1 + t * (aLevel - 1), 1 + value * (aRatio - 1), tangent, tangent);
            }
            return new AnimationCurve(keys);
        }

        // 接線とウェイトを含む曲線をアフィン変換する
        public static AnimationCurve Rescale(AnimationCurve aCurve, int aOldLevel, int aNewLevel, float aOldEnd, float aNewEnd)
        {
            if (aNewLevel == 1) return new AnimationCurve(new Keyframe(1, 1));
            if (aOldLevel <= 1)
                return Create(0, aNewLevel, aNewEnd);
            var keys = aCurve.keys;
            var timeScale = (aNewLevel - 1f) / (aOldLevel - 1f);
            var flatEndpoints = Mathf.Abs(aOldEnd - 1) < 0.000001f;
            var valueScale = flatEndpoints ? 1 : (aNewEnd - 1) / (aOldEnd - 1);
            for (var i = 0; i < keys.Length; i++)
            {
                keys[i].time = 1 + (keys[i].time - 1) * timeScale;
                keys[i].value = 1 + (keys[i].value - 1) * valueScale;
                keys[i].inTangent = valueScale == 0 ? 0 : keys[i].inTangent * valueScale / timeScale;
                keys[i].outTangent = valueScale == 0 ? 0 : keys[i].outTangent * valueScale / timeScale;
                // 両端同値でも途中の山谷は消さず、必要な終端差分だけを加える
                if (flatEndpoints)
                {
                    keys[i].value += (aNewEnd - 1) * (keys[i].time - 1) / (aNewLevel - 1);
                    keys[i].inTangent += (aNewEnd - 1) / (aNewLevel - 1);
                    keys[i].outTangent += (aNewEnd - 1) / (aNewLevel - 1);
                }
            }
            return Anchor(new AnimationCurve(keys) { preWrapMode = aCurve.preWrapMode, postWrapMode = aCurve.postWrapMode }, aNewLevel);
        }

        public static AnimationCurve Anchor(AnimationCurve aCurve, int aLevel)
        {
            if (aCurve == null || aCurve.length == 0) throw new ArgumentException("曲線にキーが必要です。");
            if (aLevel == 1) return new AnimationCurve(new Keyframe(1, 1));
            if (aCurve.length < 2) throw new ArgumentException("曲線に始点と終点が必要です。");
            var keys = aCurve.keys;
            keys[0].time = 1;
            keys[0].value = 1;
            keys[keys.Length - 1].time = aLevel;
            return new AnimationCurve(keys) { preWrapMode = aCurve.preWrapMode, postWrapMode = aCurve.postWrapMode };
        }
    }
}
