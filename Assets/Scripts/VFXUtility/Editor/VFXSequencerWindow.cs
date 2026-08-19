/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequencerWindow.cs
 * @author hqrse
 * @date 2026/08/18
 * @brief VFXSequenceDefinitionをノードグラフとして視覚的に編集する専用ウィンドウ。埋め込みプレビューも提供する
 * =====================================*/

using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace VFXUtility.Editor
{
    public class VFXSequencerWindow : EditorWindow
    {
        // レイアウト・カメラ・グリッド表示状態を保存するEditorPrefsキー(ウィンドウを閉じても復元される)
        private const string PrefKeyInspectorWidth = "VFXUtility.Sequencer.InspectorWidth";
        private const string PrefKeyPreviewHeight = "VFXUtility.Sequencer.PreviewHeight";
        private const string PrefKeyCameraYaw = "VFXUtility.Sequencer.CameraYaw";
        private const string PrefKeyCameraPitch = "VFXUtility.Sequencer.CameraPitch";
        private const string PrefKeyCameraDistance = "VFXUtility.Sequencer.CameraDistance";
        private const string PrefKeyShowGrid = "VFXUtility.Sequencer.ShowGrid";

        private const float DefaultInspectorWidth = 320f;
        private const float DefaultPreviewHeight = 220f;
        private const float DefaultCameraPitch = 10f;
        private const float DefaultCameraDistance = 6f;

        private VFXSequenceDefinition mTarget;
        private SerializedObject mSerializedObject;
        private VFXSequencerGraphView mGraphView;
        private VFXSequenceNodeInspectorPanel mInspectorPanel;
        private IMGUIContainer mPreviewContainer;
        private Button mPlayButton;
        private Toggle mGridToggle;
        private HelpBox mGoalWarningBox;
        private HelpBox mRootWarningBox;

        // プレビュー再生時に適用するオーバーライドセット(未設定なら適用しない)
        private VFXSequenceOverrideSet mPreviewOverrideSet;

        private PreviewVFXSequenceHost mPreviewHost;
        private VFXSequenceGraphExecutor mPreviewExecutor;
        private bool mIsPreviewPlaying;
        private double mLastEditorTime;

        // Play/Stopを跨いで保持するカメラ・グリッド状態。ウィンドウを閉じるとEditorPrefsへ保存する
        private Vector2 mCameraOrbit;
        private float mCameraDistance;
        private bool mShowGrid;

        // TwoPaneSplitViewの現在サイズを保存用に追跡する(ドラッグのたびにGeometryChangedEventで更新)
        private float mCurrentInspectorWidth;
        private float mCurrentPreviewHeight;

        [MenuItem("Window/VFXUtility/Sequencer")]
        public static void Open()
        {
            var window = GetWindow<VFXSequencerWindow>();
            window.titleContent = new GUIContent("VFX Sequencer");
            window.minSize = new Vector2(720, 420);
            window.Show();
        }

        // Projectウィンドウ等でVFXSequenceDefinitionをダブルクリックした際にこのウィンドウで開く
        [OnOpenAsset]
        public static bool OnOpenAsset(int aInstanceId, int aLine)
        {
            if (EditorUtility.InstanceIDToObject(aInstanceId) is not VFXSequenceDefinition asset)
            {
                return false;
            }

            var window = GetWindow<VFXSequencerWindow>();
            window.titleContent = new GUIContent("VFX Sequencer");
            window.SetTarget(asset);
            window.Show();
            return true;
        }

        // aTarget : 編集対象を切り替える
        public void SetTarget(VFXSequenceDefinition aTarget)
        {
            StopPreview();

            mTarget = aTarget;
            mSerializedObject = mTarget != null ? new SerializedObject(mTarget) : null;

            RebuildUI();
        }

        private void OnEnable()
        {
            LoadPrefs();
            RebuildUI();
        }

        private void OnDisable()
        {
            StopPreview();
            SavePrefs();
        }

        private void LoadPrefs()
        {
            mCurrentInspectorWidth = EditorPrefs.GetFloat(PrefKeyInspectorWidth, DefaultInspectorWidth);
            mCurrentPreviewHeight = EditorPrefs.GetFloat(PrefKeyPreviewHeight, DefaultPreviewHeight);
            mCameraOrbit = new Vector2(
                EditorPrefs.GetFloat(PrefKeyCameraYaw, 0f),
                EditorPrefs.GetFloat(PrefKeyCameraPitch, DefaultCameraPitch));
            mCameraDistance = EditorPrefs.GetFloat(PrefKeyCameraDistance, DefaultCameraDistance);
            mShowGrid = EditorPrefs.GetBool(PrefKeyShowGrid, true);
        }

        private void SavePrefs()
        {
            EditorPrefs.SetFloat(PrefKeyInspectorWidth, mCurrentInspectorWidth);
            EditorPrefs.SetFloat(PrefKeyPreviewHeight, mCurrentPreviewHeight);
            EditorPrefs.SetFloat(PrefKeyCameraYaw, mCameraOrbit.x);
            EditorPrefs.SetFloat(PrefKeyCameraPitch, mCameraOrbit.y);
            EditorPrefs.SetFloat(PrefKeyCameraDistance, mCameraDistance);
            EditorPrefs.SetBool(PrefKeyShowGrid, mShowGrid);
        }

        private void RebuildUI()
        {
            StopPreview();

            rootVisualElement.Clear();
            rootVisualElement.style.flexDirection = FlexDirection.Column;

            rootVisualElement.Add(BuildToolbar());

            if (mTarget == null || mSerializedObject == null)
            {
                mGoalWarningBox = null; // Clear()で破棄済みの要素を参照し続けないようにする
                mRootWarningBox = null;
                rootVisualElement.Add(new Label("VFXSequenceDefinitionアセットを選択してください")
                {
                    style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleCenter }
                });
                return;
            }

            // ルートノードがちょうど1つでない場合、Play()の開始点が正しく決まらないため警告を出す
            mRootWarningBox = new HelpBox(
                "ルートノードがちょうど1つではありません(0個または2個以上)。Play()の開始点が正しく決まりません。",
                HelpBoxMessageType.Warning);
            rootVisualElement.Add(mRootWarningBox);
            RefreshRootWarning();

            // ゴールノードへ到達できないグラフは完了通知が発火しないため警告を出す
            mGoalWarningBox = new HelpBox(
                "到達可能なゴールノードがありません。完了通知(OnSequenceCompleted / PlayAsync)は発火しません。",
                HelpBoxMessageType.Warning);
            rootVisualElement.Add(mGoalWarningBox);
            RefreshGoalWarning();

            mGraphView = new VFXSequencerGraphView(mSerializedObject, mTarget)
            {
                style = { flexGrow = 1 }
            };
            mGraphView.OnNodeSelectionChanged += aProperty => mInspectorPanel?.SetTargetProperty(aProperty);
            mGraphView.OnGraphStructureChanged += RefreshGoalWarning;
            mGraphView.OnGraphStructureChanged += RefreshRootWarning;
            // 接続の追加/削除等で選択中ノードの表示内容(分岐ノードの重み/true-false一覧等)が変わりうるため再描画する
            mGraphView.OnGraphStructureChanged += () => mInspectorPanel?.Refresh();

            mPreviewContainer = new IMGUIContainer(DrawPreviewIMGUI)
            {
                style = { borderTopWidth = 1 }
            };
            // ドラッグでプレビュー高さが変わるたびに現在値を追跡し、ウィンドウを閉じる際に保存できるようにする
            mPreviewContainer.RegisterCallback<GeometryChangedEvent>(evt => mCurrentPreviewHeight = evt.newRect.height);

            // グラフ表示とプレビューの上下比率はドラッグで変更できる(前回保存したプレビュー高さから開始)
            var leftSplit = new TwoPaneSplitView(1, mCurrentPreviewHeight, TwoPaneSplitViewOrientation.Vertical)
            {
                style = { flexGrow = 1 }
            };
            leftSplit.Add(mGraphView);
            leftSplit.Add(mPreviewContainer);

            mInspectorPanel = new VFXSequenceNodeInspectorPanel(mGraphView, TriggerPreviewEvent)
            {
                style = { borderLeftWidth = 1 }
            };
            mInspectorPanel.RegisterCallback<GeometryChangedEvent>(evt => mCurrentInspectorWidth = evt.newRect.width);

            // グラフ+プレビューとInspectorパネルの左右比率もドラッグで変更できる(前回保存した幅から開始)
            var mainSplit = new TwoPaneSplitView(1, mCurrentInspectorWidth, TwoPaneSplitViewOrientation.Horizontal)
            {
                style = { flexGrow = 1 }
            };
            mainSplit.Add(leftSplit);
            mainSplit.Add(mInspectorPanel);

            rootVisualElement.Add(mainSplit);
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    paddingTop = 2, paddingBottom = 2, paddingLeft = 4, paddingRight = 4,
                    borderBottomWidth = 1
                }
            };

            var objectField = new ObjectField("対象") { objectType = typeof(VFXSequenceDefinition), value = mTarget };
            objectField.RegisterValueChangedCallback(evt => SetTarget(evt.newValue as VFXSequenceDefinition));
            objectField.style.flexGrow = 1;
            toolbar.Add(objectField);

            var overrideSetField = new ObjectField("上書き")
            {
                objectType = typeof(VFXSequenceOverrideSet),
                value = mPreviewOverrideSet,
            };
            overrideSetField.RegisterValueChangedCallback(evt => mPreviewOverrideSet = evt.newValue as VFXSequenceOverrideSet);
            overrideSetField.style.flexGrow = 1;
            toolbar.Add(overrideSetField);

            mGridToggle = new Toggle("Grid") { value = mShowGrid };
            mGridToggle.RegisterValueChangedCallback(evt =>
            {
                mShowGrid = evt.newValue;
                if (mPreviewHost != null)
                {
                    mPreviewHost.ShowGrid = mShowGrid;
                }
            });
            toolbar.Add(mGridToggle);

            mPlayButton = new Button(TogglePreview) { text = "Play" };
            mPlayButton.SetEnabled(mTarget != null);
            toolbar.Add(mPlayButton);

            return toolbar;
        }

        private void TogglePreview()
        {
            if (mIsPreviewPlaying)
            {
                StopPreview();
            }
            else
            {
                StartPreview();
            }
        }

        private void StartPreview()
        {
            if (mTarget == null || mIsPreviewPlaying)
            {
                return;
            }

            mPreviewHost = new PreviewVFXSequenceHost(mCameraOrbit, mCameraDistance, mShowGrid);
            mPreviewExecutor = new VFXSequenceGraphExecutor(mTarget, mPreviewHost);

            // オーバーライドセットが指定されていれば、適用後の状態でプレビューする
            if (mPreviewOverrideSet != null)
            {
                mPreviewExecutor.ApplyOverrideSet(mPreviewOverrideSet);
            }

            mPreviewExecutor.Play();

            mIsPreviewPlaying = true;
            mLastEditorTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;

            if (mPlayButton != null)
            {
                mPlayButton.text = "Stop";
            }
        }

        private void StopPreview()
        {
            if (!mIsPreviewPlaying)
            {
                return;
            }

            // 直前までの操作結果(カメラ位置)を引き継ぐため、破棄前に読み出しておく
            if (mPreviewHost != null)
            {
                mCameraOrbit = mPreviewHost.CameraOrbit;
                mCameraDistance = mPreviewHost.CameraDistance;
            }

            EditorApplication.update -= OnEditorUpdate;
            mIsPreviewPlaying = false;

            mPreviewHost?.Dispose();
            mPreviewHost = null;
            mPreviewExecutor = null;

            if (mPlayButton != null)
            {
                mPlayButton.text = "Play";
            }

            mPreviewContainer?.MarkDirtyRepaint();
        }

        private void OnEditorUpdate()
        {
            if (!mIsPreviewPlaying || mPreviewExecutor == null || mPreviewHost == null)
            {
                return;
            }

            float deltaTime = (float)(EditorApplication.timeSinceStartup - mLastEditorTime);
            mLastEditorTime = EditorApplication.timeSinceStartup;

            mPreviewExecutor.Tick(deltaTime);
            mPreviewHost.AdvanceSimulation(deltaTime);

            mPreviewContainer?.MarkDirtyRepaint();
        }

        // 到達可能なゴールノードの有無を判定し、警告表示を切り替える
        private void RefreshGoalWarning()
        {
            if (mGoalWarningBox == null)
            {
                return;
            }

            bool showWarning = mTarget != null && mTarget.HasNoReachableGoalNode();
            mGoalWarningBox.style.display = showWarning ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ルートノードの個数がちょうど1つかを判定し、警告表示を切り替える
        private void RefreshRootWarning()
        {
            if (mRootWarningBox == null)
            {
                return;
            }

            bool showWarning = mTarget != null && mTarget.HasInvalidRootNodeCount();
            mRootWarningBox.style.display = showWarning ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // イベントノードは自動開始しないため、Inspectorパネルの「発火」ボタンからここ経由でPlayEventを呼ぶ
        // プレビューが再生中でない場合は何もしない(先にPlayを押してもらう必要がある)
        // aEventName : 発火するイベント名
        private void TriggerPreviewEvent(string aEventName)
        {
            if (!mIsPreviewPlaying || mPreviewExecutor == null || string.IsNullOrEmpty(aEventName))
            {
                return;
            }

            mPreviewExecutor.PlayEvent(aEventName);
        }

        private void DrawPreviewIMGUI()
        {
            Rect rect = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (mPreviewHost == null)
            {
                EditorGUI.HelpBox(rect, "Playを押すとここにプレビューが再生されます", MessageType.Info);
                return;
            }

            mPreviewHost.DrawPreview(rect);
        }
    }
}
