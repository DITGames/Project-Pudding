/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPMapListDefinition.cs
 * @author hqrse
 * @date 2026/11/22
 * @brief ステージ選択に並べるステージの一覧と、クリアの記録
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;
using AttributeUtility;
using CustomConsole;

namespace PPCore
{
    // 一覧に並べるステージ 1 件分
    [Serializable]
    public sealed class PPMapListEntry
    {
        [Label("マップ定義")] public PPMapDefinition Map;
        // 前のステージをクリアしていなくても選べる。先頭のステージは印がなくても選べる
        [Label("最初から選べる")] public bool IsUnlockedFromStart;
    }

    // クリアしたステージの記録。セーブの仕組みができるまではメモリ上だけに持ち、Play を始めるたびに空に戻す
    public static class PPStageClearRecord
    {
        /* 定数 */

        private const string LogTag = "StageMap";

        /* 状態 */

        private static readonly HashSet<PPMapDefinition> sCleared = new();

        /* 操作 */

        public static void MarkCleared(PPMapDefinition aMap)
        {
            if (aMap == null || !sCleared.Add(aMap)) return;
            CustomConsoleLog.Log(LogTag, $"クリアを記録: {aMap.DisplayName}");
        }

        /* 問い合わせ */

        public static bool IsCleared(PPMapDefinition aMap) => aMap != null && sCleared.Contains(aMap);

        /* ヘルパー */

        // ドメインの再読み込みを切っているため、前回の Play の記録が残るのを防ぐ
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => sCleared.Clear();
    }

    // ステージ選択に並べるステージの一覧。上から順に、前のステージをクリアすると次が選べるようになる
    [CreateAssetMenu(fileName = "PMLD_New", menuName = "Project-Pudding/Definition/PPMapListDefinition")]
    public class PPMapListDefinition : ScriptableObject
    {
        /* インスペクタ設定 */

        [Label("ステージ", true)]
        [SerializeField] private List<PPMapListEntry> mStages = new();

        /* 問い合わせ */

        // マップ定義が設定されている項目だけを、並べる順に返す
        public List<PPMapListEntry> GetValidStages()
        {
            var list = new List<PPMapListEntry>(mStages.Count);
            foreach (var entry in mStages)
            {
                if (entry != null && entry.Map != null) list.Add(entry);
            }
            return list;
        }

        // 先頭か、最初から選べる印があるか、ひとつ前のステージをクリアしていれば選べる
        public static bool IsUnlocked(IReadOnlyList<PPMapListEntry> aStages, int aIndex)
        {
            if (aIndex <= 0) return true;
            return aStages[aIndex].IsUnlockedFromStart || PPStageClearRecord.IsCleared(aStages[aIndex - 1].Map);
        }
    }
}
