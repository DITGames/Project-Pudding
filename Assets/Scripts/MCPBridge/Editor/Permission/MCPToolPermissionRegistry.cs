/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file MCPToolPermissionRegistry.cs
 * @author hqrse
 * @date 2026/09/10
 * @brief 現在許可されているツール名の集合を保持し、tools/list・tools/callからの許可チェックに使う
 * 許可状態の切替はUnity側のEditorWindow上のトグルUIでのみ行う(MCP経由で変更する手段は用意しない)。
 * 切替はClaude Code側へ次回のtools/list呼び出し時に反映する(即時通知は行わない)
 * =====================================*/

using System;
using System.Collections.Generic;
using MCPBridge.Editor.Window;
using UnityEditor;

namespace MCPBridge.Editor.Permission
{
    [InitializeOnLoad]
    public static class MCPToolPermissionRegistry
    {
        // 許可状態の切替時に発火する(MCPBridgeWindowのRepaintトリガーに使う)
        public static event Action OnPermissionsChanged;

        private static readonly HashSet<string> sAllowedToolNames;

        public static IReadOnlyCollection<string> AllowedToolNames => sAllowedToolNames;

        static MCPToolPermissionRegistry()
        {
            sAllowedToolNames = MCPToolPermissionStore.Load();
        }

        public static bool IsAllowed(string aToolName) => sAllowedToolNames.Contains(aToolName);

        // EditorWindow上のトグルUIから呼ばれる
        public static void SetAllowed(string aToolName, bool aAllowed)
        {
            var changed = aAllowed ? sAllowedToolNames.Add(aToolName) : sAllowedToolNames.Remove(aToolName);
            if (!changed)
            {
                return;
            }

            MCPToolPermissionStore.Save(sAllowedToolNames);
            MCPSystemEventLog.Record($"ツール許可切替: {aToolName} → {(aAllowed ? "許可" : "拒否")}");
            OnPermissionsChanged?.Invoke();
        }
    }
}
