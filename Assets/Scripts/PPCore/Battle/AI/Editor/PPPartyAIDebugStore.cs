/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyAIDebugStore.cs
 * @author hqrse
 * @date 2026/08/10
 * @brief AI思考記録の履歴を保持する静的ストア
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEditor;

namespace PPCore
{
    // AI 思考記録の履歴を保持する静的ストア
    // ウィンドウを開いていなくても記録を取り続けるため、ウィンドウ側ではなくここに履歴を置く
    // リアルタイムに進むバトルでは見たい瞬間をその場で捉えられないため、
    // 直近数件を遡れることが調整作業では必須になる
    // CustomConsoleLogStore と同じ構成
    [InitializeOnLoad]
    public static class PPPartyAIDebugStore
    {
        // 保持する思考記録の最大件数。多すぎると目的の記録を探すのが逆に手間になる
        private const int MaxReportCount = 20;

        // 新しい順に並んだ思考記録
        private static readonly List<PPPartyAIThinkReport> sReports = new();

        // 記録が増減したときに発火する
        public static event Action OnReportsChanged;

        public static IReadOnlyList<PPPartyAIThinkReport> Reports => sReports;

        static PPPartyAIDebugStore()
        {
            PPPartyAIDebugHub.OnReported += HandleReported;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        // 履歴をすべて破棄する
        public static void Clear()
        {
            sReports.Clear();
            OnReportsChanged?.Invoke();
        }

        // 思考記録を先頭へ積む。古いものは上限を超えた分から捨てる
        // aReport : 受け取った思考記録
        private static void HandleReported(PPPartyAIThinkReport aReport)
        {
            if (aReport == null)
                return;

            sReports.Insert(0, aReport);
            while (sReports.Count > MaxReportCount)
            {
                sReports.RemoveAt(sReports.Count - 1);
            }
            OnReportsChanged?.Invoke();
        }

        // 再生開始時に前回の記録を消す。前回の実行分と混ざると読み違える
        // aChange : 再生状態の変化
        private static void HandlePlayModeStateChanged(PlayModeStateChange aChange)
        {
            if (aChange == PlayModeStateChange.ExitingEditMode)
            {
                Clear();
            }
        }
    }
}
