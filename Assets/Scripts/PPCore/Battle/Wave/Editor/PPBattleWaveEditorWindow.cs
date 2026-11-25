/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleWaveEditorWindow.cs
 * @author hqrse
 * @date 2026/11/25
 * @brief バトル定義のウェーブをノード図で組むウィンドウ。Play していない間は編集モード、Play 中は表示モード
 * =====================================*/

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PPCore
{
    // ウェーブのノードエディタ。ノード図・右の欄・点検の一覧を並べ、Undo とウィンドウの外での変更に追従させる
    // Play 中はバトル定義を書き換えないよう、表示だけにする(実行中の情報は後から重ねる)
    public class PPBattleWaveEditorWindow : EditorWindow
    {
        /* 定数 */

        private const string LastTargetKey = "PPCore.WaveEditor.LastTargetGuid";
        private const float PanelWidth = 320f;
        private static readonly Vector2 MinWindowSize = new(720f, 420f);

        /* static 変数 */

        // 開いているウィンドウ。開いているバトル定義の警告を止めるのに使う
        private static readonly HashSet<PPBattleWaveEditorWindow> sOpenWindows = new();

        /* 状態 */

        // 編集の対象。ドメインを読み直しても保つ
        [SerializeField] private PPBattleDefinition mTarget;
        private SerializedObject mSerializedObject;
        private PPWaveGraphView mGraphView;
        private PPWaveNodeInspectorPanel mInspectorPanel;
        private PPWaveProblemListView mProblemList;
        private HelpBox mPlayModeBox;
        private readonly List<PPWaveProblem> mProblems = new();
        private string mSelectedNodeId;

        /* プロパティ */

        public PPBattleDefinition Target => mTarget;
        // ノード図。対象が無ければ null
        public PPWaveGraphView GraphView => mGraphView;
        public bool IsEditMode => mGraphView != null && mGraphView.IsEditable;
        // 最後に点検したときの誤り
        public IReadOnlyList<PPWaveProblem> Problems => mProblems;

        /* static 関数 */

        [MenuItem("Window/Project-Pudding/ウェーブのノードエディタ")]
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
            Undo.undoRedoEvent += HandleUndoRedo;
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
            if (mTarget == null) mTarget = LoadLastTarget();
            BuildUI();
        }

        private void OnDisable()
        {
            sOpenWindows.Remove(this);
            Undo.undoRedoEvent -= HandleUndoRedo;
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            SaveLastTarget();
        }

        /* 操作 */

        // 編集の対象を切り替え、画面を組み直す
        public void SetTarget(PPBattleDefinition aTarget)
        {
            if (mTarget == aTarget && mGraphView != null) return;
            mTarget = aTarget;
            mSelectedNodeId = null;
            SaveLastTarget();
            BuildUI();
        }

        // バトル定義を点検し直し、ノード図の印と一覧に出す
        public void RefreshProblems()
        {
            mProblems.Clear();
            if (mTarget != null) mTarget.Wave.Validate(mProblems);
            mGraphView?.SetProblems(mProblems);
            mProblemList?.SetProblems(mProblems);
        }

        /* 表示 */

        private void BuildUI()
        {
            rootVisualElement.Clear();
            mGraphView = null;
            mInspectorPanel = null;
            mProblemList = null;
            mSerializedObject = null;
            mProblems.Clear();

            rootVisualElement.Add(BuildToolbar());
            mPlayModeBox = new HelpBox("Play 中は編集できません(表示だけです)。Play を止めると編集できます", HelpBoxMessageType.Info);
            rootVisualElement.Add(mPlayModeBox);

            if (mTarget == null)
            {
                mPlayModeBox.style.display = DisplayStyle.None;
                rootVisualElement.Add(new Label("「対象」でバトル定義を選ぶか、バトル定義のインスペクタの「ノードエディタで開く」で開いてください")
                {
                    style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleCenter, whiteSpace = WhiteSpace.Normal },
                });
                return;
            }

            mSerializedObject = new SerializedObject(mTarget);
            mGraphView = new PPWaveGraphView(mSerializedObject, mTarget) { style = { flexGrow = 1 } };
            mGraphView.OnNodeSelected += HandleNodeSelected;
            mGraphView.OnEdited += HandleGraphEdited;
            mInspectorPanel = new PPWaveNodeInspectorPanel();
            mProblemList = new PPWaveProblemListView();
            mProblemList.OnProblemSelected += aId => mGraphView?.SelectNode(aId, true);

            var split = new TwoPaneSplitView(1, PanelWidth, TwoPaneSplitViewOrientation.Horizontal) { style = { flexGrow = 1 } };
            split.Add(mGraphView);
            split.Add(mInspectorPanel);
            // ウィンドウの外での変更(インスペクタなど)を見張る。組み直すたびに作るこの要素に付け、古い見張りを残さない
            var body = new VisualElement { style = { flexGrow = 1 } };
            body.Add(split);
            body.Add(mProblemList);
            body.TrackSerializedObjectValue(mSerializedObject, HandleSerializedObjectChanged);
            rootVisualElement.Add(body);

            ApplyMode();
            RefreshProblems();
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new Toolbar();
            var targetField = new ObjectField("対象") { objectType = typeof(PPBattleDefinition), allowSceneObjects = false, value = mTarget };
            targetField.style.flexGrow = 1;
            targetField.RegisterValueChangedCallback(e => SetTarget(e.newValue as PPBattleDefinition));
            toolbar.Add(targetField);
            toolbar.Add(new ToolbarButton(() => mGraphView?.FrameAll()) { text = "全体を表示" });
            return toolbar;
        }

        // Play 中(と Play に入る途中)は表示モード、それ以外は編集モードにする
        private void ApplyMode()
        {
            bool isEditable = !EditorApplication.isPlayingOrWillChangePlaymode;
            mGraphView?.SetEditable(isEditable);
            mInspectorPanel?.SetEnabled(isEditable);
            if (mPlayModeBox != null) mPlayModeBox.style.display = isEditable || mTarget == null ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /* イベントハンドラ */

        private void HandleNodeSelected(string aId)
        {
            mSelectedNodeId = aId;
            mInspectorPanel?.SetTarget(mSerializedObject, aId);
        }

        // ノード図での編集のあと。ノードの並びが変わったら、右の欄を出し直す(項目の場所がずれるため)
        private void HandleGraphEdited(bool aIsNodeChanged)
        {
            if (aIsNodeChanged) mInspectorPanel?.SetTarget(mSerializedObject, mSelectedNodeId);
            RefreshProblems();
        }

        // 値が変わったとき。ノード図と食い違えば組み直し、そうでなければ見出しと要約だけを直す
        // ノードの並べ替えでは図は変わらないが、右の欄は番号で結び付いているため、番号が変わったら出し直す
        private void HandleSerializedObjectChanged(SerializedObject aSerializedObject)
        {
            if (mGraphView == null || mTarget == null) return;
            mSerializedObject.Update();
            if (!mGraphView.IsInSync())
            {
                mGraphView.Rebuild();
                mInspectorPanel?.Refresh();
            }
            else
            {
                mGraphView.RefreshAllNodes();
                if (mInspectorPanel != null && mInspectorPanel.IsTargetMoved()) mInspectorPanel.Refresh();
            }
            RefreshProblems();
        }

        // Undo / Redo で戻った内容に、ノード図と右の欄を組み直す
        private void HandleUndoRedo(in UndoRedoInfo aInfo)
        {
            if (mGraphView == null || mTarget == null) return;
            mSerializedObject.Update();
            mGraphView.Rebuild();
            mInspectorPanel?.SetTarget(mSerializedObject, mSelectedNodeId);
            RefreshProblems();
        }

        private void HandlePlayModeChanged(PlayModeStateChange aState) => ApplyMode();

        /* ヘルパー */

        private static PPBattleWaveEditorWindow GetOrCreate()
        {
            var window = GetWindow<PPBattleWaveEditorWindow>();
            window.titleContent = new GUIContent("ウェーブ");
            window.minSize = MinWindowSize;
            return window;
        }

        // ノードエディタで開いているバトル定義か
        private static bool IsOpenInWindow(PPBattleDefinition aDefinition)
        {
            foreach (var window in sOpenWindows)
            {
                if (window != null && window.mTarget == aDefinition) return true;
            }
            return false;
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
