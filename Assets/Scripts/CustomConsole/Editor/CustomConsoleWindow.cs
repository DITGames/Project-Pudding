/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file CustomConsoleWindow.cs
 * @author hqrse
 * @date 2026/07/13
 * @brief 標準Consoleを拡張したカスタムログビューア
 * タグ/カテゴリフィルタ・正規表現検索・送信元別フィルタ・
 * 拡張ログレベル(Verbose/Critical)・一時停止・
 * クリックでのコード/オブジェクトジャンプに対応する
 * =====================================*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CustomConsole;
using UnityEditor;
using UnityEngine;

namespace CustomConsole.Editor
{
    public class CustomConsoleWindow : EditorWindow
    {
        private const string AllSourceLabel = "All";

        private static readonly CustomLogLevel[] sLevels = (CustomLogLevel[])Enum.GetValues(typeof(CustomLogLevel));

        private string mSearchText = string.Empty;
        private bool mUseRegex;
        private bool mRegexError;
        private string mCompiledPattern;
        private Regex mCompiledRegex;

        private bool mPaused;
        private List<CustomConsoleEntry> mSnapshot;
        private bool mAutoScroll = true;

        private readonly Dictionary<CustomLogLevel, bool> mLevelEnabled = new();
        private readonly Dictionary<string, bool> mCategoryEnabled = new();
        private string mSelectedSource = AllSourceLabel;

        private CustomConsoleEntry mSelectedEntry;
        private Vector2 mListScrollPosition;
        private Vector2 mDetailScrollPosition;

        [MenuItem("Window/Custom Console")]
        public static void Open()
        {
            var window = GetWindow<CustomConsoleWindow>();
            window.titleContent = new GUIContent("Custom Console");
            window.minSize = new Vector2(480, 320);
            window.Show();
        }

        private void OnEnable()
        {
            foreach (var level in sLevels)
            {
                mLevelEnabled[level] = true;
            }
            CustomConsoleLogStore.OnEntriesChanged += HandleEntriesChanged;
        }

        private void OnDisable()
        {
            CustomConsoleLogStore.OnEntriesChanged -= HandleEntriesChanged;
        }

        private void HandleEntriesChanged()
        {
            if (!mPaused)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            var source = (mPaused && mSnapshot != null) ? mSnapshot : CustomConsoleLogStore.Entries;

            DrawToolbar();
            DrawLevelFilters(source);
            DrawCategoryFilters(source);
            DrawSourceFilter(source);

            if (mUseRegex && mRegexError)
            {
                EditorGUILayout.HelpBox("正規表現が不正です。", MessageType.Warning);
            }

            var filtered = source.Where(MatchesFilter).ToList();

            DrawList(filtered);
            DrawDetail();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                CustomConsoleLogStore.Clear();
                mSelectedEntry = null;
            }

            var pausedNow = GUILayout.Toggle(mPaused, "Pause", EditorStyles.toolbarButton, GUILayout.Width(50));
            if (pausedNow != mPaused)
            {
                mPaused = pausedNow;
                mSnapshot = mPaused ? new List<CustomConsoleEntry>(CustomConsoleLogStore.Entries) : null;
            }

            mAutoScroll = GUILayout.Toggle(mAutoScroll, "Auto Scroll", EditorStyles.toolbarButton, GUILayout.Width(80));

            var clearOnPlay = GUILayout.Toggle(CustomConsoleLogStore.ClearOnPlay, "Clear On Play", EditorStyles.toolbarButton, GUILayout.Width(90));
            if (clearOnPlay != CustomConsoleLogStore.ClearOnPlay)
            {
                CustomConsoleLogStore.ClearOnPlay = clearOnPlay;
            }

            GUILayout.FlexibleSpace();

