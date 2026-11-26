/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleWaveEditorWindow.cs
 * @author hqrse
 * @date 2026/11/25
 * @brief バトル定義のウェーブを、段階の一覧・出す敵の表・見るだけの流れの図で編集するウィンドウ。Play 中は表示だけ
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace PPCore
{
    // ウェーブエディタ。上の帯(目安と誤り)・左の段階の一覧・中央の段階の中身・下の流れの図を並べる
    // 表示は毎回バトル定義から描き直すため、Undo やウィンドウの外での変更にそのまま追従する
    public class PPBattleWaveEditorWindow : EditorWindow
    {
        /* 定数 */

        private const string LastTargetKey = "PPCore.WaveEditor.LastTargetGuid";
        private const string StagesPath = "mWave.mStages";
        private const float ListWidth = 230f;
        private const float StageRowHeight = 40f;
        private const float FlowHeight = 130f;
        private const float DefaultSeconds = 20f;
        private const float DefaultPercent = 50f;
        private static readonly Vector2 MinWindowSize = new(820f, 560f);

        /* static 変数 */

        // 開いているウィンドウ。開いているバトル定義の警告を止めるのに使う
        private static readonly HashSet<PPBattleWaveEditorWindow> sOpenWindows = new();

        /* 状態 */

        // 編集の対象。ドメインを読み直しても保つ
        [SerializeField] private PPBattleDefinition mTarget;
        // 選んでいる段階の識別子。並べ替えや Undo で番号が変わっても同じ段階を選び続ける
        [SerializeField] private string mSelectedStageId;
        [SerializeField] private bool mShowsProblems = true;
        private SerializedObject mSerializedObject;
        private ReorderableList mStageList;
        private readonly List<PPWaveProblem> mProblems = new();
        private readonly HashSet<string> mProblemStageIds = new();
        private Vector2 mListScroll;
        private Vector2 mCenterScroll;
        private Vector2 mProblemScroll;
        private int mLastDirtyCount = -1;

        /* プロパティ */

        public PPBattleDefinition Target => mTarget;
        // Play 中(と Play に入る途中)は編集できない
        public bool IsEditable => mTarget != null && !EditorApplication.isPlayingOrWillChangePlaymode;
        // 選んでいる段階の番号(0 から)。選んでいなければ -1
        public int SelectedIndex => mTarget != null ? mTarget.Wave.IndexOf(mSelectedStageId) : -1;
        public string SelectedStageId => mSelectedStageId;
        // 最後に点検したときの誤り
        public IReadOnlyList<PPWaveProblem> Problems => mProblems;

        /* static 関数 */

        [MenuItem("Window/Project-Pudding/ウェーブエディタ")]
        public static void ShowWindow() => GetOrCreate().Show();

        // バトル定義を対象にしてウィンドウを開く
        public static PPBattleWaveEditorWindow Open(PPBattleDefinition aTarget)
        {
            var window = GetOrCreate();
            window.SetTarget(aTarget);
            window.Show();
            window.Focus();
            return window;
        }

        /* Unity イベント */

        private void OnEnable()
        {
            sOpenWindows.Add(this);
            PPBattleDefinition.IsWaveWarningSuppressed = IsOpenInWindow;
            Undo.undoRedoPerformed += HandleUndoRedo;
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
            if (mTarget == null) mTarget = LoadLastTarget();
        }

        private void OnDisable()
        {
            sOpenWindows.Remove(this);
            Undo.undoRedoPerformed -= HandleUndoRedo;
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            SaveLastTarget();
        }

        // ウィンドウの外(インスペクタなど)での変更を、変更の回数で見つけて描き直す
        private void OnInspectorUpdate()
        {
            int dirtyCount = mTarget != null ? EditorUtility.GetDirtyCount(mTarget) : -1;
            if (dirtyCount == mLastDirtyCount) return;
            mLastDirtyCount = dirtyCount;
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();
            if (mTarget == null)
            {
                EditorGUILayout.HelpBox("「対象」でバトル定義を選ぶか、バトル定義のインスペクタの「ウェーブエディタで開く」で開いてください", MessageType.Info);
                return;
            }
            if (!PrepareSerializedObject()) return;
            mSerializedObject.Update();
            if (Event.current.type == EventType.Layout) RefreshProblems();

            if (!IsEditable) EditorGUILayout.HelpBox("Play 中は編集できません(表示だけです)。Play を止めると編集できます", MessageType.Info);
            DrawProblems();

            var stages = mSerializedObject.FindProperty(StagesPath);
            EnsureSelection(stages);
            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
            {
                DrawStageList(stages);
                DrawCenter(stages);
            }
            DrawFlow();

            mSerializedObject.ApplyModifiedProperties();
        }

        /* 操作 */

        // 編集の対象を切り替え、先頭の段階を選ぶ
        public void SetTarget(PPBattleDefinition aTarget)
        {
            if (mTarget == aTarget) return;
            mTarget = aTarget;
            mSelectedStageId = aTarget != null && aTarget.Wave.Stages.Count > 0 ? aTarget.Wave.Stages[0].Id : null;
            mSerializedObject = null;
            mStageList = null;
            SaveLastTarget();
            Repaint();
        }

        // 番号(0 から)の段階をすぐに選ぶ。画面の操作からは RequestSelectStage を使う
        public void SelectStage(int aIndex)
        {
            if (mTarget == null || aIndex < 0 || aIndex >= mTarget.Wave.Stages.Count) return;
            mSelectedStageId = mTarget.Wave.Stages[aIndex].Id;
            Repaint();
        }

        // 入力中の数値の欄を今の段階へ確定させてから、次の更新で aAction を行う。画面の操作で選択や並びを変えるときに使う
        // 確定の命令は同じ回の描画のうちに同じ位置の欄へ届くため、先に選択や並びを変えると、確定した値が別の段階へ入る
        public void RunAfterCommit(Action aAction)
        {
            GUI.FocusControl(null);
            var target = mTarget;
            EditorApplication.delayCall += () =>
            {
                // 次の更新までにウィンドウが閉じられたか、対象が替わっていたら何もしない
                if (this == null || mTarget != target) return;
                aAction();
                Repaint();
            };
        }

        // 画面の操作で段階を選ぶ。入力中の数値を確定させてから選ぶ
        private void RequestSelectStage(string aStageId)
        {
            if (string.IsNullOrEmpty(aStageId) || aStageId == mSelectedStageId) return;
            RunAfterCommit(() =>
            {
                if (mTarget != null && mTarget.Wave.IndexOf(aStageId) >= 0) mSelectedStageId = aStageId;
            });
        }

        // 選んでいる段階の後ろに段階を足して選ぶ。選んでいる段階に行き先が無ければ、新しい段階への行き先も足す
        public bool AddStage()
        {
            if (!BeginEdit(out var stages)) return false;
            int selected = SelectedIndex;
            int index = selected >= 0 ? selected + 1 : stages.arraySize;
            string id = PPWaveStage.NewId();
            stages.InsertArrayElementAtIndex(index);
            var stage = stages.GetArrayElementAtIndex(index);
            stage.FindPropertyRelative("mId").stringValue = id;
            stage.FindPropertyRelative("mName").stringValue = $"段階 {stages.arraySize}";
            stage.FindPropertyRelative("mWaitsFromBattleStart").boolValue = stages.arraySize == 1;
            stage.FindPropertyRelative("mSpawns").ClearArray();
            stage.FindPropertyRelative("mNexts").ClearArray();
            var conditions = stage.FindPropertyRelative("mStartConditions");
            conditions.ClearArray();
            if (stages.arraySize > 1) InitializeCondition(AppendElement(conditions), PPWaveStartKind.Elapsed, null);
            if (selected >= 0)
            {
                var nexts = stages.GetArrayElementAtIndex(selected).FindPropertyRelative("mNexts");
                if (nexts.arraySize == 0) InitializeNext(AppendElement(nexts), id);
            }
            mSelectedStageId = id;
            EndEdit("段階を追加");
            return true;
        }

        // 選んでいる段階を、名前に「のコピー」を付けて後ろへ複製して選ぶ
        public bool DuplicateStage()
        {
            int selected = SelectedIndex;
            if (selected < 0 || !BeginEdit(out var stages)) return false;
            stages.InsertArrayElementAtIndex(selected);
            var copy = stages.GetArrayElementAtIndex(selected + 1);
            string id = PPWaveStage.NewId();
            copy.FindPropertyRelative("mId").stringValue = id;
            var name = copy.FindPropertyRelative("mName");
            name.stringValue = $"{name.stringValue} のコピー";
            mSelectedStageId = id;
            EndEdit("段階を複製");
            return true;
        }

        // 選んでいる段階を消す。その段階への行き先と、その段階の全滅を待つ条件も一緒に消す
        public bool DeleteStage()
        {
            int selected = SelectedIndex;
            if (selected < 0 || !BeginEdit(out var stages)) return false;
            string id = mSelectedStageId;
            stages.DeleteArrayElementAtIndex(selected);
            for (int i = 0; i < stages.arraySize; i++)
            {
                var stage = stages.GetArrayElementAtIndex(i);
                RemoveWhere(stage.FindPropertyRelative("mNexts"), aNext => aNext.FindPropertyRelative("mTargetId").stringValue == id);
                RemoveWhere(stage.FindPropertyRelative("mStartConditions"), aCondition =>
                    aCondition.FindPropertyRelative("mKind").enumValueIndex == (int)PPWaveStartKind.StageWiped && aCondition.FindPropertyRelative("mStageId").stringValue == id);
            }
            int next = Mathf.Min(selected, stages.arraySize - 1);
            mSelectedStageId = next >= 0 ? stages.GetArrayElementAtIndex(next).FindPropertyRelative("mId").stringValue : null;
            EndEdit("段階を削除");
            return true;
        }

        // 段階の並びを変える(一覧のドラッグと同じ)
        public bool MoveStage(int aFrom, int aTo)
        {
            if (!BeginEdit(out var stages) || aFrom < 0 || aFrom >= stages.arraySize || aTo < 0 || aTo >= stages.arraySize) return false;
            stages.MoveArrayElement(aFrom, aTo);
            EndEdit("段階を並べ替え");
            return true;
        }

        // 選んでいる段階の出す敵に行を足す。行があれば最後の行を写す
        public bool AddSpawnRow()
        {
            if (!BeginEditSelected(out var stage)) return false;
            var spawns = stage.FindPropertyRelative("mSpawns");
            var row = AppendElement(spawns);
            if (spawns.arraySize == 1)
            {
                row.FindPropertyRelative("mUnit").objectReferenceValue = null;
                row.FindPropertyRelative("mCount").intValue = 5;
                row.FindPropertyRelative("mInterval").floatValue = 3f;
                row.FindPropertyRelative("mFirstDelay").floatValue = 0f;
            }
            EndEdit("敵を足す");
            return true;
        }

        public bool RemoveSpawnRow(int aRow) => RemoveSelectedElement("mSpawns", aRow, "敵を消す");

        // 選んでいる段階に始まる条件を足す。全滅の対象は 1 つ前の段階にする
        public bool AddCondition(PPWaveStartKind aKind)
        {
            int selected = SelectedIndex;
            if (!BeginEditSelected(out var stage)) return false;
            string previousId = selected > 0 ? mTarget.Wave.Stages[selected - 1].Id : null;
            InitializeCondition(AppendElement(stage.FindPropertyRelative("mStartConditions")), aKind, previousId);
            EndEdit("条件を足す");
            return true;
        }

        public bool RemoveCondition(int aIndex) => RemoveSelectedElement("mStartConditions", aIndex, "条件を消す");

        // 選んでいる段階に行き先を足す。行き先は 1 つ後の段階(最後なら自分)にする
        public bool AddNext()
        {
            int selected = SelectedIndex;
            if (!BeginEditSelected(out var stage)) return false;
            var wave = mTarget.Wave;
            string targetId = wave.Stages[Mathf.Min(selected + 1, wave.Stages.Count - 1)].Id;
            InitializeNext(AppendElement(stage.FindPropertyRelative("mNexts")), targetId);
            EndEdit("行き先を足す");
            return true;
        }

        public bool RemoveNext(int aIndex) => RemoveSelectedElement("mNexts", aIndex, "行き先を消す");

        private bool RemoveSelectedElement(string aListName, int aIndex, string aUndoName)
        {
            if (!BeginEditSelected(out var stage)) return false;
            var list = stage.FindPropertyRelative(aListName);
            if (aIndex < 0 || aIndex >= list.arraySize) return false;
            list.DeleteArrayElementAtIndex(aIndex);
            EndEdit(aUndoName);
            return true;
        }

        // 編集を始める。前の入力を確定させてから最新の値を読み、この操作を Undo 1 回にまとめる
        private bool BeginEdit(out SerializedProperty aStages)
        {
            aStages = null;
            if (!IsEditable || !PrepareSerializedObject()) return false;
            mSerializedObject.ApplyModifiedProperties();
            mSerializedObject.Update();
            Undo.IncrementCurrentGroup();
            aStages = mSerializedObject.FindProperty(StagesPath);
            return true;
        }

        private bool BeginEditSelected(out SerializedProperty aStage)
        {
            aStage = null;
            int selected = SelectedIndex;
            if (selected < 0 || !BeginEdit(out var stages) || selected >= stages.arraySize) return false;
            aStage = stages.GetArrayElementAtIndex(selected);
            return true;
        }

        private void EndEdit(string aUndoName)
        {
            mSerializedObject.ApplyModifiedProperties();
            Undo.SetCurrentGroupName(aUndoName);
            Repaint();
        }

        /* 描画 */

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("対象", GUILayout.Width(30f));
                var target = (PPBattleDefinition)EditorGUILayout.ObjectField(mTarget, typeof(PPBattleDefinition), false, GUILayout.Width(260f));
                if (target != mTarget) SetTarget(target);
                GUILayout.FlexibleSpace();
                if (mTarget == null) return;

                var wave = mTarget.Wave;
                DrawPill($"敵 {wave.CountFiniteEnemies()} 体", "各段階を 1 回ずつ出したときの敵の数(数が無限の行は数えない)", PPWaveEditorStyles.TextColor);
                DrawPill($"長さ 約 {Mathf.RoundToInt(wave.EstimateLength())} 秒", "時間の条件だけで進んだとして、各段階を最初に 1 度出し終えるまでの秒数(全滅・被弾・HP でしか始まらない段階は数えない)", PPWaveEditorStyles.TextColor);
                DrawPill(wave.HasInfinite() ? "無限 あり" : "無限 なし", "数が 0(無限)の敵を出す段階があるか", PPWaveEditorStyles.TextColor);
                bool hasProblem = mProblems.Count > 0;
                if (DrawPill(hasProblem ? $"! 誤り {mProblems.Count}" : "✓ 誤り 0", "押すと誤りの一覧を出し入れします", hasProblem ? PPWaveEditorStyles.ErrorColor : PPWaveEditorStyles.OkColor)) mShowsProblems = !mShowsProblems;
            }
        }

        // 帯の札を 1 つ描く。押されたら true
        private static bool DrawPill(string aText, string aTooltip, Color aColor)
        {
            var content = new GUIContent(aText, aTooltip);
            var style = PPWaveEditorStyles.Pill;
            style.normal.textColor = aColor;
            style.hover.textColor = aColor;
            return GUILayout.Button(content, style, GUILayout.Width(style.CalcSize(content).x + 4f));
        }

        private void DrawProblems()
        {
            if (!mShowsProblems || mProblems.Count == 0) return;
            mProblemScroll = EditorGUILayout.BeginScrollView(mProblemScroll, GUILayout.MaxHeight(80f));
            foreach (var problem in mProblems)
            {
                if (GUILayout.Button(problem.Message, PPWaveEditorStyles.ProblemRow)) RequestSelectStage(problem.StageId);
            }
            EditorGUILayout.EndScrollView();
        }

        // 左の段階の一覧。ドラッグで並べ替え、下に追加・複製・削除
        private void DrawStageList(SerializedProperty aStages)
        {
            using (new EditorGUILayout.VerticalScope(PPWaveEditorStyles.ListPane, GUILayout.Width(ListWidth), GUILayout.ExpandHeight(true)))
            {
                if (mStageList == null || mStageList.serializedProperty.propertyPath != aStages.propertyPath) mStageList = CreateStageList(aStages);
                mStageList.serializedProperty = aStages;
                mStageList.draggable = IsEditable;
                mStageList.index = SelectedIndex;
                mListScroll = EditorGUILayout.BeginScrollView(mListScroll);
                mStageList.DoLayoutList();
                EditorGUILayout.EndScrollView();

                using (new EditorGUI.DisabledScope(!IsEditable))
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+ 追加")) RunAfterCommit(() => AddStage());
                    using (new EditorGUI.DisabledScope(SelectedIndex < 0))
                    {
                        if (GUILayout.Button("複製")) RunAfterCommit(() => DuplicateStage());
                        if (GUILayout.Button("削除")) RunAfterCommit(() => DeleteStage());
                    }
                }
                GUILayout.Label("ドラッグで並べ替え", EditorStyles.miniLabel);
            }
        }

        private ReorderableList CreateStageList(SerializedProperty aStages)
        {
            var list = new ReorderableList(mSerializedObject, aStages, true, false, false, false)
            {
                elementHeight = StageRowHeight,
                headerHeight = 0f,
                footerHeight = 0f,
                showDefaultBackground = false,
            };
            list.drawElementBackgroundCallback = (aRect, aIndex, aIsActive, aIsFocused) =>
            {
                if (Event.current.type != EventType.Repaint) return;
                var color = aIndex == SelectedIndex ? PPWaveEditorStyles.SelectedColor : PPWaveEditorStyles.ListRowColor;
                EditorGUI.DrawRect(aRect, color);
                EditorGUI.DrawRect(new Rect(aRect.x, aRect.yMax - 1f, aRect.width, 1f), PPWaveEditorStyles.BorderColor);
            };
            list.drawElementCallback = (aRect, aIndex, aIsActive, aIsFocused) => DrawStageRow(aRect, aIndex);
            // 一覧が持つ番号は次の描画で選択に合わせて戻すため、ここでは選択の予約だけをする
            list.onSelectCallback = aList =>
            {
                if (aList.index >= 0 && aList.index < mTarget.Wave.Stages.Count) RequestSelectStage(mTarget.Wave.Stages[aList.index].Id);
            };
            return list;
        }

        private void DrawStageRow(Rect aRect, int aIndex)
        {
            var wave = mTarget.Wave;
            if (aIndex >= wave.Stages.Count) return;
            var stage = wave.Stages[aIndex];
            var titleRect = new Rect(aRect.x + 2f, aRect.y + 3f, aRect.width - 20f, 18f);
            GUI.Label(titleRect, $"{aIndex + 1}   {wave.GetStageName(stage.Id)}", PPWaveEditorStyles.StageTitle);
            GUI.Label(new Rect(aRect.x + 2f, aRect.y + 20f, aRect.width - 4f, 16f), new GUIContent(wave.DescribeStart(stage), wave.DescribeStart(stage)), PPWaveEditorStyles.StageSummary);
            if (mProblemStageIds.Contains(stage.Id)) GUI.Label(new Rect(aRect.xMax - 16f, aRect.y + 3f, 16f, 18f), new GUIContent("!", "この段階に誤りがあります"), PPWaveEditorStyles.ErrorMark);
        }

        // 中央の欄。選んでいる段階の名前・目安・出す敵の表・始まる条件・行き先
        private void DrawCenter(SerializedProperty aStages)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                mCenterScroll = EditorGUILayout.BeginScrollView(mCenterScroll);
                int selected = SelectedIndex;
                if (selected < 0 || selected >= aStages.arraySize)
                {
                    EditorGUILayout.HelpBox(aStages.arraySize == 0 ? "段階がありません。左の「+ 追加」で段階を足してください" : "左の一覧で段階を選んでください", MessageType.Info);
                }
                else
                {
                    using (new EditorGUI.DisabledScope(!IsEditable))
                    {
                        PPWaveStageEditorGUI.Draw(this, mTarget.Wave, selected, aStages.GetArrayElementAtIndex(selected));
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawFlow()
        {
            var rect = GUILayoutUtility.GetRect(0f, FlowHeight, GUILayout.ExpandWidth(true));
            int clicked = PPWaveFlowDiagram.Draw(rect, mTarget.Wave, SelectedIndex);
            if (clicked >= 0) RequestSelectStage(mTarget.Wave.Stages[clicked].Id);
        }

        /* イベントハンドラ */

        private void HandleUndoRedo()
        {
            mSerializedObject?.Update();
            Repaint();
        }

        private void HandlePlayModeChanged(PlayModeStateChange aState) => Repaint();

        /* ヘルパー */

        private static PPBattleWaveEditorWindow GetOrCreate()
        {
            var window = GetWindow<PPBattleWaveEditorWindow>();
            window.titleContent = new GUIContent("ウェーブ");
            window.minSize = MinWindowSize;
            return window;
        }

        // ウェーブエディタで開いているバトル定義か
        private static bool IsOpenInWindow(PPBattleDefinition aDefinition)
        {
            foreach (var window in sOpenWindows)
            {
                if (window != null && window.mTarget == aDefinition) return true;
            }
            return false;
        }

        private bool PrepareSerializedObject()
        {
            if (mTarget == null) return false;
            if (mSerializedObject == null || mSerializedObject.targetObject != mTarget)
            {
                mSerializedObject = new SerializedObject(mTarget);
                mStageList = null;
            }
            return true;
        }

        private void RefreshProblems()
        {
            mProblems.Clear();
            mProblemStageIds.Clear();
            mTarget.Wave.Validate(mProblems);
            foreach (var problem in mProblems)
            {
                if (problem.StageId != null) mProblemStageIds.Add(problem.StageId);
            }
        }

        // 選んでいる段階が無くなっていたら、近い段階を選び直す
        private void EnsureSelection(SerializedProperty aStages)
        {
            if (SelectedIndex >= 0 || aStages.arraySize == 0 || mTarget.Wave.Stages.Count == 0) return;
            mSelectedStageId = mTarget.Wave.Stages[0].Id;
        }

        private static SerializedProperty AppendElement(SerializedProperty aList)
        {
            aList.InsertArrayElementAtIndex(aList.arraySize);
            return aList.GetArrayElementAtIndex(aList.arraySize - 1);
        }

        private static void RemoveWhere(SerializedProperty aList, System.Func<SerializedProperty, bool> aPredicate)
        {
            for (int i = aList.arraySize - 1; i >= 0; i--)
            {
                if (aPredicate(aList.GetArrayElementAtIndex(i))) aList.DeleteArrayElementAtIndex(i);
            }
        }

        private static void InitializeCondition(SerializedProperty aCondition, PPWaveStartKind aKind, string aStageId)
        {
            aCondition.FindPropertyRelative("mKind").enumValueIndex = (int)aKind;
            aCondition.FindPropertyRelative("mSeconds").floatValue = DefaultSeconds;
            aCondition.FindPropertyRelative("mStageId").stringValue = aStageId ?? string.Empty;
            aCondition.FindPropertyRelative("mSide").enumValueIndex = (int)BattleSide.Enemy;
            aCondition.FindPropertyRelative("mPercent").floatValue = DefaultPercent;
            aCondition.FindPropertyRelative("mIsOnce").boolValue = false;
        }

        private static void InitializeNext(SerializedProperty aNext, string aTargetId)
        {
            aNext.FindPropertyRelative("mTargetId").stringValue = aTargetId;
            aNext.FindPropertyRelative("mTiming").enumValueIndex = (int)PPWaveNextTiming.Started;
            aNext.FindPropertyRelative("mCondition").enumValueIndex = (int)PPWaveNextCondition.None;
            aNext.FindPropertyRelative("mSide").enumValueIndex = (int)BattleSide.Enemy;
            aNext.FindPropertyRelative("mPercent").floatValue = DefaultPercent;
        }

        private void SaveLastTarget()
        {
            string path = mTarget != null ? AssetDatabase.GetAssetPath(mTarget) : string.Empty;
            EditorPrefs.SetString(LastTargetKey, string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path));
        }

        // 最後に開いていたバトル定義。記録が無いか、消されていれば null
        private static PPBattleDefinition LoadLastTarget()
        {
            string guid = EditorPrefs.GetString(LastTargetKey, string.Empty);
            if (string.IsNullOrEmpty(guid)) return null;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<PPBattleDefinition>(path);
        }
    }
}
