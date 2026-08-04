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
        private const float RowHeight = 20f;
        private const float ScrollBarWidth = 16f;

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

        // ログ本体・フィルタ条件の変化を検知するためのdirtyフラグ。
        // スクロール操作等の毎イベントで重い再計算(LINQ走査)を走らせないために使う
        private bool mEntriesDirty = true;
        private bool mFilterDirty = true;

        private List<CustomConsoleEntry> mCachedFiltered = new();
        private List<string> mCachedCategories = new();
        private List<string> mCachedSources = new();
        private string[] mCachedSourceOptions = { AllSourceLabel };
        private readonly Dictionary<CustomLogLevel, int> mCachedLevelCounts = new();

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
            mEntriesDirty = true;
            if (!mPaused)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            var source = (mPaused && mSnapshot != null) ? mSnapshot : CustomConsoleLogStore.Entries;

            DrawToolbar();

            // 重い再計算(RebuildCacheIfNeeded)はLayout/Repaintイベントの時だけ行う。
            // MouseDrag(スクロールバードラッグ)等の入力イベントの発火頻度に比例して再計算しないようにするため
            var eventType = Event.current.type;
            if (eventType == EventType.Layout || eventType == EventType.Repaint)
            {
                RebuildCacheIfNeeded(source);
            }

            DrawLevelFilters();
            DrawCategoryFilters();
            DrawSourceFilter();

            if (mUseRegex && mRegexError)
            {
                EditorGUILayout.HelpBox("正規表現が不正です。", MessageType.Warning);
            }

            DrawList(mCachedFiltered);
            DrawDetail();
        }

        // エントリの増減・Pause切替・フィルタ条件の変化があった時だけ、
        // 絞り込み結果/カテゴリ一覧/送信元一覧/レベル別件数を再計算してキャッシュする
        private void RebuildCacheIfNeeded(IReadOnlyList<CustomConsoleEntry> aSource)
        {
            if (mEntriesDirty)
            {
                RebuildEntryDerivedCache(aSource);
            }

            if (mEntriesDirty || mFilterDirty)
            {
                mCachedFiltered = aSource.Where(MatchesFilter).ToList();
            }

            mEntriesDirty = false;
            mFilterDirty = false;
        }

        private void RebuildEntryDerivedCache(IReadOnlyList<CustomConsoleEntry> aSource)
        {
            mCachedCategories = aSource
                .Select(aEntry => aEntry.Category)
                .Where(aCategory => !string.IsNullOrEmpty(aCategory))
                .Distinct()
                .OrderBy(aCategory => aCategory)
                .ToList();

            foreach (var category in mCachedCategories)
            {
                if (!mCategoryEnabled.ContainsKey(category))
                {
                    mCategoryEnabled[category] = true;
                }
            }

            mCachedSources = aSource
                .Select(aEntry => aEntry.SourceLabel)
                .Where(aLabel => !string.IsNullOrEmpty(aLabel))
                .Distinct()
                .OrderBy(aLabel => aLabel)
                .ToList();

            var sourceOptions = new List<string> { AllSourceLabel };
            sourceOptions.AddRange(mCachedSources);
            mCachedSourceOptions = sourceOptions.ToArray();

            if (!mCachedSources.Contains(mSelectedSource) && mSelectedSource != AllSourceLabel)
            {
                mSelectedSource = AllSourceLabel;
            }

            mCachedLevelCounts.Clear();
            foreach (var level in sLevels)
            {
                mCachedLevelCounts[level] = aSource.Count(aEntry => aEntry.Level == level);
            }
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
                mEntriesDirty = true;
            }

            mAutoScroll = GUILayout.Toggle(mAutoScroll, "Auto Scroll", EditorStyles.toolbarButton, GUILayout.Width(80));

            var clearOnPlay = GUILayout.Toggle(CustomConsoleLogStore.ClearOnPlay, "Clear On Play", EditorStyles.toolbarButton, GUILayout.Width(90));
            if (clearOnPlay != CustomConsoleLogStore.ClearOnPlay)
            {
                CustomConsoleLogStore.ClearOnPlay = clearOnPlay;
            }

            GUILayout.FlexibleSpace();

            var newUseRegex = GUILayout.Toggle(mUseRegex, "Regex", EditorStyles.toolbarButton, GUILayout.Width(50));
            if (newUseRegex != mUseRegex)
            {
                mUseRegex = newUseRegex;
                mFilterDirty = true;
            }

            var newSearchText = EditorGUILayout.TextField(mSearchText, EditorStyles.toolbarSearchField, GUILayout.MinWidth(160));
            if (newSearchText != mSearchText)
            {
                mSearchText = newSearchText;
                mFilterDirty = true;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawLevelFilters()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            foreach (var level in sLevels)
            {
                var count = mCachedLevelCounts.TryGetValue(level, out var cachedCount) ? cachedCount : 0;
                var newValue = GUILayout.Toggle(mLevelEnabled[level], $"{level} ({count})", EditorStyles.toolbarButton);
                if (newValue != mLevelEnabled[level])
                {
                    mLevelEnabled[level] = newValue;
                    mFilterDirty = true;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCategoryFilters()
        {
            if (mCachedCategories.Count == 0)
            {
                return;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Tags:", GUILayout.Width(35));

            if (GUILayout.Button("All", EditorStyles.miniButtonLeft, GUILayout.Width(35)))
            {
                foreach (var category in mCachedCategories)
                {
                    mCategoryEnabled[category] = true;
                }
                mFilterDirty = true;
            }
            if (GUILayout.Button("None", EditorStyles.miniButtonRight, GUILayout.Width(45)))
            {
                foreach (var category in mCachedCategories)
                {
                    mCategoryEnabled[category] = false;
                }
                mFilterDirty = true;
            }

            foreach (var category in mCachedCategories)
            {
                var width = Mathf.Clamp(category.Length * 8 + 20, 40, 160);
                var newValue = GUILayout.Toggle(mCategoryEnabled[category], category, EditorStyles.miniButton, GUILayout.Width(width));
                if (newValue != mCategoryEnabled[category])
                {
                    mCategoryEnabled[category] = newValue;
                    mFilterDirty = true;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSourceFilter()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Source:", GUILayout.Width(50));
            var index = Array.IndexOf(mCachedSourceOptions, mSelectedSource);
            if (index < 0)
            {
                index = 0;
            }
            var newIndex = EditorGUILayout.Popup(index, mCachedSourceOptions, GUILayout.Width(240));
            var newSelected = mCachedSourceOptions[newIndex];
            if (newSelected != mSelectedSource)
            {
                mSelectedSource = newSelected;
                mFilterDirty = true;
            }
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

        // 表示範囲(スクロール位置から見える行)だけをDrawRowで描画する。
        // ログ件数が多い場合でも、スクロール操作1回あたりの描画コストを可視行数分に抑えるための仮想化
        private void DrawList(List<CustomConsoleEntry> aFiltered)
        {
            if (mAutoScroll && !mPaused)
            {
                mListScrollPosition.y = float.MaxValue;
            }

            var viewportRect = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            var contentHeight = aFiltered.Count * RowHeight;
            var contentRect = new Rect(0, 0, Mathf.Max(viewportRect.width - ScrollBarWidth, 0), contentHeight);

            mListScrollPosition = GUI.BeginScrollView(viewportRect, mListScrollPosition, contentRect);

            if (aFiltered.Count > 0)
            {
                var firstIndex = Mathf.Clamp(Mathf.FloorToInt(mListScrollPosition.y / RowHeight), 0, aFiltered.Count - 1);
                var visibleCount = Mathf.CeilToInt(viewportRect.height / RowHeight) + 1;
                var lastIndex = Mathf.Min(aFiltered.Count - 1, firstIndex + visibleCount);

                for (var i = firstIndex; i <= lastIndex; i++)
                {
                    var rowRect = new Rect(0, i * RowHeight, contentRect.width, RowHeight);
                    DrawRow(aFiltered[i], rowRect);
                }
            }

            GUI.EndScrollView();

            EditorGUILayout.LabelField($"{aFiltered.Count} / {(mPaused && mSnapshot != null ? mSnapshot.Count : CustomConsoleLogStore.Entries.Count)} logs", EditorStyles.miniLabel);
        }

        private void DrawRow(CustomConsoleEntry aEntry, Rect aRowRect)
        {
            var isSelected = mSelectedEntry == aEntry;
            if (isSelected)
            {
                EditorGUI.DrawRect(aRowRect, new Color(0.24f, 0.48f, 0.90f, 0.35f));
            }

            var x = aRowRect.x;
            var timestampRect = new Rect(x, aRowRect.y, 72, aRowRect.height);
            x += 72;
            var levelRect = new Rect(x, aRowRect.y, 60, aRowRect.height);
            x += 60;
            var categoryRect = new Rect(x, aRowRect.y, 80, aRowRect.height);
            x += 80;
            var sourceRect = new Rect(x, aRowRect.y, 150, aRowRect.height);
            x += 150;
            var messageRect = new Rect(x, aRowRect.y, Mathf.Max(0, aRowRect.width - (x - aRowRect.x)), aRowRect.height);

            GUI.Label(timestampRect, aEntry.Timestamp.ToString("HH:mm:ss.fff"), EditorStyles.miniLabel);
            GUI.Label(levelRect, aEntry.Level.ToString(), LevelStyle(aEntry.Level));
            GUI.Label(categoryRect, string.IsNullOrEmpty(aEntry.Category) ? "-" : aEntry.Category, EditorStyles.miniLabel);
            GUI.Label(sourceRect, aEntry.SourceLabel, EditorStyles.miniLabel);
            GUI.Label(messageRect, aEntry.Message, EditorStyles.label);

            if (Event.current.type != EventType.MouseDown || !aRowRect.Contains(Event.current.mousePosition))
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
