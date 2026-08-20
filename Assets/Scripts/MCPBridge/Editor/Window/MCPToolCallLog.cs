/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPToolCallLog.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief MCPクライアントからのtools/call呼び出し1件ごとの操作ログ
 * execute_planによる自動実行の進行(PlanExecutionState.LogEntries)とは別枠で、
 * MCPクライアントが直接呼び出したツール(execute_plan経由のToolCallステップも含む)を
 * 呼び出しごとに記録する。MCPToolRegistry.Call()はHTTPハンドラスレッド・
 * メインスレッド(execute_plan経由)のどちらからも呼ばれ得るため、
 * 記録自体は必ずMCPMainThreadDispatcher経由でメインスレッドに寄せてから行う
 * =====================================*/

using System;
using System.Collections.Generic;

namespace MCPBridge.Editor.Window
{
    public static class MCPToolCallLog
    {
        public static event Action OnChanged;

        private static readonly List<string> sEntries = new();
        public static IReadOnlyList<string> Entries => sEntries;

        public static void RecordSuccess(string aToolName)
        {
            sEntries.Add($"[{DateTime.Now:HH:mm:ss}] {aToolName} - 成功");
            OnChanged?.Invoke();
        }

        public static void RecordError(string aToolName, string aErrorMessage)
        {
            sEntries.Add($"[{DateTime.Now:HH:mm:ss}] {aToolName} - エラー: {aErrorMessage}");
            OnChanged?.Invoke();
        }

        // MCPBridgeWindowの「クリア」ボタンから呼ばれる。他のMCPツールから参照されない
        // 表示専用ログのため、そのまま空にしてよい
        public static void Clear()
        {
            sEntries.Clear();
            OnChanged?.Invoke();
        }
    }
}
