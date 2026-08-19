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
    }
}
