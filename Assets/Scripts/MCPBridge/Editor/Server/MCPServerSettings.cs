/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file MCPServerSettings.cs
 * @author hqrse
 * @date 2026/11/09
 * @brief MCPサーバーの自動起動設定とPreferences項目
 * 自動起動は非推奨扱いで既定はOFF。設定はEditorPrefs(ユーザー単位)に保存する
 * =====================================*/

using UnityEditor;

namespace MCPBridge.Editor.Server
{
    public static class MCPServerSettings
    {
        private const string AutoStartKey = "MCPBridge.AutoStartOnEditorLaunch";

        // Editor起動時にMCPサーバーを自動起動するか(既定false)
        public static bool AutoStartOnEditorLaunch
        {
            get => EditorPrefs.GetBool(AutoStartKey, false);
            set => EditorPrefs.SetBool(AutoStartKey, value);
        }

        [SettingsProvider]
        private static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Preferences/MCP Bridge", SettingsScope.User)
            {
                label = "MCP Bridge",
                guiHandler = aSearchContext =>
                {
                    EditorGUILayout.Space();
                    EditorGUI.BeginChangeCheck();
                    var value = EditorGUILayout.ToggleLeft(
                        "Editor起動時にMCPサーバーを自動起動する(非推奨)", AutoStartOnEditorLaunch);
                    if (EditorGUI.EndChangeCheck())
                    {
                        AutoStartOnEditorLaunch = value;
                    }
                    EditorGUILayout.HelpBox(
                        "既定はOFFです。サーバーは Tools/MCP Bridge メニューから手動で起動・停止できます。",
                        MessageType.Info);
                },
                keywords = new[] { "MCP", "Bridge", "サーバー", "自動起動" },
            };
        }
    }
}
