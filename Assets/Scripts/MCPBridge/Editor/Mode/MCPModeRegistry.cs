/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPModeRegistry.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief 現在選択中のツール利用モードを保持し、tools/list・tools/callからの許可チェックに使う
 * モードの切替・新規作成はUnity側のEditorWindow上でのみ行う(MCP経由で変更する手段は用意しない)。
 * モード切替はClaude Code側へ次回のtools/list呼び出し時に反映する(即時通知は行わない)
 * =====================================*/

using System.Collections.Generic;
using System.Linq;
using MCPBridge.Editor.Window;
using UnityEditor;

namespace MCPBridge.Editor.Mode
{
    [InitializeOnLoad]
    public static class MCPModeRegistry
    {
        // モード切替・新規作成時に発火する(MCPBridgeWindowのRepaintトリガーに使う)
        public static event System.Action OnModeChanged;

        public static List<MCPToolMode> Modes { get; private set; }
        public static MCPToolMode CurrentMode { get; private set; }

        static MCPModeRegistry()
        {
            var (modes, currentName) = MCPModeStore.Load();
            Modes = modes;
            CurrentMode = modes.FirstOrDefault(m => m.Name == currentName) ?? modes[0];
        }

        public static bool IsAllowed(string aToolName) => CurrentMode.AllowedToolNames.Contains(aToolName);

        // EditorWindow上のモード選択UIから呼ばれる
        public static void SwitchTo(string aModeName)
        {
            var next = Modes.FirstOrDefault(m => m.Name == aModeName);
            if (next == null || next == CurrentMode)
            {
                return;
            }

            var previousName = CurrentMode.Name;
            CurrentMode = next;
            MCPModeStore.Save(Modes, CurrentMode.Name);
            MCPSystemEventLog.Record($"モード切替: {previousName} → {next.Name}");
            OnModeChanged?.Invoke();
        }

        // EditorWindow上の新規モード作成UIから呼ばれる。既存名なら許可ツール一覧を上書きする
        public static void CreateOrUpdateMode(string aName, IReadOnlyList<string> aAllowedToolNames)
        {
            var existing = Modes.FirstOrDefault(m => m.Name == aName);
            if (existing != null)
            {
                existing.AllowedToolNames = aAllowedToolNames.ToList();
            }
            else
            {
                Modes.Add(new MCPToolMode { Name = aName, AllowedToolNames = aAllowedToolNames.ToList() });
            }

            MCPModeStore.Save(Modes, CurrentMode.Name);
            MCPSystemEventLog.Record($"モード作成/更新: {aName}");
            OnModeChanged?.Invoke();
        }
    }
}
