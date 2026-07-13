/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file RecentAssetsWindow.cs
 * @author hqrse
 * @date 2026/07/10
 * @brief 最近開いたアセットの履歴を表示するウィンドウ
 * 種類によるフィルタリングと文字列検索に対応し、
 * 一覧のダブルクリックでアセットを開くことができる
 * =====================================*/
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RecentAssetsWindow.Editor
{
    public class RecentAssetsWindow : EditorWindow
    {
        private class DisplayEntry
        {
            public string Guid;
            public string Path;
            public string Name;
            public string Category;
            public Texture Icon;
        }

        private const string AllCategoryLabel = "All";

        private string mSearchText = string.Empty;
        private string mSelectedCategory = AllCategoryLabel;
        private Vector2 mScrollPosition;
        private List<DisplayEntry> mCachedDisplayEntries = new List<DisplayEntry>();

        [MenuItem("Window/Recent Assets")]
        public static void Open()
        {
            var window = GetWindow<RecentAssetsWindow>();
            window.titleContent = new GUIContent("Recent Assets");
            window.minSize = new Vector2(320, 240);
            window.Show();
        }

        private void OnEnable()
        {
            RecentAssetsHistory.OnChanged += HandleHistoryChanged;
            RebuildDisplayEntries();
        }

        private void OnDisable()
        {
            RecentAssetsHistory.OnChanged -= HandleHistoryChanged;
        }

        private void HandleHistoryChanged()
        {
            RebuildDisplayEntries();
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawCategoryFilter();
            DrawList();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            mSearchText = EditorGUILayout.TextField(mSearchText, EditorStyles.toolbarSearchField);

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                if (EditorUtility.DisplayDialog("Recent Assets", "履歴をすべて削除しますか？", "削除", "キャンセル"))
                {
                    RecentAssetsHistory.Clear();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCategoryFilter()
        {
            var categories = new List<string> { AllCategoryLabel };
            categories.AddRange(mCachedDisplayEntries
                .Select(aEntry => aEntry.Category)
                .Distinct()
                .OrderBy(aCategory => aCategory));

            if (!categories.Contains(mSelectedCategory))
            {
                mSelectedCategory = AllCategoryLabel;
            }

            EditorGUILayout.BeginHorizontal();
            foreach (var category in categories)
            {
                var isSelected = mSelectedCategory == category;
                var pressed = GUILayout.Toggle(isSelected, category, EditorStyles.toolbarButton);
                if (pressed && !isSelected)
                {
                    mSelectedCategory = category;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawList()
        {
            var filtered = mCachedDisplayEntries.Where(MatchesFilter).ToList();

            mScrollPosition = EditorGUILayout.BeginScrollView(mScrollPosition);
            foreach (var entry in filtered)
            {
                DrawRow(entry);
            }
            EditorGUILayout.EndScrollView();

            if (filtered.Count == 0)
            {
                EditorGUILayout.HelpBox("該当するアセットがありません。", MessageType.Info);
            }
        }

        private bool MatchesFilter(DisplayEntry aEntry)
        {
            if (mSelectedCategory != AllCategoryLabel && aEntry.Category != mSelectedCategory)
            {
                return false;
            }

            if (string.IsNullOrEmpty(mSearchText))
            {
                return true;
            }

            return aEntry.Name.IndexOf(mSearchText, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawRow(DisplayEntry aEntry)
        {
            var rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(20));

            GUILayout.Label(aEntry.Icon, GUILayout.Width(18), GUILayout.Height(18));
            GUILayout.Label(aEntry.Name, GUILayout.Width(180));
            GUILayout.Label(aEntry.Category, EditorStyles.miniLabel, GUILayout.Width(90));
            GUILayout.Label(aEntry.Path, EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();

            if (Event.current.type != EventType.MouseDown || !rect.Contains(Event.current.mousePosition))
            {
                return;
            }

            var obj = AssetDatabase.LoadMainAssetAtPath(aEntry.Path);
            if (obj == null)
            {
                return;
            }

            if (Event.current.clickCount == 2)
            {
                AssetDatabase.OpenAsset(obj);
                Event.current.Use();
            }
            else
            {
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
            }
        }

        private void RebuildDisplayEntries()
        {
            RecentAssetsHistory.RemoveMissing();

            mCachedDisplayEntries = RecentAssetsHistory.GetAll()
                .Select(BuildDisplayEntry)
                .Where(aEntry => aEntry != null)
                .ToList();
        }

        private DisplayEntry BuildDisplayEntry(RecentAssetEntry aSourceEntry)
        {
            var path = AssetDatabase.GUIDToAssetPath(aSourceEntry.Guid);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return new DisplayEntry
            {
                Guid = aSourceEntry.Guid,
                Path = path,
                Name = Path.GetFileNameWithoutExtension(path),
                Category = GetCategory(path),
                Icon = AssetDatabase.GetCachedIcon(path)
            };
        }

        private static string GetCategory(string aPath)
        {
            var type = AssetDatabase.GetMainAssetTypeAtPath(aPath);
            if (type == null)
            {
                return "Other";
            }

            if (type == typeof(SceneAsset))
            {
                return "Scene";
            }

            if (type == typeof(GameObject))
            {
                return "Prefab";
            }

            if (type == typeof(MonoScript))
            {
                return "Script";
            }

            if (typeof(ScriptableObject).IsAssignableFrom(type))
            {
                return "ScriptableObject";
            }

            return type.Name;
        }
    }
}
