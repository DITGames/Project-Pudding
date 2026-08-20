/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPSystemEventLog.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief モード切替等、PlanExecutorの実行ログとは別枠のシステムイベントログ
 * =====================================*/

using System;
using System.Collections.Generic;

namespace MCPBridge.Editor.Window
{
    public static class MCPSystemEventLog
    {
        public static event Action OnChanged;

        private static readonly List<string> sEntries = new();
        public static IReadOnlyList<string> Entries => sEntries;

        public static void Record(string aMessage)
        {
            sEntries.Add($"[{DateTime.Now:HH:mm:ss}] {aMessage}");
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