            mUseRegex = GUILayout.Toggle(mUseRegex, "Regex", EditorStyles.toolbarButton, GUILayout.Width(50));
            mSearchText = EditorGUILayout.TextField(mSearchText, EditorStyles.toolbarSearchField, GUILayout.MinWidth(160));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawLevelFilters(IReadOnlyList<CustomConsoleEntry> aSource)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            foreach (var level in sLevels)
            {
                var count = aSource.Count(aEntry => aEntry.Level == level);
                mLevelEnabled[level] = GUILayout.Toggle(mLevelEnabled[level], $"{level} ({count})", EditorStyles.toolbarButton);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCategoryFilters(IReadOnlyList<CustomConsoleEntry> aSource)
        {
            var categories = aSource
                .Select(aEntry => aEntry.Category)
                .Where(aCategory => !string.IsNullOrEmpty(aCategory))
                .Distinct()
                .OrderBy(aCategory => aCategory)
                .ToList();

            foreach (var category in categories)
            {
                if (!mCategoryEnabled.ContainsKey(category))
                {
                    mCategoryEnabled[category] = true;
                }
            }

            if (categories.Count == 0)
            {
                return;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Tags:", GUILayout.Width(35));

            if (GUILayout.Button("All", EditorStyles.miniButtonLeft, GUILayout.Width(35)))
            {
                foreach (var category in categories)
                {
                    mCategoryEnabled[category] = true;
                }
            }
            if (GUILayout.Button("None", EditorStyles.miniButtonRight, GUILayout.Width(45)))
            {
                foreach (var category in categories)
                {
                    mCategoryEnabled[category] = false;
                }
            }

            foreach (var category in categories)
            {
                var width = Mathf.Clamp(category.Length * 8 + 20, 40, 160);
                mCategoryEnabled[category] = GUILayout.Toggle(mCategoryEnabled[category], category, EditorStyles.miniButton, GUILayout.Width(width));
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSourceFilter(IReadOnlyList<CustomConsoleEntry> aSource)
        {
            var sources = new List<string> { AllSourceLabel };
            sources.AddRange(aSource
                .Select(aEntry => aEntry.SourceLabel)
                .Where(aLabel => !string.IsNullOrEmpty(aLabel))
                .Distinct()
                .OrderBy(aLabel => aLabel));

            if (!sources.Contains(mSelectedSource))
            {
                mSelectedSource = AllSourceLabel;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Source:", GUILayout.Width(50));
            var index = sources.IndexOf(mSelectedSource);
            var newIndex = EditorGUILayout.Popup(index, sources.ToArray(), GUILayout.Width(240));
            mSelectedSource = sources[newIndex];
            EditorGUILayout.EndHorizontal();
        }

        private bool MatchesFilter(CustomConsoleEntry aEntry)
        {
            if (!mLevelEnabled.TryGetValue(aEntry.Level, out var levelOn) || !levelOn)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(aEntry.Category) &&
                mCategoryEnabled.TryGetValue(aEntry.Category, out var categoryOn) && !categoryOn)
            {
                return false;
            }

            if (mSelectedSource != AllSourceLabel && aEntry.SourceLabel != mSelectedSource)
            {
                return false;
            }

            if (string.IsNullOrEmpty(mSearchText))
            {
                return true;
            }

            if (mUseRegex)
            {
                var regex = GetCompiledRegex();
                return regex != null && regex.IsMatch(aEntry.RawMessage);
            }

            return aEntry.RawMessage.IndexOf(mSearchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private Regex GetCompiledRegex()
        {
            if (mCompiledPattern == mSearchText)
            {
                return mCompiledRegex;
            }

            mCompiledPattern = mSearchText;
            try
            {
                mCompiledRegex = new Regex(mSearchText, RegexOptions.IgnoreCase);
                mRegexError = false;
            }
            catch (ArgumentException)
            {
                mCompiledRegex = null;
                mRegexError = true;
            }

            return mCompiledRegex;
        }

        private void DrawList(List<CustomConsoleEntry> aFiltered)
        {
            if (mAutoScroll && !mPaused)
            {
                mListScrollPosition.y = float.MaxValue;
            }

            mListScrollPosition = EditorGUILayout.BeginScrollView(mListScrollPosition, GUILayout.ExpandHeight(true));
            foreach (var entry in aFiltered)
            {
                DrawRow(entry);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.LabelField($"{aFiltered.Count} / {(mPaused && mSnapshot != null ? mSnapshot.Count : CustomConsoleLogStore.Entries.Count)} logs", EditorStyles.miniLabel);
        }

        private void DrawRow(CustomConsoleEntry aEntry)
        {
            var isSelected = mSelectedEntry == aEntry;
            var rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(20));
            if (isSelected)
            {
                EditorGUI.DrawRect(rect, new Color(0.24f, 0.48f, 0.90f, 0.35f));
            }

            GUILayout.Label(aEntry.Timestamp.ToString("HH:mm:ss.fff"), EditorStyles.miniLabel, GUILayout.Width(72));
            GUILayout.Label(aEntry.Level.ToString(), LevelStyle(aEntry.Level), GUILayout.Width(60));
            GUILayout.Label(string.IsNullOrEmpty(aEntry.Category) ? "-" : aEntry.Category, EditorStyles.miniLabel, GUILayout.Width(80));
            GUILayout.Label(aEntry.SourceLabel, EditorStyles.miniLabel, GUILayout.Width(150));
            GUILayout.Label(aEntry.Message, EditorStyles.label);

            EditorGUILayout.EndHorizontal();

            if (Event.current.type != EventType.MouseDown || !rect.Contains(Event.current.mousePosition))
            {
                return;
            }

            mSelectedEntry = aEntry;

            if (aEntry.Context != null)
            {
                Selection.activeObject = aEntry.Context;
                EditorGUIUtility.PingObject(aEntry.Context);
            }

            if (Event.current.clickCount == 2 && aEntry.HasScriptLocation)
            {
                UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(aEntry.ScriptPath, aEntry.ScriptLine);
            }

            Event.current.Use();
            Repaint();
        }

        private void DrawDetail()
        {
            if (mSelectedEntry == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Detail", EditorStyles.boldLabel);

            mDetailScrollPosition = EditorGUILayout.BeginScrollView(mDetailScrollPosition, GUILayout.Height(140));
            EditorGUILayout.SelectableLabel(mSelectedEntry.RawMessage, EditorStyles.wordWrappedLabel, GUILayout.Height(36));
            EditorGUILayout.SelectableLabel(mSelectedEntry.StackTrace, EditorStyles.wordWrappedLabel, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!mSelectedEntry.HasScriptLocation))
            {
                if (GUILayout.Button("Open Script", GUILayout.Width(100)))
                {
                    UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(mSelectedEntry.ScriptPath, mSelectedEntry.ScriptLine);
                }
            }
            using (new EditorGUI.DisabledScope(mSelectedEntry.Context == null))
            {
                if (GUILayout.Button("Ping Object", GUILayout.Width(100)))
                {
                    Selection.activeObject = mSelectedEntry.Context;
                    EditorGUIUtility.PingObject(mSelectedEntry.Context);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private static readonly Dictionary<CustomLogLevel, GUIStyle> sLevelStyles = new();

        private static GUIStyle LevelStyle(CustomLogLevel aLevel)
        {
            if (sLevelStyles.TryGetValue(aLevel, out var cached))
            {
                return cached;
            }

            var style = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };
            style.normal.textColor = aLevel switch
            {
                CustomLogLevel.Verbose => Color.gray,
                CustomLogLevel.Warning => new Color(0.85f, 0.65f, 0.13f),
                CustomLogLevel.Error => new Color(0.80f, 0.25f, 0.25f),
                CustomLogLevel.Critical => new Color(0.85f, 0.10f, 0.55f),
                _ => style.normal.textColor,
            };

            sLevelStyles[aLevel] = style;
            return style;
        }
    }
}
