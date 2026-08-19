/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPModeCreateWindow.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief 新規モード作成・既存モード編集用のポップアップEditorWindow
 * モード名 + 登録済み全ツール名のチェックボックス一覧を表示し、保存時にMCPModeRegistryへ反映する
 * =====================================*/

using System.Collections.Generic;
using System.Linq;
using MCPBridge.Editor.Mode;
using UnityEditor;
using UnityEngine;

namespace MCPBridge.Editor.Window
{
    public class MCPModeCreateWindow : EditorWindow
    {
        private string mModeName = "";
        private readonly Dictionary<string, bool> mToolEnabled = new();
        private Vector2 mScrollPosition;

        // 新規モード作成ウィンドウを開く。aAllToolNamesはチェックボックスとして列挙するツール名一覧
        public static void Open(IEnumerable<string> aAllToolNames)
        {
            var window = CreateInstance<MCPModeCreateWindow>();
            window.titleContent = new GUIContent("新規モード作成");
            window.minSize = new Vector2(280, 320);
            window.InitializeToolList(aAllToolNames);
            window.ShowUtility();
        }

        private void InitializeToolList(IEnumerable<string> aAllToolNames)
        {
            mToolEnabled.Clear();
            foreach (var toolName in aAllToolNames)
            {
                mToolEnabled[toolName] = false;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("モード名", EditorStyles.boldLabel);
            mModeName = EditorGUILayout.TextField(mModeName);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("許可するツール", EditorStyles.boldLabel);

            mScrollPosition = EditorGUILayout.BeginScrollView(mScrollPosition);
            foreach (var toolName in mToolEnabled.Keys.ToList())
            {
                mToolEnabled[toolName] = EditorGUILayout.ToggleLeft(toolName, mToolEnabled[toolName]);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(mModeName)))
            {
                if (GUILayout.Button("保存"))
                {
                    var allowedToolNames = mToolEnabled.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
                    MCPModeRegistry.CreateOrUpdateMode(mModeName, allowedToolNames);
                    Close();
                }
            }
        }
    }
}
