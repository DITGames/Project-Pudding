/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file AnimSequencerWindow.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief AnimSequenceDefinitionのアニメーションキー一覧管理・タイムライン編集・プレビュー再生を一体化したウィンドウ
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AnimSequencer2D.Editor
{
    public class AnimSequencerWindow : EditorWindow
    {
        private const string UssPath = "Assets/Scripts/AnimSequencer2D/Editor/AnimSequencerWindow.uss";

        // レイアウト状態を保存するEditorPrefsキー(ウィンドウを閉じても復元される)
        private const string PrefKeyEntryListWidth = "AnimSequencer2D.Sequencer.EntryListWidth";
        private const string PrefKeyInspectorWidth = "AnimSequencer2D.Sequencer.InspectorWidth";
        private const string PrefKeyTimelineHeight = "AnimSequencer2D.Sequencer.TimelineHeight";
        private const string PrefKeyAspectSelected = "AnimSequencer2D.Sequencer.AspectSelected";
        private const string PrefKeyAspectCustomPresets = "AnimSequencer2D.Sequencer.AspectCustomPresets";
        private const string PrefKeyMoveSnap = "AnimSequencer2D.Sequencer.MoveSnap";
        private const string PrefKeyRotateSnap = "AnimSequencer2D.Sequencer.RotateSnap";
        private const string PrefKeyScaleSnap = "AnimSequencer2D.Sequencer.ScaleSnap";
        private const string PrefKeyFrameSnapFps = "AnimSequencer2D.Sequencer.FrameSnapFps";
        private const string PrefKeyPreviewGridSpacing = "AnimSequencer2D.Sequencer.PreviewGridSpacing";
        private const string PrefKeyKeyframeShortcutKey = "AnimSequencer2D.Sequencer.KeyframeShortcutKey";
        private const string PrefKeySimulateRuntime = "AnimSequencer2D.Sequencer.SimulateRuntime";
        private const string PrefKeyFrameStepSeconds = "AnimSequencer2D.Sequencer.FrameStepSeconds";
        // キーボードNudgeの8アクション分。アクションごとに個別のPrefキーで保存する(将来アクションが増減しても壊れにくいため)
        private const string PrefKeyNudgeKeyPrefix = "AnimSequencer2D.Sequencer.NudgeKey.";

        private const float DefaultEntryListWidth = 240f;
        private const float DefaultInspectorWidth = 280f;
        // トラック1本(5チャンネル行+ヘッダ+イベント行+トラック追加ボタン)がちょうど収まる程度の既定高さ
        private const float DefaultTimelineHeight = 250f;
        // 前後キーフレーム検索でこの秒数以内は「同じ時刻」とみなす
        private const float KeyframeTimeEpsilon = 0.0001f;

        // ギズモのスナップ間隔の既定値(0にするとそのチャンネルはスナップなしになる)
        private const float DefaultMoveSnap = 10f;
        private const float DefaultRotateSnap = 15f;
        private const float DefaultScaleSnap = 0.1f;
        // キーフレーム時刻のフレームスナップの既定値(SPEC.md通り30fps)
        private const float DefaultFrameSnapFps = 30f;
        // 矢印キー(←/→)でのフレーム送り/戻し1回あたりの秒数の既定値(30fpsの1フレーム分)
        private const float DefaultFrameStepSeconds = 1f / 30f;
        private const float DefaultPreviewGridSpacing = 10f;
        // キーフレーム作成ショートカットの既定キー
        private const KeyCode DefaultKeyframeShortcutKey = KeyCode.Space;

        // キーフレーム作成ショートカットの選択肢(英数字+Space/Tab+ファンクションキー中心。SPEC.md参照)
        private static readonly KeyCode[] sKeyframeShortcutChoices = BuildKeyframeShortcutChoices();

        private static KeyCode[] BuildKeyframeShortcutChoices()
        {
            var choices = new List<KeyCode> { KeyCode.Space, KeyCode.Tab };
            for (KeyCode key = KeyCode.A; key <= KeyCode.Z; key++)
            {
                choices.Add(key);
            }
            for (KeyCode key = KeyCode.Alpha0; key <= KeyCode.Alpha9; key++)
            {
                choices.Add(key);
            }
            for (KeyCode key = KeyCode.F1; key <= KeyCode.F12; key++)
            {
                choices.Add(key);
            }
            return choices.ToArray();
        }

        // ===== アスペクト比プリセット(プレビューのアスペクト比固定機能) =====

        private readonly struct AspectPreset
        {
            public readonly string Name;
            public readonly float Width;  // 0以下ならFree Aspect(比率固定なし)
            public readonly float Height;

            public AspectPreset(string aName, float aWidth, float aHeight)
            {
                Name = aName;
                Width = aWidth;
                Height = aHeight;
            }

            public float AspectRatioOrZero => Width > 0f && Height > 0f ? Width / Height : 0f;

            public string ToPrefString() => $"{Name}:{Width}:{Height}";

            public static AspectPreset FromPrefString(string aValue, AspectPreset aFallback)
            {
                string[] parts = aValue?.Split(':');
                if (parts == null || parts.Length != 3 ||
                    !float.TryParse(parts[1], out float width) || !float.TryParse(parts[2], out float height))
                {
                    return aFallback;
                }
                return new AspectPreset(parts[0], width, height);
            }
        }

        private static readonly AspectPreset[] sBuiltInPresets =
        {
            new("Free Aspect", 0, 0),
            new("16:9", 16, 9),
            new("16:10", 16, 10),
            new("Full HD (1920x1080)", 1920, 1080),
            new("WXGA (1366x768)", 1366, 768),
            new("QHD (2560x1440)", 2560, 1440),
            new("4K UHD (3840x2160)", 3840, 2160),
        };

        private AnimSequenceDefinition mTarget;
        private SerializedObject mSerializedObject;

        private AnimSequenceEntryGraphView mEntryGraphView;
        private AnimSequenceTimelineView mTimelineView;
        private AnimSequenceInspectorPanel mInspectorPanel;
        private IMGUIContainer mPreviewContainer;
        private ListView mEntryListView;
        private readonly List<AnimSequenceEntry> mEntryListItems = new();
        // アニメーションキー一覧の検索欄に入力中の文字列(部分一致・大文字小文字区別なしでKeyを絞り込む)
        private string mEntrySearchFilter = string.Empty;
        private ToolbarMenu mAspectDropdown;

        private Button mRewindButton;
        private Button mPrevKeyButton;
        private Button mPlayPauseButton;
        private Button mNextKeyButton;
        private Button mFastForwardButton;
        private Button mFitDurationButton;
        private Label mPlayingKeyLabel;
        // ONの場合、末尾到達時にEndBehavior(Loop/Transition)をそのままランタイムと同じように反映する。
        // OFF(既定)の場合は選択中のアニメーションキーをEndBehaviorに関係なく常にループ再生する(単体編集用)
        private bool mSimulateRuntime;
        private Button mSimulateRuntimeButton;
        private ToolbarMenu mGridWidthDropdown;
        private ToolbarMenu mFrameSnapDropdown;
        // キーフレーム時刻のフレームスナップ(0以下でスナップなし)。AnimSequenceTimelineViewへ注入する
        private float mFrameSnapFps = DefaultFrameSnapFps;
        // 矢印キー(←/→)でのフレーム送り/戻し1回あたりの秒数。AnimSequenceTimelineViewへ注入する
        private float mFrameStepSeconds = DefaultFrameStepSeconds;
        private FloatField mFrameStepField;
        private HelpBox mDuplicateKeyWarningBox;
        private HelpBox mInvalidTransitionWarningBox;
        private HelpBox mInvalidMaterialParameterWarningBox;
        private HelpBox mInvalidObjectReferenceWarningBox;

        // ウィンドウ全体の画面モード。「アニメーション編集」は既存の3ペイン画面、「初期配置」は
        // アニメーションキーをまたいで共有するオブジェクトを配置・編集する新設画面
        private enum WindowMode { AnimationEdit, ObjectPlacement }
        private WindowMode mWindowMode = WindowMode.AnimationEdit;
        private Button mAnimationEditModeButton;
        private Button mObjectPlacementModeButton;
        private IMGUIContainer mObjectPlacementContainer;
        private ListView mObjectListView;
        private readonly List<AnimSequenceObject> mObjectListItems = new();
        private VisualElement mObjectDetailsContainer;
        // 初期配置画面のキャンバスとオブジェクト一覧の選択を同期させるために使う(キャンバスのクリックはPreviewAnimSequenceHost内部で
        // 完結するため、毎フレームSelectedTrackIdと比較して変化を検知する)
        private string mSelectedPlacementObjectId;
        private Button mPlacementMoveModeButton;
        private Button mPlacementRotateModeButton;
        private Button mPlacementScaleModeButton;

        // プレビューのPreview/Editモード(Editモードのときのみギズモ・グリッドが有効になる)
        private enum PreviewEditMode { Preview, Edit }

        // Editモードでのキーボード操作(選択中トラックへのNudge)の8アクション
        private enum KeyboardNudgeAction { MoveUp, MoveDown, MoveLeft, MoveRight, RotateCcw, RotateCw, ScaleUp, ScaleDown }
        private PreviewEditMode mPreviewEditMode = PreviewEditMode.Preview;
        private Button mPreviewModeToggleButton;
        private Button mEditModeToggleButton;

        // ギズモの種類とモード切替ボタン(Editモード時のみ有効)
        private GizmoMode mGizmoMode = GizmoMode.Move;
        private Button mMoveModeButton;
        private Button mRotateModeButton;
        private Button mScaleModeButton;
        private Button mSnapSettingsButton;
        // ギズモ編集内容からキーフレームを作成/上書きするショートカットキー(既定Space)。EditorPrefsで永続化する
        private KeyCode mKeyframeShortcutKey = DefaultKeyframeShortcutKey;
        private ToolbarMenu mKeyframeShortcutDropdown;

        // Editモードでの選択中トラックへのキーボードNudge操作(既定WASD/QE/RF)。現在選択中のギズモモードに関係なく常に使える
        private readonly Dictionary<KeyboardNudgeAction, KeyCode> mNudgeKeyBindings = new()
        {
            { KeyboardNudgeAction.MoveUp, KeyCode.W },
            { KeyboardNudgeAction.MoveDown, KeyCode.S },
            { KeyboardNudgeAction.MoveLeft, KeyCode.A },
            { KeyboardNudgeAction.MoveRight, KeyCode.D },
            { KeyboardNudgeAction.RotateCcw, KeyCode.Q },
            { KeyboardNudgeAction.RotateCw, KeyCode.E },
            { KeyboardNudgeAction.ScaleUp, KeyCode.R },
            { KeyboardNudgeAction.ScaleDown, KeyCode.F },
        };
        private Button mNudgeSettingsButton;

        // ギズモ操作時のスナップ間隔(移動/回転/拡縮それぞれ独立に設定可能。0でスナップなし)
        private float mMoveSnapValue = DefaultMoveSnap;
        private float mRotateSnapValue = DefaultRotateSnap;
        private float mScaleSnapValue = DefaultScaleSnap;
        // Editモードのプレビューに表示するグリッド間隔。ギズモの移動スナップとは独立して設定する
        private float mPreviewGridSpacing = DefaultPreviewGridSpacing;

        private PreviewAnimSequenceHost mPreviewHost;
        private PreviewAnimSequenceTimeProvider mPreviewTimeProvider;
        private AnimSequencePlayback mPreviewPlayback;
        // trueの間、選択中エントリの基準状態・見た目が読み込まれている(スクラブ・コマ送りの対象になる)
        private bool mIsPreviewPlaying;
        // trueの間、Tick()による時間の自動進行を止める(スクラブ・コマ送りは引き続き可能)
        private bool mIsPreviewPaused;
        // 選択中キーフレームの時刻(未選択はnull)。ギズモ編集の書き込み先時刻・選択時のプレビュー表示位置に使う。
        // SerializedPropertyは並べ替え・再構築で別の要素を指しうるため、参照ではなく値として保持する
        private float? mSelectedKeyframeTime;
        private double mLastEditorTime;

        // 現在選択中のエントリ(グラフのノード選択で更新される)。キーフレーム未選択時のInspector表示対象にもなる
        private SerializedProperty mSelectedEntryProperty;
        // 選択中エントリのKey。Undo/Redo後にmSelectedEntryPropertyが古くなる可能性があるため、IDとして保持し再検索に使う
        private string mSelectedEntryKey;
        // 直前にMute/Soloをリセットした際のエントリキー(切り替え検出用)
        private string mLastMuteSoloEntryKey;

        private readonly List<AspectPreset> mCustomPresets = new();
        private AspectPreset mSelectedAspectPreset = sBuiltInPresets[0];

        // TwoPaneSplitViewの現在サイズを保存用に追跡する(ドラッグのたびにGeometryChangedEventで更新)
        private float mCurrentEntryListWidth;
        private float mCurrentInspectorWidth;
        private float mCurrentTimelineHeight;
        private bool mIsSynchronizingEntryListSelection;

        [MenuItem("Window/AnimSequencer2D/Sequencer")]
        public static void Open()
        {
            var window = GetWindow<AnimSequencerWindow>();
            window.titleContent = new GUIContent("Anim Sequencer");
            window.minSize = new Vector2(720, 420);
            window.Show();
        }

        // Projectウィンドウ等でAnimSequenceDefinitionをダブルクリックした際にこのウィンドウで開く
        [OnOpenAsset]
        public static bool OnOpenAsset(int aInstanceId, int aLine)
        {
            if (EditorUtility.InstanceIDToObject(aInstanceId) is not AnimSequenceDefinition asset)
            {
                return false;
            }

            var window = GetWindow<AnimSequencerWindow>();
            window.titleContent = new GUIContent("Anim Sequencer");
            window.SetTarget(asset);
            window.Show();
            return true;
        }

        // aTarget : 編集対象を切り替える
        public void SetTarget(AnimSequenceDefinition aTarget)
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
            EditorApplication.update += PeriodicTimelineRefresh;
            Undo.undoRedoEvent += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoEvent -= OnUndoRedo;
            EditorApplication.update -= PeriodicTimelineRefresh;
            StopPreview();
            SavePrefs();
        }

        // Undo/Redoが発生した際、各パネル(グラフ・タイムライン・インスペクタ・一覧・プレビュー)の表示を最新のデータへ追従させる
        private void OnUndoRedo(in UndoRedoInfo aInfo)
        {
            if (mTarget == null || mSerializedObject == null)
            {
                return;
            }

            mSerializedObject.Update(); // SerializedObjectのキャッシュをUndo/Redo後の最新データへ同期する
            StopPreview(); // 再生中の内部状態を安全のため一旦破棄する(SPEC.mdの受け入れ条件5)

            mEntryGraphView?.RefreshFromExternalChange();
            RefreshEntryListItems();
            mEntryListView?.RefreshItems();

            // 保持しておいたKeyで選択を再検索し、既存のOnEntrySelectionChangedへ渡すことで
            // タイムライン・インスペクタ・一覧の選択・プレビューのアイドル表示・Mute/Solo・トランスポートボタンを
            // まとめて最新状態へ追従させる(見つからなければ未選択表示になる)
            OnEntrySelectionChanged(FindEntryPropertyByKey(mSelectedEntryKey));

            RefreshWarnings();
        }

        // aKeyに一致するエントリのSerializedPropertyを再検索する。見つからなければnull
        private SerializedProperty FindEntryPropertyByKey(string aKey)
        {
            if (string.IsNullOrEmpty(aKey) || mSerializedObject == null)
            {
                return null;
            }

            SerializedProperty entriesProperty = mSerializedObject.FindProperty("mEntries");
            for (int index = 0; index < entriesProperty.arraySize; index++)
            {
                SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(index);
                if (entry.FindPropertyRelative("mKey").stringValue == aKey)
                {
                    return entry;
                }
            }
            return null;
        }

        // タイムラインの非破壊的な位置更新は毎エディタTickで呼んでよい(内部フラグが立っていない限り即returnするため負荷は無視できる)
        private void PeriodicTimelineRefresh() => mTimelineView?.RefreshPositionsIfDirty();

        private void LoadPrefs()
        {
            mCurrentEntryListWidth = EditorPrefs.GetFloat(PrefKeyEntryListWidth, DefaultEntryListWidth);
            mCurrentInspectorWidth = EditorPrefs.GetFloat(PrefKeyInspectorWidth, DefaultInspectorWidth);
            mCurrentTimelineHeight = EditorPrefs.GetFloat(PrefKeyTimelineHeight, DefaultTimelineHeight);

            mMoveSnapValue = EditorPrefs.GetFloat(PrefKeyMoveSnap, DefaultMoveSnap);
            mRotateSnapValue = EditorPrefs.GetFloat(PrefKeyRotateSnap, DefaultRotateSnap);
            mScaleSnapValue = EditorPrefs.GetFloat(PrefKeyScaleSnap, DefaultScaleSnap);
            mPreviewGridSpacing = EditorPrefs.GetFloat(PrefKeyPreviewGridSpacing, DefaultPreviewGridSpacing);
            mFrameSnapFps = EditorPrefs.GetFloat(PrefKeyFrameSnapFps, DefaultFrameSnapFps);
            mFrameStepSeconds = EditorPrefs.GetFloat(PrefKeyFrameStepSeconds, DefaultFrameStepSeconds);
            mKeyframeShortcutKey = (KeyCode)EditorPrefs.GetInt(PrefKeyKeyframeShortcutKey, (int)DefaultKeyframeShortcutKey);
            mSimulateRuntime = EditorPrefs.GetBool(PrefKeySimulateRuntime, false);

            foreach (KeyboardNudgeAction action in (KeyboardNudgeAction[])Enum.GetValues(typeof(KeyboardNudgeAction)))
            {
                mNudgeKeyBindings[action] = (KeyCode)EditorPrefs.GetInt(PrefKeyNudgeKeyPrefix + action, (int)mNudgeKeyBindings[action]);
            }

            mCustomPresets.Clear();
            string savedCustom = EditorPrefs.GetString(PrefKeyAspectCustomPresets, string.Empty);
            if (!string.IsNullOrEmpty(savedCustom))
            {
                foreach (string entry in savedCustom.Split(';'))
                {
                    AspectPreset preset = AspectPreset.FromPrefString(entry, default);
                    if (!string.IsNullOrEmpty(preset.Name))
                    {
                        mCustomPresets.Add(preset);
                    }
                }
            }

            string savedSelected = EditorPrefs.GetString(PrefKeyAspectSelected, string.Empty);
            mSelectedAspectPreset = string.IsNullOrEmpty(savedSelected)
                ? sBuiltInPresets[0]
                : AspectPreset.FromPrefString(savedSelected, sBuiltInPresets[0]);
        }

        private void SavePrefs()
        {
            EditorPrefs.SetFloat(PrefKeyEntryListWidth, mCurrentEntryListWidth);
            EditorPrefs.SetFloat(PrefKeyInspectorWidth, mCurrentInspectorWidth);
            EditorPrefs.SetFloat(PrefKeyTimelineHeight, mCurrentTimelineHeight);
            SaveAspectPrefs();
            SaveSnapPrefs();
        }

        private void SaveAspectPrefs()
        {
            EditorPrefs.SetString(PrefKeyAspectSelected, mSelectedAspectPreset.ToPrefString());
            EditorPrefs.SetString(PrefKeyAspectCustomPresets, string.Join(";", mCustomPresets.Select(p => p.ToPrefString())));
        }

        private void SaveSnapPrefs()
        {
            EditorPrefs.SetFloat(PrefKeyMoveSnap, mMoveSnapValue);
            EditorPrefs.SetFloat(PrefKeyRotateSnap, mRotateSnapValue);
            EditorPrefs.SetFloat(PrefKeyScaleSnap, mScaleSnapValue);
            EditorPrefs.SetFloat(PrefKeyPreviewGridSpacing, mPreviewGridSpacing);
            EditorPrefs.SetFloat(PrefKeyFrameSnapFps, mFrameSnapFps);
            EditorPrefs.SetFloat(PrefKeyFrameStepSeconds, mFrameStepSeconds);
            EditorPrefs.SetInt(PrefKeyKeyframeShortcutKey, (int)mKeyframeShortcutKey);
            EditorPrefs.SetBool(PrefKeySimulateRuntime, mSimulateRuntime);
            SaveNudgeKeyPrefs();
        }

        private void SaveNudgeKeyPrefs()
        {
            foreach (KeyValuePair<KeyboardNudgeAction, KeyCode> pair in mNudgeKeyBindings)
            {
                EditorPrefs.SetInt(PrefKeyNudgeKeyPrefix + pair.Key, (int)pair.Value);
            }
        }

        private void RebuildUI()
        {
            StopPreview();

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            rootVisualElement.Clear();
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }
            rootVisualElement.style.flexDirection = FlexDirection.Column;

            rootVisualElement.Add(BuildToolbar());

            if (mTarget == null || mSerializedObject == null)
            {
                mDuplicateKeyWarningBox = null; // Clear()で破棄済みの要素を参照し続けないようにする
                mInvalidTransitionWarningBox = null;
                mInvalidMaterialParameterWarningBox = null;
                mInvalidObjectReferenceWarningBox = null;
                rootVisualElement.Add(new Label("AnimSequenceDefinitionアセットを選択してください")
                {
                    style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleCenter }
                });
                return;
            }

            mPreviewHost = new PreviewAnimSequenceHost(mTarget);
            mPreviewTimeProvider = new PreviewAnimSequenceTimeProvider();

            // 重複したアニメーションキーが存在すると再生対象が一意に定まらないため警告を出す
            mDuplicateKeyWarningBox = new HelpBox(
                "重複したアニメーションキーが存在します。PlaySequenceは最初に見つかったエントリのみを対象にします。",
                HelpBoxMessageType.Warning);
            rootVisualElement.Add(mDuplicateKeyWarningBox);

            // 存在しないキーへの遷移設定はStop相当にフォールバックするだけで即座に不具合にはならないが、
            // 設定漏れに気付けるよう警告として出す
            mInvalidTransitionWarningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            rootVisualElement.Add(mInvalidTransitionWarningBox);

            // 基準Materialのシェーダに存在しないプロパティを指すMaterialパラメータトラックがあると、
            // ランタイム側では単純にスキップされるだけで気付きにくいため警告として出す
            mInvalidMaterialParameterWarningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            rootVisualElement.Add(mInvalidMaterialParameterWarningBox);

            // 参照先オブジェクトが削除・見つからなくなったトラックがあると、ランタイムでは黙ってスキップされるだけで
            // 気付きにくいため警告として出す
            mInvalidObjectReferenceWarningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            rootVisualElement.Add(mInvalidObjectReferenceWarningBox);

            RefreshWarnings();

            if (mWindowMode == WindowMode.ObjectPlacement)
            {
                rootVisualElement.Add(BuildObjectPlacementScreen());
                return;
            }

            mEntryGraphView = new AnimSequenceEntryGraphView(mSerializedObject, mTarget)
            {
                // entryGraphArea(Column)内で高さ0に潰れないよう、明示的に残り領域全部を使わせる
                style = { flexGrow = 1, borderRightWidth = 1 }
            };
            mEntryGraphView.OnEntrySelectionChanged += OnEntrySelectionChanged;
            mEntryGraphView.OnGraphStructureChanged += OnEntryGraphStructureChanged;
            mEntryGraphView.RegisterCallback<GeometryChangedEvent>(evt => mCurrentEntryListWidth = evt.newRect.width);

            var entryGraphArea = new VisualElement { style = { flexDirection = FlexDirection.Column, flexGrow = 1 } };
            entryGraphArea.Add(mEntryGraphView);
            entryGraphArea.Add(BuildEntryListPanel());

            mTimelineView = new AnimSequenceTimelineView(mSerializedObject, mTarget, OnKeyframeSelectionChanged, RefreshWarnings, ScrubToTime,
                mPreviewHost, () => mPreviewContainer?.MarkDirtyRepaint())
            {
                style = { flexGrow = 1 }
            };
            mTimelineView.SetFrameSnapFps(mFrameSnapFps); // EditorPrefsから読み込んだ設定値を反映する(既定値のままだと30fps固定になってしまうため)
            mTimelineView.SetFrameStepSeconds(mFrameStepSeconds); // 同様に矢印キーのステップ秒数もEditorPrefsの設定値を反映する

            mPreviewContainer = new IMGUIContainer(DrawPreviewIMGUI)
            {
                style = { flexGrow = 1 },
                focusable = true, // Spaceキーでのキーフレーム作成をこの領域に閉じるため、クリック時にフォーカスを持たせる
            };

            var previewArea = new VisualElement { style = { flexDirection = FlexDirection.Column, flexGrow = 1, borderBottomWidth = 1 } };
            previewArea.Add(BuildPreviewToolbar());
            previewArea.Add(mPreviewContainer);

            // 再生用トランスポートバーは、タイムラインのスクロール領域外かつ上部に固定する
            var timelineArea = new VisualElement { style = { flexDirection = FlexDirection.Column, flexGrow = 1 } };
            timelineArea.Add(BuildTransportToolbar());
            timelineArea.Add(mTimelineView);
            timelineArea.RegisterCallback<GeometryChangedEvent>(evt => mCurrentTimelineHeight = evt.newRect.height);

            // プレビューを上側・残り領域全部を使う可変側にし、タイムライン(内容量に応じた既定高さ、
            // ドラッグでリサイズ可能)を下側の固定側にする
            var centerSplit = new TwoPaneSplitView(1, mCurrentTimelineHeight, TwoPaneSplitViewOrientation.Vertical)
            {
                style = { flexGrow = 1 }
            };
            centerSplit.Add(previewArea);
            centerSplit.Add(timelineArea);

            var leftSplit = new TwoPaneSplitView(0, mCurrentEntryListWidth, TwoPaneSplitViewOrientation.Horizontal)
            {
                style = { flexGrow = 1 }
            };
            leftSplit.Add(entryGraphArea);
            leftSplit.Add(centerSplit);

            mInspectorPanel = new AnimSequenceInspectorPanel(() => mTimelineView?.RequestPositionRefresh(), OnKeyRenamed, OnSelectedKeyframeTimeChanged)
            {
                style = { borderLeftWidth = 1 }
            };
            mInspectorPanel.RegisterCallback<GeometryChangedEvent>(evt => mCurrentInspectorWidth = evt.newRect.width);

            var mainSplit = new TwoPaneSplitView(1, mCurrentInspectorWidth, TwoPaneSplitViewOrientation.Horizontal)
            {
                style = { flexGrow = 1 }
            };
            mainSplit.Add(leftSplit);
            mainSplit.Add(mInspectorPanel);

            rootVisualElement.Add(mainSplit);

            OnEntrySelectionChanged(null);
        }

        // グラフ下部に表示するアニメーションキー一覧を作る
        private VisualElement BuildEntryListPanel()
        {
            var container = new VisualElement { style = { height = 150, flexShrink = 0, borderTopWidth = 1, flexDirection = FlexDirection.Column } };
            container.Add(new Label("アニメーションキー") { style = { height = 20, unityTextAlign = TextAnchor.MiddleLeft, paddingLeft = 6 } });

            var searchField = new TextField { value = mEntrySearchFilter, style = { marginLeft = 4, marginRight = 4, marginBottom = 2 } };
            searchField.RegisterValueChangedCallback(evt =>
            {
                mEntrySearchFilter = evt.newValue;
                RefreshEntryListItems();
                mEntryListView?.RefreshItems();
            });
            container.Add(searchField);

            RefreshEntryListItems();

            mEntryListView = new ListView
            {
                itemsSource = mEntryListItems,
                selectionType = SelectionType.Single,
                fixedItemHeight = 20,
                style = { flexGrow = 1 }
            };
            mEntryListView.makeItem = () => new Label();
            // itemsSource(mEntryListItems)自体を参照する。検索フィルタで絞り込まれるとmTarget.Entriesとは
            // インデックスが一致しなくなるため、mTarget.Entries[index]を直接参照してはいけない
            mEntryListView.bindItem = (element, index) => ((Label)element).text = mEntryListItems[index].Key;
            mEntryListView.selectionChanged += selectedItems =>
            {
                if (mIsSynchronizingEntryListSelection || selectedItems.FirstOrDefault() is not AnimSequenceEntry entry)
                {
                    return;
                }
                mEntryGraphView?.SelectAndFocusEntry(entry.Key);
            };
            container.Add(mEntryListView);
            return container;
        }

        // ノード追加・削除後に一覧を更新する
        private void OnEntryGraphStructureChanged()
        {
            RefreshWarnings();
            RefreshEntryListItems();
            mEntryListView?.RefreshItems();
        }

        // ListViewが要求するIListへ、検索欄の文字列で絞り込んだ一覧をコピーする
        private void RefreshEntryListItems()
        {
            mEntryListItems.Clear();
            if (mTarget == null)
            {
                return;
            }
            foreach (AnimSequenceEntry entry in mTarget.Entries)
            {
                if (string.IsNullOrEmpty(mEntrySearchFilter) || entry.Key.IndexOf(mEntrySearchFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    mEntryListItems.Add(entry);
                }
            }
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

            var saveButton = new Button(SaveTarget) { text = "保存", tooltip = "編集中のAnimSequenceDefinitionアセットを保存する(Ctrl+S相当)", style = { marginRight = 8 } };
            toolbar.Add(saveButton);

            var objectField = new ObjectField("対象") { objectType = typeof(AnimSequenceDefinition), value = mTarget };
            objectField.RegisterValueChangedCallback(evt => SetTarget(evt.newValue as AnimSequenceDefinition));
            objectField.style.flexGrow = 1;
            toolbar.Add(objectField);

            mAnimationEditModeButton = new Button(() => SetWindowMode(WindowMode.AnimationEdit)) { text = "アニメーション", style = { marginLeft = 8 } };
            mObjectPlacementModeButton = new Button(() => SetWindowMode(WindowMode.ObjectPlacement)) { text = "オブジェクト" };
            toolbar.Add(mAnimationEditModeButton);
            toolbar.Add(mObjectPlacementModeButton);
            RefreshWindowModeButtons();

            return toolbar;
        }

        // 編集中のアセットを明示的にディスクへ保存する。SerializedObject.ApplyModifiedProperties()自体は
        // アセットをdirty化するだけでディスクへの書き込みは行わないため、Unity終了時のプロンプト等に頼らず
        // 明示的に保存したい場合のためのボタン
        private void SaveTarget()
        {
            if (mTarget == null)
            {
                return;
            }
            AssetDatabase.SaveAssetIfDirty(mTarget);
        }

        // タイムライン(トラック)の直上に置く再生トランスポートバー。Unity標準のAnimationウィンドウに倣った構成
        private VisualElement BuildTransportToolbar()
        {
            var toolbar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row, alignItems = Align.Center, justifyContent = Justify.Center,
                    paddingTop = 2, paddingBottom = 2, paddingLeft = 4, paddingRight = 4,
                    borderBottomWidth = 1
                }
            };

            // 左右を同じ幅に固定し、再生ボタン群がウィンドウの中央に来るようにする
            var leftArea = new VisualElement { style = { width = 340, flexShrink = 0, justifyContent = Justify.Center } };
            mPlayingKeyLabel = new Label(string.Empty) { style = { unityTextAlign = TextAnchor.MiddleLeft, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis } };
            leftArea.Add(mPlayingKeyLabel);
            toolbar.Add(leftArea);

            var playbackButtons = new VisualElement { style = { flexDirection = FlexDirection.Row, flexShrink = 0 } };

            mRewindButton = new Button(RewindToStart) { text = "|◀◀", style = { width = 28 } };
            mPrevKeyButton = new Button(StepToPreviousKeyframe) { text = "|◀", style = { width = 28 } };
            mPlayPauseButton = new Button(TogglePlayPause) { text = "▶", style = { width = 28 } };
            mNextKeyButton = new Button(StepToNextKeyframe) { text = "▶|", style = { width = 28 } };
            mFastForwardButton = new Button(FastForwardToEnd) { text = "▶▶|", style = { width = 28 } };

            playbackButtons.Add(mRewindButton);
            playbackButtons.Add(mPrevKeyButton);
            playbackButtons.Add(mPlayPauseButton);
            playbackButtons.Add(mNextKeyButton);
            playbackButtons.Add(mFastForwardButton);

            mSimulateRuntimeButton = new Button(ToggleSimulateRuntime)
            {
                text = "SimulateRuntime",
                tooltip = "ONの場合、末尾到達時にEndBehavior(Loop/Transition)をランタイムと同じように反映します。OFFの場合は選択中のアニメーションキーを常にループ再生します",
                style = { marginLeft = 8 },
            };
            playbackButtons.Add(mSimulateRuntimeButton);
            toolbar.Add(playbackButtons);

            var rightArea = new VisualElement { style = { width = 340, flexShrink = 0, flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd, alignItems = Align.Center } };
            mFitDurationButton = new Button(() => mTimelineView?.FitDurationToView()) { text = "全体表示", tooltip = "シーケンスの長さ全体を表示領域に収めます", style = { marginRight = 4 } };
            rightArea.Add(mFitDurationButton);
            mGridWidthDropdown = new ToolbarMenu { text = "グリッド幅" };
            RefreshGridWidthMenu();
            rightArea.Add(mGridWidthDropdown);
            mFrameSnapDropdown = new ToolbarMenu { text = FormatFrameSnapLabel(mFrameSnapFps) };
            RefreshFrameSnapMenu();
            rightArea.Add(mFrameSnapDropdown);

            rightArea.Add(new Label("ステップ(秒):") { style = { unityTextAlign = TextAnchor.MiddleLeft, marginLeft = 8, marginRight = 2 } });
            mFrameStepField = new FloatField { value = mFrameStepSeconds, tooltip = "タイムラインで←/→キーを押した際に1回で進む/戻る秒数", style = { width = 50 } };
            mFrameStepField.RegisterValueChangedCallback(evt =>
            {
                mFrameStepSeconds = Mathf.Max(0.0001f, evt.newValue);
                mFrameStepField.SetValueWithoutNotify(mFrameStepSeconds);
                mTimelineView?.SetFrameStepSeconds(mFrameStepSeconds);
                SavePrefs();
            });
            rightArea.Add(mFrameStepField);
            toolbar.Add(rightArea);

            RefreshTransportButtons();
            return toolbar;
        }

        // タイムラインの1秒あたりの表示幅を選択するメニューを作る
        private void RefreshGridWidthMenu()
        {
            if (mGridWidthDropdown == null)
            {
                return;
            }

            mGridWidthDropdown.menu.MenuItems().Clear();
            foreach (float width in new[] { 40f, 80f, 120f, 180f, 240f })
            {
                float selectedWidth = width;
                mGridWidthDropdown.menu.AppendAction($"{width:0} px/秒", _ =>
                {
                    mTimelineView?.SetGridWidth(selectedWidth);
                    mGridWidthDropdown.text = $"{selectedWidth:0}px/秒";
                });
            }
        }

        // キーフレーム時刻のフレームスナップを選択するメニューを作る(「なし」「24fps」「30fps」「60fps」)
        private void RefreshFrameSnapMenu()
        {
            if (mFrameSnapDropdown == null)
            {
                return;
            }

            mFrameSnapDropdown.menu.MenuItems().Clear();
            foreach (float fps in new[] { 0f, 24f, 30f, 60f })
            {
                float selectedFps = fps;
                mFrameSnapDropdown.menu.AppendAction(FormatFrameSnapLabel(fps), _ =>
                {
                    mFrameSnapFps = selectedFps;
                    mFrameSnapDropdown.text = FormatFrameSnapLabel(selectedFps);
                    mTimelineView?.SetFrameSnapFps(selectedFps);
                    SaveSnapPrefs();
                });
            }
        }

        private static string FormatFrameSnapLabel(float aFps) => aFps > 0.0001f ? $"{aFps:0}fps" : "なし";

        // キーフレーム作成ショートカット(既定Space)を選ぶメニューを作る
        private void RefreshKeyframeShortcutMenu()
        {
            if (mKeyframeShortcutDropdown == null)
            {
                return;
            }

            mKeyframeShortcutDropdown.menu.MenuItems().Clear();
            foreach (KeyCode key in sKeyframeShortcutChoices)
            {
                KeyCode capturedKey = key;
                mKeyframeShortcutDropdown.menu.AppendAction(key.ToString(), _ =>
                {
                    mKeyframeShortcutKey = capturedKey;
                    mKeyframeShortcutDropdown.text = $"キー: {capturedKey}";
                    EditorPrefs.SetInt(PrefKeyKeyframeShortcutKey, (int)capturedKey);
                },
                _ => capturedKey == mKeyframeShortcutKey ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }
        }

        // プレビュー領域上部のツールバー。左側にPreview/Editモード切替とギズモモード切替(Editモード時のみ有効)・
        // スナップ設定、右側にUnity Game ビューの解像度選択ドロップダウンに相当するUIを配置する
        private VisualElement BuildPreviewToolbar()
        {
            var toolbar = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, justifyContent = Justify.SpaceBetween, paddingTop = 1, paddingBottom = 1, paddingLeft = 4, paddingRight = 2 }
            };

            var leftGroup = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            var modeToggleGroup = new VisualElement { style = { flexDirection = FlexDirection.Row, marginRight = 8 } };
            mPreviewModeToggleButton = new Button(() => SetPreviewEditMode(PreviewEditMode.Preview)) { text = "Preview", style = { marginRight = 2 } };
            mEditModeToggleButton = new Button(() => SetPreviewEditMode(PreviewEditMode.Edit)) { text = "Edit" };
            modeToggleGroup.Add(mPreviewModeToggleButton);
            modeToggleGroup.Add(mEditModeToggleButton);
            leftGroup.Add(modeToggleGroup);

            var gizmoModeGroup = new VisualElement { style = { flexDirection = FlexDirection.Row, marginRight = 4 } };
            mMoveModeButton = new Button(() => SetGizmoMode(GizmoMode.Move)) { text = "Move", style = { marginRight = 2 } };
            mRotateModeButton = new Button(() => SetGizmoMode(GizmoMode.Rotate)) { text = "Rotate", style = { marginRight = 2 } };
            mScaleModeButton = new Button(() => SetGizmoMode(GizmoMode.Scale)) { text = "Scale" };
            gizmoModeGroup.Add(mMoveModeButton);
            gizmoModeGroup.Add(mRotateModeButton);
            gizmoModeGroup.Add(mScaleModeButton);
            leftGroup.Add(gizmoModeGroup);

            mSnapSettingsButton = new Button(ShowSnapSettingsPopup) { text = "スナップ/グリッド" };
            leftGroup.Add(mSnapSettingsButton);

            mKeyframeShortcutDropdown = new ToolbarMenu { text = $"キー: {mKeyframeShortcutKey}", style = { marginLeft = 4 } };
            RefreshKeyframeShortcutMenu();
            leftGroup.Add(mKeyframeShortcutDropdown);

            mNudgeSettingsButton = new Button(ShowNudgeSettingsPopup) { text = "キーボード操作設定", style = { marginLeft = 4 } };
            leftGroup.Add(mNudgeSettingsButton);

            toolbar.Add(leftGroup);

            mAspectDropdown = new ToolbarMenu { text = mSelectedAspectPreset.Name };
            RefreshAspectDropdownMenu();
            toolbar.Add(mAspectDropdown);

            RefreshPreviewEditModeButtons();
            RefreshGizmoModeButtons();
            return toolbar;
        }

        // aMode : 切り替え先の画面モード。RebuildUI()を呼び直して画面全体を切り替える
        private void SetWindowMode(WindowMode aMode)
        {
            if (mWindowMode == aMode)
            {
                return;
            }
            mWindowMode = aMode;
            RebuildUI();
        }

        private void RefreshWindowModeButtons()
        {
            mAnimationEditModeButton?.EnableInClassList("anim-seq-gizmo-mode--active", mWindowMode == WindowMode.AnimationEdit);
            mObjectPlacementModeButton?.EnableInClassList("anim-seq-gizmo-mode--active", mWindowMode == WindowMode.ObjectPlacement);
        }

        // aMode : 切り替え先のモード(Preview=表示専用/Edit=ギズモ・グリッド編集)
        private void SetPreviewEditMode(PreviewEditMode aMode)
        {
            if (mPreviewEditMode == aMode)
            {
                return;
            }
            mPreviewEditMode = aMode;

            if (aMode == PreviewEditMode.Edit)
            {
                // 編集中に映像が動き続けないよう、Editモードへ入る際は一時停止する
                // (トランスポートバーのPlayボタンは引き続き使えるため、必要ならそこから再生できる)
                EnsurePreviewLoaded();
                mIsPreviewPaused = true;
                mLastEditorTime = EditorApplication.timeSinceStartup;
                RefreshPlayingKeyLabel();
            }
            else
            {
                mPreviewHost?.ClearGizmoSelection();
            }

            RefreshPreviewEditModeButtons();
            RefreshGizmoModeButtons();
            mPreviewContainer?.MarkDirtyRepaint();
        }

        private void RefreshPreviewEditModeButtons()
        {
            mPreviewModeToggleButton?.EnableInClassList("anim-seq-gizmo-mode--active", mPreviewEditMode == PreviewEditMode.Preview);
            mEditModeToggleButton?.EnableInClassList("anim-seq-mode-edit--active", mPreviewEditMode == PreviewEditMode.Edit);
        }

        private void ShowSnapSettingsPopup()
        {
            var popup = new SnapSettingsPopupContent(mMoveSnapValue, mRotateSnapValue, mScaleSnapValue, mPreviewGridSpacing, (aMove, aRotate, aScale, aGridSpacing) =>
            {
                mMoveSnapValue = Mathf.Max(0f, aMove);
                mRotateSnapValue = Mathf.Max(0f, aRotate);
                mScaleSnapValue = Mathf.Max(0f, aScale);
                mPreviewGridSpacing = Mathf.Max(0f, aGridSpacing);
                SaveSnapPrefs();
                mPreviewContainer?.MarkDirtyRepaint();
            });
            UnityEditor.PopupWindow.Show(new Rect(mSnapSettingsButton.worldBound.x, mSnapSettingsButton.worldBound.yMax, 0, 0), popup);
        }

        // 「スナップ」ボタンから開く、移動/回転/拡縮それぞれのスナップ間隔を設定する簡易ポップアップ
        private class SnapSettingsPopupContent : PopupWindowContent
        {
            private readonly Action<float, float, float, float> mOnChanged;
            private float mMove;
            private float mRotate;
            private float mScale;
            private float mGridSpacing;

            public SnapSettingsPopupContent(float aMove, float aRotate, float aScale, float aGridSpacing, Action<float, float, float, float> aOnChanged)
            {
                mMove = aMove;
                mRotate = aRotate;
                mScale = aScale;
                mGridSpacing = aGridSpacing;
                mOnChanged = aOnChanged;
            }

            public override Vector2 GetWindowSize() => new(240, 112);

            public override void OnGUI(Rect aRect)
            {
                GUILayout.Label("ギズモのスナップ・グリッド設定(0で無効)", EditorStyles.boldLabel);
                float newMove = EditorGUILayout.FloatField("移動 (単位)", mMove);
                float newRotate = EditorGUILayout.FloatField("回転 (度)", mRotate);
                float newScale = EditorGUILayout.FloatField("拡縮 (倍率)", mScale);
                float newGridSpacing = EditorGUILayout.FloatField("プレビューグリッド (単位)", mGridSpacing);
                if (!Mathf.Approximately(newMove, mMove) || !Mathf.Approximately(newRotate, mRotate) || !Mathf.Approximately(newScale, mScale) || !Mathf.Approximately(newGridSpacing, mGridSpacing))
                {
                    mMove = Mathf.Max(0f, newMove);
                    mRotate = Mathf.Max(0f, newRotate);
                    mScale = Mathf.Max(0f, newScale);
                    mGridSpacing = Mathf.Max(0f, newGridSpacing);
                    mOnChanged?.Invoke(mMove, mRotate, mScale, mGridSpacing);
                }
            }
        }

        private void ShowNudgeSettingsPopup()
        {
            var popup = new NudgeSettingsPopupContent(mNudgeKeyBindings, (aAction, aKey) =>
            {
                mNudgeKeyBindings[aAction] = aKey;
                SaveNudgeKeyPrefs();
            });
            UnityEditor.PopupWindow.Show(new Rect(mNudgeSettingsButton.worldBound.x, mNudgeSettingsButton.worldBound.yMax, 0, 0), popup);
        }

        // 「キーボード操作設定」ボタンから開く、Editモードでの選択中トラックへのキーボードNudge(移動/回転/拡縮)の
        // キー割り当てを変更する簡易ポップアップ
        private class NudgeSettingsPopupContent : PopupWindowContent
        {
            private static readonly (KeyboardNudgeAction Action, string Label)[] sRows =
            {
                (KeyboardNudgeAction.MoveUp, "移動: 上"), (KeyboardNudgeAction.MoveDown, "移動: 下"),
                (KeyboardNudgeAction.MoveLeft, "移動: 左"), (KeyboardNudgeAction.MoveRight, "移動: 右"),
                (KeyboardNudgeAction.RotateCcw, "回転: 反時計回り"), (KeyboardNudgeAction.RotateCw, "回転: 時計回り"),
                (KeyboardNudgeAction.ScaleUp, "拡縮: 拡大"), (KeyboardNudgeAction.ScaleDown, "拡縮: 縮小"),
            };

            private readonly Dictionary<KeyboardNudgeAction, KeyCode> mBindings;
            private readonly Action<KeyboardNudgeAction, KeyCode> mOnChanged;

            public NudgeSettingsPopupContent(Dictionary<KeyboardNudgeAction, KeyCode> aBindings, Action<KeyboardNudgeAction, KeyCode> aOnChanged)
            {
                mBindings = new Dictionary<KeyboardNudgeAction, KeyCode>(aBindings);
                mOnChanged = aOnChanged;
            }

            public override Vector2 GetWindowSize() => new(240, 24 * sRows.Length + 30);

            public override void OnGUI(Rect aRect)
            {
                GUILayout.Label("キーボード操作のキー割り当て", EditorStyles.boldLabel);
                foreach ((KeyboardNudgeAction action, string label) in sRows)
                {
                    var newKey = (KeyCode)EditorGUILayout.EnumPopup(label, mBindings[action]);
                    if (newKey != mBindings[action])
                    {
                        mBindings[action] = newKey;
                        mOnChanged?.Invoke(action, newKey);
                    }
                }
            }
        }

        // aMode : 選択するギズモの種類(移動/回転/拡大縮小)
        private void SetGizmoMode(GizmoMode aMode)
        {
            mGizmoMode = aMode;
            RefreshGizmoModeButtons();
            mPreviewContainer?.MarkDirtyRepaint();
        }

        // ギズモモードボタンの選択中ハイライトと、Editモード以外での無効化を反映する
        private void RefreshGizmoModeButtons()
        {
            bool isEditMode = mPreviewEditMode == PreviewEditMode.Edit;
            mMoveModeButton?.SetEnabled(isEditMode);
            mRotateModeButton?.SetEnabled(isEditMode);
            mScaleModeButton?.SetEnabled(isEditMode);
            mSnapSettingsButton?.SetEnabled(isEditMode);

            mMoveModeButton?.EnableInClassList("anim-seq-gizmo-mode--active", mGizmoMode == GizmoMode.Move);
            mRotateModeButton?.EnableInClassList("anim-seq-gizmo-mode--active", mGizmoMode == GizmoMode.Rotate);
            mScaleModeButton?.EnableInClassList("anim-seq-gizmo-mode--active", mGizmoMode == GizmoMode.Scale);
        }

        private void RefreshAspectDropdownMenu()
        {
            mAspectDropdown.menu.MenuItems().Clear();

            foreach (AspectPreset preset in sBuiltInPresets)
            {
                AspectPreset capturedPreset = preset;
                mAspectDropdown.menu.AppendAction(preset.Name, _ => SelectAspectPreset(capturedPreset),
                    _ => capturedPreset.Name == mSelectedAspectPreset.Name ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }

            if (mCustomPresets.Count > 0)
            {
                mAspectDropdown.menu.AppendSeparator();
                foreach (AspectPreset preset in mCustomPresets)
                {
                    AspectPreset capturedPreset = preset;
                    mAspectDropdown.menu.AppendAction(preset.Name, _ => SelectAspectPreset(capturedPreset),
                        _ => capturedPreset.Name == mSelectedAspectPreset.Name ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
                }
            }

            mAspectDropdown.menu.AppendSeparator();
            mAspectDropdown.menu.AppendAction("+ カスタムアスペクト比を追加...", _ => ShowAddCustomAspectPopup());
        }

        private void SelectAspectPreset(AspectPreset aPreset)
        {
            mSelectedAspectPreset = aPreset;
            mAspectDropdown.text = aPreset.Name;
            SaveAspectPrefs();
            mPreviewContainer?.MarkDirtyRepaint();
        }

        private void ShowAddCustomAspectPopup()
        {
            var popup = new AddCustomAspectPopupContent((aName, aWidth, aHeight) =>
            {
                var preset = new AspectPreset(string.IsNullOrEmpty(aName) ? $"{aWidth}x{aHeight}" : aName, aWidth, aHeight);
                mCustomPresets.Add(preset);
                RefreshAspectDropdownMenu();
                SelectAspectPreset(preset);
            });
            UnityEditor.PopupWindow.Show(new Rect(mAspectDropdown.worldBound.x, mAspectDropdown.worldBound.yMax, 0, 0), popup);
        }

        // 「+」から開くカスタムアスペクト比追加用の簡易ポップアップ
        private class AddCustomAspectPopupContent : PopupWindowContent
        {
            private readonly Action<string, float, float> mOnAdd;
            private string mName = "Custom";
            private int mWidth = 1920;
            private int mHeight = 1080;

            public AddCustomAspectPopupContent(Action<string, float, float> aOnAdd) => mOnAdd = aOnAdd;

            public override Vector2 GetWindowSize() => new(220, 100);

            public override void OnGUI(Rect aRect)
            {
                GUILayout.Label("カスタムアスペクト比を追加", EditorStyles.boldLabel);
                mName = EditorGUILayout.TextField("名前", mName);
                mWidth = EditorGUILayout.IntField("幅", mWidth);
                mHeight = EditorGUILayout.IntField("高さ", mHeight);
                using (new EditorGUI.DisabledScope(mWidth <= 0 || mHeight <= 0))
                {
                    if (GUILayout.Button("追加"))
                    {
                        mOnAdd?.Invoke(mName, mWidth, mHeight);
                        editorWindow.Close();
                    }
                }
            }
        }

        // ===== 選択変化 =====

        private void OnEntrySelectionChanged(SerializedProperty aEntryProperty)
        {
            mSelectedEntryProperty = aEntryProperty;
            mSelectedEntryKey = aEntryProperty?.FindPropertyRelative("mKey").stringValue;
            AnimSequenceEntry entry = aEntryProperty != null && mTarget != null
                ? mTarget.FindEntry(aEntryProperty.FindPropertyRelative("mKey").stringValue)
                : null;

            // Mute/Soloはセッション限定・エントリ切り替えでリセットする仕様(SPEC.md)のため、
            // 選択中のアニメーションキーが変わったタイミングでクリアする
            if (entry?.Key != mLastMuteSoloEntryKey)
            {
                mPreviewHost?.MutedTrackIds.Clear();
                mPreviewHost?.SoloedTrackIds.Clear();
                mLastMuteSoloEntryKey = entry?.Key;
            }

            mTimelineView?.SetEntryProperty(aEntryProperty);

            if (mEntryListView != null)
            {
                mIsSynchronizingEntryListSelection = true;
                mEntryListView.selectedIndex = FindEntryListIndex(aEntryProperty);
                mIsSynchronizingEntryListSelection = false;
            }

            if (mIsPreviewPlaying && mPreviewPlayback != null && entry != null && mPreviewPlayback.CurrentKey != entry.Key)
            {
                // Transition(SimulateRuntime ON時)等で実際の再生対象と選択中のキーが食い違った状態のまま
                // 別のキーへ選択を切り替えた場合、再生中のプレビューをそのまま選択中のキーへ切り替える
                mPreviewPlayback.PlaySequence(entry.Key);
                mTimelineView?.SetPlayheadTime(mPreviewPlayback.CurrentTime);
                RefreshPlayingKeyLabel();
                mPreviewContainer?.MarkDirtyRepaint();
            }
            else if (!mIsPreviewPlaying)
            {
                // 未読み込み時のみアイドル表示を選択中エントリへ切り替える(再生・スクラブ中は対象の見た目を保持する)
                mPreviewHost?.SetTargetEntry(entry);
                mPreviewContainer?.MarkDirtyRepaint();
            }

            RefreshTransportButtons();
        }

        // aEntryPropertyに対応する一覧のインデックスを返す。未選択は-1
        private int FindEntryListIndex(SerializedProperty aEntryProperty)
        {
            if (aEntryProperty == null)
            {
                return -1;
            }

            // 検索フィルタ後のmEntryListItems(itemsSourceそのもの)を走査する。フィルタで除外されている場合は-1のままでよい
            string key = aEntryProperty.FindPropertyRelative("mKey").stringValue;
            for (int index = 0; index < mEntryListItems.Count; index++)
            {
                if (mEntryListItems[index].Key == key)
                {
                    return index;
                }
            }
            return -1;
        }

        // キーフレーム/イベントキーの選択が変化した際にInspectorへ反映する。nullの場合はエントリ自体を表示する
        private void OnKeyframeSelectionChanged(SerializedProperty aKeyframeProperty)
        {
            mInspectorPanel?.SetTargetProperty(aKeyframeProperty ?? mSelectedEntryProperty, aKeyframeProperty != null);

            // SerializedProperty自体は並べ替え・再構築で別の要素を指しうるため、時刻の値だけを保持する
            mSelectedKeyframeTime = aKeyframeProperty?.FindPropertyRelative("mTime")?.floatValue;
            RefreshPlayingKeyLabel(); // 選択状態の表示を更新する
            if (mSelectedKeyframeTime == null)
            {
                return;
            }

            // 選択中キーフレームの時刻の見た目をプレビューへ反映する。再生中に引き戻してしまわないよう、
            // 一時停止中(スクラブ・編集中)またはプレビュー未読み込みの場合のみ行う
            if (mIsPreviewPlaying && !mIsPreviewPaused)
            {
                return;
            }
            ScrubToTime(mSelectedKeyframeTime.Value);
            mTimelineView?.SetPlayheadTime(mSelectedKeyframeTime.Value);
        }

        // Inspectorで選択中キーフレームの時刻が変更された際に呼ぶ。Inspectorでの編集はタイムラインの再構築
        // (=OnKeyframeSelectionChangedによる選択変更通知)を経ないため、ギズモ編集の書き込み先・プレビュー表示位置・
        // 状態表示が編集前の時刻のまま取り残される。ここで新しい時刻へ明示的に追従させる
        // aTime : 変更後の時刻(秒)
        private void OnSelectedKeyframeTimeChanged(float aTime)
        {
            if (mSelectedKeyframeTime == null)
            {
                return; // キーフレーム未選択(エントリ自体の編集)時は追従対象が無い
            }

            mSelectedKeyframeTime = aTime;
            RefreshPlayingKeyLabel();

            // 再生中に表示を引き戻さないよう、一時停止中(スクラブ・編集中)の場合のみプレビューを移動する
            if (mIsPreviewPlaying && !mIsPreviewPaused)
            {
                return;
            }
            ScrubToTime(aTime);
            mTimelineView?.SetPlayheadTime(aTime);
        }

        // Inspectorでアニメーションキー名がリネームされた際に、グラフのノードタイトルへ同期する
        // aOldKey : リネーム前のキー / aNewKey : リネーム後のキー
        private void OnKeyRenamed(string aOldKey, string aNewKey)
        {
            mEntryGraphView?.RefreshNodeTitle(aOldKey, aNewKey);
            RefreshWarnings();
        }

        // ===== 警告表示 =====

        private void RefreshWarnings()
        {
            if (mDuplicateKeyWarningBox != null)
            {
                bool showDuplicate = mTarget != null && mTarget.HasDuplicateKeys();
                mDuplicateKeyWarningBox.style.display = showDuplicate ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (mInvalidTransitionWarningBox != null)
            {
                List<string> invalidKeys = mTarget != null ? mTarget.CollectInvalidTransitionKeys() : new List<string>();
                bool showInvalid = invalidKeys.Count > 0;
                mInvalidTransitionWarningBox.style.display = showInvalid ? DisplayStyle.Flex : DisplayStyle.None;
                if (showInvalid)
                {
                    mInvalidTransitionWarningBox.text = $"存在しない遷移先キーを指すエントリがあります: {string.Join(", ", invalidKeys)}";
                }
            }

            if (mInvalidMaterialParameterWarningBox != null)
            {
                List<string> invalidParams = mTarget != null ? mTarget.CollectInvalidMaterialParameterTracks() : new List<string>();
                bool showInvalidParams = invalidParams.Count > 0;
                mInvalidMaterialParameterWarningBox.style.display = showInvalidParams ? DisplayStyle.Flex : DisplayStyle.None;
                if (showInvalidParams)
                {
                    mInvalidMaterialParameterWarningBox.text = $"基準Materialに存在しないプロパティを指すMaterialパラメータがあります: {string.Join(", ", invalidParams)}";
                }
            }

            if (mInvalidObjectReferenceWarningBox != null)
            {
                List<string> invalidRefs = mTarget != null ? mTarget.CollectInvalidObjectReferences() : new List<string>();
                bool showInvalidRefs = invalidRefs.Count > 0;
                mInvalidObjectReferenceWarningBox.style.display = showInvalidRefs ? DisplayStyle.Flex : DisplayStyle.None;
                if (showInvalidRefs)
                {
                    mInvalidObjectReferenceWarningBox.text = $"存在しないオブジェクトを参照しているトラックがあります: {string.Join(", ", invalidRefs)}";
                }
            }
        }

        // ===== 初期配置画面 =====

        // アニメーションキーをまたいで共有するオブジェクトを配置・編集する画面のルートを組み立てる
        private VisualElement BuildObjectPlacementScreen()
        {
            mPreviewHost.LoadObjectsForPlacement(mTarget.Objects);
            mSelectedPlacementObjectId = null;

            var canvasArea = new VisualElement { style = { flexDirection = FlexDirection.Column, flexGrow = 1, borderRightWidth = 1 } };
            canvasArea.Add(new HelpBox("Spriteアセットをキャンバスへドラッグ&ドロップして配置してください", HelpBoxMessageType.Info));

            var gizmoToolbar = new VisualElement { style = { flexDirection = FlexDirection.Row, paddingTop = 2, paddingBottom = 2, paddingLeft = 4 } };
            mPlacementMoveModeButton = new Button(() => SetPlacementGizmoMode(GizmoMode.Move)) { text = "Move", style = { marginRight = 2 } };
            mPlacementRotateModeButton = new Button(() => SetPlacementGizmoMode(GizmoMode.Rotate)) { text = "Rotate", style = { marginRight = 2 } };
            mPlacementScaleModeButton = new Button(() => SetPlacementGizmoMode(GizmoMode.Scale)) { text = "Scale", style = { marginRight = 8 } };
            gizmoToolbar.Add(mPlacementMoveModeButton);
            gizmoToolbar.Add(mPlacementRotateModeButton);
            gizmoToolbar.Add(mPlacementScaleModeButton);

            // アニメーション編集画面と同じスナップ/キーボードNudge設定ポップアップを共有する(ボタン参照フィールド・
            // 表示メソッドともに共通のものを再利用し、この画面用に生成し直すだけでよい)
            mSnapSettingsButton = new Button(ShowSnapSettingsPopup) { text = "スナップ/グリッド", style = { marginRight = 4 } };
            mNudgeSettingsButton = new Button(ShowNudgeSettingsPopup) { text = "キーボード操作設定" };
            gizmoToolbar.Add(mSnapSettingsButton);
            gizmoToolbar.Add(mNudgeSettingsButton);
            canvasArea.Add(gizmoToolbar);
            RefreshPlacementGizmoModeButtons();

            mObjectPlacementContainer = new IMGUIContainer(DrawObjectPlacementIMGUI)
            {
                style = { flexGrow = 1 },
                focusable = true,
            };
            canvasArea.Add(mObjectPlacementContainer);
            // Spriteブラウザはプレビューウィンドウの下に配置する(サイド側は一覧・詳細のみにして縦を広く使う)
            canvasArea.Add(BuildSpriteBrowserPanel());

            var sidePanel = new VisualElement { style = { flexDirection = FlexDirection.Column } };
            sidePanel.Add(BuildObjectListPanel());
            mObjectDetailsContainer = new VisualElement { style = { flexGrow = 1, borderTopWidth = 1 } };
            sidePanel.Add(mObjectDetailsContainer);
            RefreshObjectDetailsPanel();

            var mainSplit = new TwoPaneSplitView(1, mCurrentInspectorWidth, TwoPaneSplitViewOrientation.Horizontal) { style = { flexGrow = 1 } };
            mainSplit.Add(canvasArea);
            mainSplit.Add(sidePanel);
            return mainSplit;
        }

        // aMode : 選択するギズモの種類。アニメーション編集画面のmGizmoModeと同じフィールドを共有する
        private void SetPlacementGizmoMode(GizmoMode aMode)
        {
            mGizmoMode = aMode;
            RefreshPlacementGizmoModeButtons();
            mObjectPlacementContainer?.MarkDirtyRepaint();
        }

        private void RefreshPlacementGizmoModeButtons()
        {
            mPlacementMoveModeButton?.EnableInClassList("anim-seq-gizmo-mode--active", mGizmoMode == GizmoMode.Move);
            mPlacementRotateModeButton?.EnableInClassList("anim-seq-gizmo-mode--active", mGizmoMode == GizmoMode.Rotate);
            mPlacementScaleModeButton?.EnableInClassList("anim-seq-gizmo-mode--active", mGizmoMode == GizmoMode.Scale);
        }

        private void DrawObjectPlacementIMGUI()
        {
            Rect rect = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (mPreviewHost == null)
            {
                return;
            }

            mPreviewHost.DrawPreview(rect, mSelectedAspectPreset.AspectRatioOrZero);
            mPreviewHost.DrawGrid(rect, mSelectedAspectPreset.AspectRatioOrZero, mPreviewGridSpacing);

            // アニメーション編集画面のDrawPreviewIMGUIと同じく、ギズモ操作を先に処理してからフォーカスを移す。
            // Focus()を先に呼ぶとUI Toolkit側のフォーカス移動で処理中のIMGUIイベントが失われ、キャンバスが
            // 未フォーカスの状態からの1回目のクリックでギズモをつかめないため
            bool isMouseDown = Event.current.type == EventType.MouseDown;

            var snap = new GizmoSnapSettings { MoveSnap = mMoveSnapValue, RotateSnap = mRotateSnapValue, ScaleSnap = mScaleSnapValue };
            if (mPreviewHost.HandleGizmoInput(rect, mSelectedAspectPreset.AspectRatioOrZero, mGizmoMode, snap))
            {
                WriteBackPlacementEdit();
                mObjectPlacementContainer?.MarkDirtyRepaint();
            }

            if (isMouseDown)
            {
                mObjectPlacementContainer?.Focus();
            }

            // 選択中のギズモモードに関係なく常に有効なキーボードNudge(既定WASD/QE/RF)。アニメーション編集画面と同じ
            // HandleNudgeKeyDownをそのまま使う(内部でmPreviewHostの表示状態を直接書き換えるだけのため流用できる)
            if (Event.current.type == EventType.KeyDown && HandleNudgeKeyDown(Event.current.keyCode))
            {
                Event.current.Use();
                WriteBackPlacementEdit();
                mObjectPlacementContainer?.MarkDirtyRepaint();
            }

            // キャンバス上のクリックによる選択変更はPreviewAnimSequenceHost内部で完結するため、
            // 毎フレームSelectedTrackIdと比較して一覧・詳細パネル側の表示同期のトリガーにする
            if (mPreviewHost.SelectedTrackId != mSelectedPlacementObjectId)
            {
                mSelectedPlacementObjectId = mPreviewHost.SelectedTrackId;
                RefreshObjectDetailsPanel();
                SyncObjectListSelection();
            }

            if (mPreviewHost.HandleObjectDrop(rect, mSelectedAspectPreset.AspectRatioOrZero, out Sprite droppedSprite, out Vector2 dropPosition))
            {
                CreateObjectAtDropPosition(droppedSprite, dropPosition);
            }
        }

        // ギズモ操作で変化した選択中オブジェクトの値を、即座にAnimSequenceObjectのSerializedPropertyへ書き戻す
        // (アニメーションキー編集画面のSpaceキー確定と異なり、初期配置画面はキーフレームを持たないため即時反映する)
        private void WriteBackPlacementEdit()
        {
            string objectId = mPreviewHost?.SelectedTrackId;
            if (objectId == null || !mPreviewHost.TryGetTrackState(objectId, out AnimSequenceTrackState state))
            {
                return;
            }

            SerializedProperty objectProperty = FindObjectProperty(objectId);
            if (objectProperty == null)
            {
                return;
            }

            objectProperty.FindPropertyRelative("mPosition").vector2Value = state.AnchoredPosition;
            objectProperty.FindPropertyRelative("mScale").vector2Value = state.Scale;
            objectProperty.FindPropertyRelative("mRotation").vector3Value = state.Rotation;
            mSerializedObject.ApplyModifiedProperties();
        }

        // aObjectId : 検索するオブジェクトID / 戻り値 : mObjects内の該当SerializedProperty。見つからなければnull
        private SerializedProperty FindObjectProperty(string aObjectId)
        {
            SerializedProperty objectsProperty = mSerializedObject.FindProperty("mObjects");
            for (int i = 0; i < objectsProperty.arraySize; i++)
            {
                SerializedProperty obj = objectsProperty.GetArrayElementAtIndex(i);
                if (obj.FindPropertyRelative("mObjectId").stringValue == aObjectId)
                {
                    return obj;
                }
            }
            return null;
        }

        // aSprite : ドロップされたSprite / aPosition : ドロップ位置(基準Position)
        private void CreateObjectAtDropPosition(Sprite aSprite, Vector2 aPosition)
        {
            SerializedProperty objectsProperty = mSerializedObject.FindProperty("mObjects");
            int index = objectsProperty.arraySize;
            objectsProperty.InsertArrayElementAtIndex(index);
            SerializedProperty obj = objectsProperty.GetArrayElementAtIndex(index);

            string objectId = MakeUniqueObjectId(aSprite.name, null);
            obj.FindPropertyRelative("mObjectId").stringValue = objectId;
            obj.FindPropertyRelative("mSprite").objectReferenceValue = aSprite;
            obj.FindPropertyRelative("mPosition").vector2Value = aPosition;
            obj.FindPropertyRelative("mScale").vector2Value = Vector2.one;
            obj.FindPropertyRelative("mRotation").vector3Value = Vector3.zero;
            obj.FindPropertyRelative("mColor").colorValue = Color.white;
            obj.FindPropertyRelative("mBaseMaterial").objectReferenceValue = null;
            obj.FindPropertyRelative("mInstantiateMaterial").boolValue = false;

            mSerializedObject.ApplyModifiedProperties();
            mPreviewHost.LoadObjectsForPlacement(mTarget.Objects);
            RefreshObjectListItems();
            mObjectListView?.RefreshItems();
            SelectPlacementObject(objectId);
        }

        // aDesiredId : 希望するID(空ならInitialな既定名を使う) / aIgnoreId : 重複判定から除外するID(リネーム時、自分自身を除外するため。新規作成時はnull)
        private string MakeUniqueObjectId(string aDesiredId, string aIgnoreId)
        {
            string baseName = string.IsNullOrEmpty(aDesiredId) ? "Object" : aDesiredId;
            string candidate = baseName;
            int suffix = 1;
            while (IsObjectIdUsed(candidate, aIgnoreId))
            {
                candidate = $"{baseName}_{suffix}";
                suffix++;
            }
            return candidate;
        }

        private bool IsObjectIdUsed(string aObjectId, string aIgnoreId)
        {
            foreach (AnimSequenceObject obj in mTarget.Objects)
            {
                if (obj.ObjectId == aIgnoreId)
                {
                    continue;
                }
                if (obj.ObjectId == aObjectId)
                {
                    return true;
                }
            }
            return false;
        }

        // オブジェクトIDのリネームを全アニメーションキーのトラック参照へ反映する
        // aOldId : リネーム前のID / aNewId : リネーム後のID
        private void RenameObjectReferences(string aOldId, string aNewId)
        {
            SerializedProperty entriesProperty = mSerializedObject.FindProperty("mEntries");
            for (int e = 0; e < entriesProperty.arraySize; e++)
            {
                SerializedProperty tracksProperty = entriesProperty.GetArrayElementAtIndex(e).FindPropertyRelative("mTracks");
                for (int t = 0; t < tracksProperty.arraySize; t++)
                {
                    SerializedProperty trackIdProperty = tracksProperty.GetArrayElementAtIndex(t).FindPropertyRelative("mTrackId");
                    if (trackIdProperty.stringValue == aOldId)
                    {
                        trackIdProperty.stringValue = aNewId;
                    }
                }
            }
        }

        // プロジェクト内のSpriteアセットを検索・サムネイル表示し、キャンバスへドラッグして配置できるようにする
        // (Projectウィンドウを別途開かずに済むようにするための埋め込みブラウザ)
        private VisualElement BuildSpriteBrowserPanel()
        {
            var container = new VisualElement { style = { height = 220, flexShrink = 0, borderBottomWidth = 1, flexDirection = FlexDirection.Column } };
            container.Add(new Label("Spriteブラウザ(ドラッグしてキャンバスへ配置)") { style = { height = 20, unityTextAlign = TextAnchor.MiddleLeft, paddingLeft = 6 } });

            var searchField = new TextField { style = { marginLeft = 4, marginRight = 4, marginBottom = 2 } };
            var scrollView = new ScrollView { style = { flexGrow = 1 } };
            var itemsContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            scrollView.Add(itemsContainer);

            RefreshSpriteBrowserItems(itemsContainer, string.Empty);
            searchField.RegisterValueChangedCallback(evt => RefreshSpriteBrowserItems(itemsContainer, evt.newValue));

            container.Add(searchField);
            container.Add(scrollView);
            return container;
        }

        // aFilter : Sprite名の部分一致フィルタ(空文字列で全件)
        private void RefreshSpriteBrowserItems(VisualElement aItemsContainer, string aFilter)
        {
            aItemsContainer.Clear();

            string query = string.IsNullOrEmpty(aFilter) ? "t:Sprite" : $"t:Sprite {aFilter}";
            string[] guids = AssetDatabase.FindAssets(query);
            // 大規模プロジェクトでの表示負荷対策として表示件数に上限を設ける(検索で絞り込めば目的のSpriteは十分見つけられる)
            int count = Mathf.Min(guids.Length, 200);
            for (int i = 0; i < count; i++)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (sprite == null)
                {
                    continue;
                }
                aItemsContainer.Add(BuildSpriteBrowserItem(sprite));
            }
        }

        // aSprite : サムネイル表示・ドラッグ元にするSprite
        private VisualElement BuildSpriteBrowserItem(Sprite aSprite)
        {
            var item = new VisualElement { style = { width = 56, height = 72, marginRight = 2, marginBottom = 2, alignItems = Align.Center } };

            Texture2D thumbnail = AssetPreview.GetAssetPreview(aSprite);
            var thumbnailImage = new Image { image = thumbnail != null ? thumbnail : AssetPreview.GetMiniThumbnail(aSprite), style = { width = 48, height = 48 } };
            item.Add(thumbnailImage);
            item.Add(new Label(aSprite.name) { style = { fontSize = 9, unityTextAlign = TextAnchor.UpperCenter, whiteSpace = WhiteSpace.Normal, width = 56 } });

            if (thumbnail == null)
            {
                // AssetPreview.GetAssetPreviewは実プレビューの生成が非同期で、初回呼び出し時はnullが返ることがある。
                // 生成が終わるまで一定間隔で再取得し、実際のSprite画像に置き換える(それまでは簡易アイコンを暫定表示)
                int instanceId = aSprite.GetInstanceID();
                thumbnailImage.schedule.Execute(() =>
                {
                    Texture2D preview = AssetPreview.GetAssetPreview(aSprite);
                    if (preview != null)
                    {
                        thumbnailImage.image = preview;
                    }
                }).Every(100).Until(() => AssetPreview.GetAssetPreview(aSprite) != null || !AssetPreview.IsLoadingAssetPreview(instanceId));
            }

            // 一定距離以上ポインタが動いたらドラッグ開始とみなす(単純なクリックと区別するため)
            bool isPressed = false;
            Vector2 pointerDownPosition = Vector2.zero;
            item.RegisterCallback<PointerDownEvent>(evt =>
            {
                isPressed = true;
                pointerDownPosition = evt.position;
            });
            item.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!isPressed || Vector2.Distance(evt.position, pointerDownPosition) < 4f)
                {
                    return;
                }
                isPressed = false;
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new UnityEngine.Object[] { aSprite };
                DragAndDrop.StartDrag(aSprite.name);
            });
            item.RegisterCallback<PointerUpEvent>(_ => isPressed = false);
            return item;
        }

        private VisualElement BuildObjectListPanel()
        {
            var container = new VisualElement { style = { height = 200, flexShrink = 0, borderBottomWidth = 1, flexDirection = FlexDirection.Column } };
            container.Add(new Label("オブジェクト") { style = { height = 20, unityTextAlign = TextAnchor.MiddleLeft, paddingLeft = 6 } });

            RefreshObjectListItems();

            mObjectListView = new ListView
            {
                itemsSource = mObjectListItems,
                selectionType = SelectionType.Single,
                fixedItemHeight = 20,
                style = { flexGrow = 1 }
            };
            mObjectListView.makeItem = () => new Label();
            mObjectListView.bindItem = (element, index) => ((Label)element).text = mObjectListItems[index].ObjectId;
            mObjectListView.selectionChanged += selectedItems =>
            {
                if (selectedItems.FirstOrDefault() is not AnimSequenceObject obj)
                {
                    return;
                }
                SelectPlacementObject(obj.ObjectId);
            };
            container.Add(mObjectListView);
            return container;
        }

        private void RefreshObjectListItems()
        {
            mObjectListItems.Clear();
            if (mTarget == null)
            {
                return;
            }
            foreach (AnimSequenceObject obj in mTarget.Objects)
            {
                mObjectListItems.Add(obj);
            }
        }

        // 一覧・キャンバスどちらから選んでも同じ経路を通り、両方の表示を同期させる
        // aObjectId : 選択するオブジェクトID
        private void SelectPlacementObject(string aObjectId)
        {
            mPreviewHost?.SelectTrack(aObjectId);
            mSelectedPlacementObjectId = aObjectId;
            RefreshObjectDetailsPanel();
            SyncObjectListSelection();
            mObjectPlacementContainer?.MarkDirtyRepaint();
        }

        private void SyncObjectListSelection()
        {
            if (mObjectListView == null)
            {
                return;
            }
            int index = mObjectListItems.FindIndex(o => o.ObjectId == mSelectedPlacementObjectId);
            if (mObjectListView.selectedIndex != index)
            {
                mObjectListView.selectedIndex = index;
            }
        }

        // 選択中オブジェクトの全フィールドを、既存のInspectorパネルと同じPropertyField汎用反復で表示する。
        // オブジェクトIDのみリネーム時の参照追従(RenameObjectReferences)を伴うため専用フィールドとして扱う
        private void RefreshObjectDetailsPanel()
        {
            if (mObjectDetailsContainer == null)
            {
                return;
            }
            mObjectDetailsContainer.Clear();

            SerializedProperty objectProperty = mSelectedPlacementObjectId != null ? FindObjectProperty(mSelectedPlacementObjectId) : null;
            if (objectProperty == null)
            {
                mObjectDetailsContainer.Add(new Label("オブジェクトを選択してください") { style = { paddingTop = 8, paddingLeft = 8 } });
                return;
            }

            SerializedProperty idProperty = objectProperty.FindPropertyRelative("mObjectId");
            string currentId = idProperty.stringValue;
            var idField = new TextField("オブジェクトID") { value = currentId, isDelayed = true };
            idField.RegisterValueChangedCallback(evt =>
            {
                string oldId = currentId;
                string uniqueId = MakeUniqueObjectId(evt.newValue, oldId);
                idProperty.stringValue = uniqueId;
                RenameObjectReferences(oldId, uniqueId);
                mSerializedObject.ApplyModifiedProperties();
                if (uniqueId != evt.newValue)
                {
                    idField.SetValueWithoutNotify(uniqueId);
                }
                currentId = uniqueId;
                mSelectedPlacementObjectId = uniqueId;
                mPreviewHost.LoadObjectsForPlacement(mTarget.Objects);
                mPreviewHost.SelectTrack(uniqueId);
                RefreshObjectListItems();
                mObjectListView?.RefreshItems();
                SyncObjectListSelection();
                RefreshWarnings();
                mObjectPlacementContainer?.MarkDirtyRepaint();
            });
            mObjectDetailsContainer.Add(idField);

            string objectIdForDelete = currentId;
            var deleteButton = new Button(() => DeletePlacementObject(objectIdForDelete)) { text = "削除", style = { marginTop = 4, marginBottom = 4 } };
            mObjectDetailsContainer.Add(deleteButton);

            SerializedProperty iterator = objectProperty.Copy();
            SerializedProperty end = objectProperty.GetEndProperty();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                if (iterator.name == "mObjectId")
                {
                    continue; // 上のidFieldで専用表示しているため重複させない
                }

                var field = new PropertyField(iterator.Copy());
                field.Bind(mSerializedObject);
                field.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
                {
                    mPreviewHost.RefreshObjectBaseState(mTarget.FindObject(mSelectedPlacementObjectId));
                    mObjectPlacementContainer?.MarkDirtyRepaint();
                });
                mObjectDetailsContainer.Add(field);
            }
        }

        // aObjectId : 削除するオブジェクトのID
        private void DeletePlacementObject(string aObjectId)
        {
            if (aObjectId == null)
            {
                return;
            }
            if (!EditorUtility.DisplayDialog("オブジェクトの削除",
                $"オブジェクト「{aObjectId}」を削除しますか?\nこのオブジェクトを参照しているトラックがある場合、参照切れとして警告表示されるようになります(トラック自体は削除されません)。",
                "削除", "キャンセル"))
            {
                return;
            }

            SerializedProperty objectsProperty = mSerializedObject.FindProperty("mObjects");
            for (int i = 0; i < objectsProperty.arraySize; i++)
            {
                if (objectsProperty.GetArrayElementAtIndex(i).FindPropertyRelative("mObjectId").stringValue == aObjectId)
                {
                    objectsProperty.DeleteArrayElementAtIndex(i);
                    break;
                }
            }
            mSerializedObject.ApplyModifiedProperties();

            mSelectedPlacementObjectId = null;
            mPreviewHost.LoadObjectsForPlacement(mTarget.Objects);
            mPreviewHost.ClearGizmoSelection();
            RefreshObjectListItems();
            mObjectListView?.RefreshItems();
            RefreshObjectDetailsPanel();
            RefreshWarnings();
            mObjectPlacementContainer?.MarkDirtyRepaint();
        }

        // ===== プレビュー再生・スクラブ =====

        // 選択中エントリの基準状態を読み込む(未読み込みの場合のみ)。読み込み直後は一時停止状態にする
        // (スクラブ/コマ送りだけを行いたい場合に、意図せず自動再生が始まらないようにするため)
        private void EnsurePreviewLoaded()
        {
            if (mIsPreviewPlaying || mSelectedEntryProperty == null || mTarget == null || mPreviewHost == null)
            {
                return;
            }

            string key = mSelectedEntryProperty.FindPropertyRelative("mKey").stringValue;

            mPreviewPlayback = new AnimSequencePlayback(mTarget, mPreviewHost, mPreviewTimeProvider);
            mPreviewPlayback.ForceLoopCurrentEntry = !mSimulateRuntime;
            mPreviewPlayback.PlaySequence(key);

            mIsPreviewPlaying = true;
            mIsPreviewPaused = true;
            mLastEditorTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
        }

        // プレビューのSimulateRuntime設定を切り替える。ON:末尾到達時にEndBehavior(Loop/Transition)をランタイムと同じ
        // ように反映する。OFF:選択中のアニメーションキーを常にループ再生する(EndBehaviorを無視する)
        private void ToggleSimulateRuntime()
        {
            mSimulateRuntime = !mSimulateRuntime;
            if (mPreviewPlayback != null)
            {
                mPreviewPlayback.ForceLoopCurrentEntry = !mSimulateRuntime;
            }
            SavePrefs();
            RefreshTransportButtons();
        }

        private void TogglePlayPause()
        {
            if (!mIsPreviewPlaying)
            {
                EnsurePreviewLoaded();
                mIsPreviewPaused = false;
            }
            else
            {
                mIsPreviewPaused = !mIsPreviewPaused;
            }

            // ギズモの表示可否はPreview/Editモード(mPreviewEditMode)のみに従う。Editモード中はPlayボタンで
            // 再生を再開してもギズモ・グリッドは表示されたままにする(トランスポートバーは常時使えるようにするため)
            mLastEditorTime = EditorApplication.timeSinceStartup; // 一時停止明けに大きなデルタタイムが入らないようにする
            RefreshTransportButtons();
            RefreshPlayingKeyLabel();
            mPreviewContainer?.MarkDirtyRepaint();
        }

        private void RewindToStart()
        {
            EnsurePreviewLoaded();
            mIsPreviewPaused = true;
            mPreviewPlayback?.SetTime(0f);
            mTimelineView?.SetPlayheadTime(0f);
            RefreshTransportButtons();
            RefreshPlayingKeyLabel();
            mPreviewContainer?.MarkDirtyRepaint();
        }

        private void FastForwardToEnd()
        {
            EnsurePreviewLoaded();
            if (mSelectedEntryProperty == null || mPreviewPlayback == null)
            {
                return;
            }
            float duration = mSelectedEntryProperty.FindPropertyRelative("mDuration").floatValue;
            mIsPreviewPaused = true;
            mPreviewPlayback.SetTime(duration);
            mTimelineView?.SetPlayheadTime(duration);
            RefreshTransportButtons();
            RefreshPlayingKeyLabel();
            mPreviewContainer?.MarkDirtyRepaint();
        }

        private void StepToPreviousKeyframe()
        {
            EnsurePreviewLoaded();
            AnimSequenceEntry entry = ResolveSelectedEntry();
            if (entry == null || mPreviewPlayback == null)
            {
                return;
            }
            float time = FindPreviousKeyframeTime(entry, mPreviewPlayback.CurrentTime) ?? 0f;
            mIsPreviewPaused = true;
            mPreviewPlayback.SetTime(time);
            mTimelineView?.SetPlayheadTime(time);
            RefreshTransportButtons();
            RefreshPlayingKeyLabel();
            mPreviewContainer?.MarkDirtyRepaint();
        }

        private void StepToNextKeyframe()
        {
            EnsurePreviewLoaded();
            AnimSequenceEntry entry = ResolveSelectedEntry();
            if (entry == null || mPreviewPlayback == null)
            {
                return;
            }
            float time = FindNextKeyframeTime(entry, mPreviewPlayback.CurrentTime) ?? entry.Duration;
            mIsPreviewPaused = true;
            mPreviewPlayback.SetTime(time);
            mTimelineView?.SetPlayheadTime(time);
            RefreshTransportButtons();
            RefreshPlayingKeyLabel();
            mPreviewContainer?.MarkDirtyRepaint();
        }

        // タイムラインのルーラーをドラッグした際に呼ばれる(スクラブ)
        // aTime : スクラブ先の時刻(秒)
        private void ScrubToTime(float aTime)
        {
            EnsurePreviewLoaded();
            if (mPreviewPlayback == null)
            {
                return;
            }
            mIsPreviewPaused = true;
            mPreviewPlayback.SetTime(aTime);
            RefreshTransportButtons();
            RefreshPlayingKeyLabel();
            mPreviewContainer?.MarkDirtyRepaint();
        }

        private AnimSequenceEntry ResolveSelectedEntry()
        {
            if (mSelectedEntryProperty == null || mTarget == null)
            {
                return null;
            }
            return mTarget.FindEntry(mSelectedEntryProperty.FindPropertyRelative("mKey").stringValue);
        }

        // aEntry内の直近時刻より前にある最も近いキーフレーム/イベントキーの時刻を返す(無ければnull)
        private static float? FindPreviousKeyframeTime(AnimSequenceEntry aEntry, float aCurrentTime)
        {
            float? best = null;
            foreach (float t in EnumerateKeyframeTimes(aEntry))
            {
                if (t < aCurrentTime - KeyframeTimeEpsilon && (best == null || t > best.Value))
                {
                    best = t;
                }
            }
            return best;
        }

        // aEntry内の直近時刻より後にある最も近いキーフレーム/イベントキーの時刻を返す(無ければnull)
        private static float? FindNextKeyframeTime(AnimSequenceEntry aEntry, float aCurrentTime)
        {
            float? best = null;
            foreach (float t in EnumerateKeyframeTimes(aEntry))
            {
                if (t > aCurrentTime + KeyframeTimeEpsilon && (best == null || t < best.Value))
                {
                    best = t;
                }
            }
            return best;
        }

        private static IEnumerable<float> EnumerateKeyframeTimes(AnimSequenceEntry aEntry)
        {
            foreach (AnimSequenceTrack track in aEntry.Tracks)
            {
                foreach (AnimSequenceVector2Keyframe k in track.PositionKeyframes) yield return k.Time;
                foreach (AnimSequenceVector2Keyframe k in track.ScaleKeyframes) yield return k.Time;
                foreach (AnimSequenceVector3Keyframe k in track.RotationKeyframes) yield return k.Time;
                foreach (AnimSequenceColorKeyframe k in track.ColorKeyframes) yield return k.Time;
                foreach (AnimSequenceSpriteKeyframe k in track.SpriteKeyframes) yield return k.Time;
                foreach (AnimSequenceMaterialKeyframe k in track.MaterialKeyframes) yield return k.Time;
                foreach (AnimSequenceMaterialParameterTrack paramTrack in track.MaterialParameterTracks)
                {
                    foreach (AnimSequenceFloatKeyframe k in paramTrack.FloatKeyframes) yield return k.Time;
                    foreach (AnimSequenceColorKeyframe k in paramTrack.ColorKeyframes) yield return k.Time;
                    foreach (AnimSequenceVector4Keyframe k in paramTrack.Vector4Keyframes) yield return k.Time;
                }
            }
            foreach (AnimSequenceEventKey k in aEntry.EventKeys) yield return k.Time;
        }

        private void StopPreview()
        {
            if (!mIsPreviewPlaying)
            {
                return;
            }

            EditorApplication.update -= OnEditorUpdate;
            mIsPreviewPlaying = false;
            mIsPreviewPaused = false;
            mPreviewPlayback = null;
            mPreviewHost?.ClearGizmoSelection();
            // 読み込み済みのプレビューが無くなるため、Editモードのままだとギズモ表示の前提が崩れる
            mPreviewEditMode = PreviewEditMode.Preview;

            RefreshTransportButtons();
            RefreshPlayingKeyLabel();

            // 選択中キーがある限り、停止後も先頭位置の再生バーを表示し続ける
            mTimelineView?.SetPlayheadTime(mSelectedEntryProperty != null ? 0f : null);
            mPreviewContainer?.MarkDirtyRepaint();
        }

        private void OnEditorUpdate()
        {
            if (!mIsPreviewPlaying || mPreviewPlayback == null)
            {
                return;
            }

            var deltaTime = (float)(EditorApplication.timeSinceStartup - mLastEditorTime);
            mLastEditorTime = EditorApplication.timeSinceStartup;

            if (mIsPreviewPaused)
            {
                return; // 一時停止中は時間を進めない(スクラブ/コマ送りのみ反映する)
            }

            mPreviewTimeProvider.SetEditorDeltaTime(deltaTime);
            mPreviewPlayback.Tick();

            if (mPreviewPlayback.IsPlaying)
            {
                mTimelineView?.SetPlayheadTime(mPreviewPlayback.CurrentTime);

                // SimulateRuntime ON中にTransitionで実際の再生キーが選択中のキーと食い違った場合、
                // グラフの選択・フォーカスを追従させる(内部でmSelectedEntryProperty/タイムライン/
                // Inspector/一覧選択もOnEntrySelectionChanged経由でまとめて切り替わる)。
                // OFF時はForceLoopCurrentEntryにより常に選択中と同じキーのままなのでこのずれは起こらない
                if (mPreviewPlayback.CurrentKey != mSelectedEntryKey)
                {
                    mEntryGraphView?.SelectAndFocusEntry(mPreviewPlayback.CurrentKey);
                }

                RefreshPlayingKeyLabel();
                mPreviewContainer?.MarkDirtyRepaint();
            }
            else
            {
                // Stop到達で自動終了した場合もボタン表示を戻す(VFXSequencePlayerのアイドル停止と同じ考え方)
                StopPreview();
            }
        }

        // 各トランスポートボタンの有効/無効・Play/Pauseの表示を現在の状態に合わせる
        private void RefreshTransportButtons()
        {
            bool hasEntry = mSelectedEntryProperty != null;
            mRewindButton?.SetEnabled(hasEntry);
            mPrevKeyButton?.SetEnabled(hasEntry);
            mPlayPauseButton?.SetEnabled(hasEntry);
            mNextKeyButton?.SetEnabled(hasEntry);
            mFastForwardButton?.SetEnabled(hasEntry);
            mFitDurationButton?.SetEnabled(hasEntry);

            if (mPlayPauseButton != null)
            {
                mPlayPauseButton.text = mIsPreviewPlaying && !mIsPreviewPaused ? "❚❚" : "▶";
            }

            mSimulateRuntimeButton?.EnableInClassList("anim-seq-gizmo-mode--active", mSimulateRuntime);

            RefreshPreviewEditModeButtons();
            RefreshGizmoModeButtons();
        }

        // 再生中ラベルの文言に加え、グラフ上の再生中ノードハイライト(mEntryGraphView.SetPlayingKey)もここでまとめて更新する。
        // トランスポート操作・選択切り替え・毎フレームのTick後などプレビュー状態が変わりうる箇所全てから呼ばれるため、
        // 「現在プレビューが実際に表示しているキー」を反映する箇所を一本化できる
        private void RefreshPlayingKeyLabel()
        {
            string currentKey = mIsPreviewPlaying ? mPreviewPlayback?.CurrentKey : null;
            mEntryGraphView?.SetPlayingKey(currentKey);

            if (mPlayingKeyLabel == null)
            {
                return;
            }
            if (!mIsPreviewPlaying || mPreviewPlayback == null)
            {
                mPlayingKeyLabel.text = string.Empty;
                return;
            }
            string state = mIsPreviewPaused ? "一時停止中" : "再生中";
            // キーフレーム選択中はギズモ編集の書き込み先がその時刻に固定されるため、状態が分かるよう併記する
            string selection = mSelectedKeyframeTime.HasValue
                ? $" / キーフレーム選択中({mSelectedKeyframeTime.Value:F2}s・Escapeで解除)"
                : string.Empty;
            mPlayingKeyLabel.text = $"{state}: {mPreviewPlayback.CurrentKey} ({mPreviewPlayback.CurrentTime:F2}s){selection}";
        }

        private void DrawPreviewIMGUI()
        {
            bool isEditMode = mPreviewEditMode == PreviewEditMode.Edit;
            DrawColorSpriteEditStrip(isEditMode);

            Rect rect = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (mPreviewHost == null)
            {
                EditorGUI.HelpBox(rect, "アニメーションキーを選択し、トランスポートバーから再生・スクラブできます", MessageType.Info);
                return;
            }

            mPreviewHost.DrawPreview(rect, mSelectedAspectPreset.AspectRatioOrZero);

            // Editモードのときのみグリッド・ギズモ操作を有効にする(Preview/Editの切り替えは専用ボタンで行う)
            if (isEditMode)
            {
                mPreviewHost.DrawGrid(rect, mSelectedAspectPreset.AspectRatioOrZero, mPreviewGridSpacing);
            }

            if (!isEditMode)
            {
                return;
            }

            // ギズモ操作を先に処理してからフォーカスを移す。Focus()を先に呼ぶとUI Toolkit側のフォーカス移動で
            // 処理中のIMGUIイベントが失われ、プレビュー領域が未フォーカスの状態からの1回目のクリックで
            // ギズモをつかめない(ギズモは見えているのに動かせない)ため
            bool isMouseDown = Event.current.type == EventType.MouseDown;

            var snap = new GizmoSnapSettings { MoveSnap = mMoveSnapValue, RotateSnap = mRotateSnapValue, ScaleSnap = mScaleSnapValue };
            if (mPreviewHost.HandleGizmoInput(rect, mSelectedAspectPreset.AspectRatioOrZero, mGizmoMode, snap))
            {
                mPreviewContainer?.MarkDirtyRepaint();
            }

            if (isMouseDown)
            {
                mPreviewContainer?.Focus(); // Spaceキー・矢印キーがこの領域に閉じるようにする
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == mKeyframeShortcutKey)
            {
                CreateOrUpdateKeyframesFromGizmoEdit();
                Event.current.Use();
            }
            else if (Event.current.type == EventType.KeyDown && HandleTimelineShortcutKeyDown(Event.current.keyCode))
            {
                Event.current.Use();
                mPreviewContainer?.MarkDirtyRepaint();
            }
            else if (Event.current.type == EventType.KeyDown && HandleNudgeKeyDown(Event.current.keyCode))
            {
                Event.current.Use();
                mPreviewContainer?.MarkDirtyRepaint();
            }
        }

        // 矢印キー(←/→)でのフレーム送り/戻しと、Escapeキーでのキーフレーム選択解除。タイムライン側
        // (AnimSequenceTimelineView)にもUI ToolkitのKeyDownEventによる同じ操作があるが、そちらはビューが
        // フォーカスを持っていないと届かず、フォーカス取得がポインタイベント依存のため環境によっては機能しない。
        // IMGUIのプレビュー領域からも操作できるようにする
        // aKeyCode : 押されたキー / 戻り値 : いずれかの操作として処理した場合true
        private bool HandleTimelineShortcutKeyDown(KeyCode aKeyCode)
        {
            if (mTimelineView == null)
            {
                return false;
            }
            if (aKeyCode == KeyCode.LeftArrow)
            {
                mTimelineView.StepFrame(-1);
                return true;
            }
            if (aKeyCode == KeyCode.RightArrow)
            {
                mTimelineView.StepFrame(1);
                return true;
            }
            if (aKeyCode == KeyCode.Escape)
            {
                mTimelineView.ClearKeyframeSelection();
                return true;
            }
            return false;
        }

        // 選択中のギズモモードに関係なく常に有効なキーボードNudge(既定WASD/QE/RF)。選択中トラックが無ければ
        // PreviewAnimSequenceHost側のNudge*メソッドが早期returnするため、ここでは特別な前提条件チェックは行わない
        // 戻り値 : いずれかのNudgeアクションに一致し処理した場合true
        private bool HandleNudgeKeyDown(KeyCode aKeyCode)
        {
            if (mPreviewHost == null)
            {
                return false;
            }

            if (aKeyCode == mNudgeKeyBindings[KeyboardNudgeAction.MoveUp]) { mPreviewHost.NudgePosition(Vector2.up, mMoveSnapValue); return true; }
            if (aKeyCode == mNudgeKeyBindings[KeyboardNudgeAction.MoveDown]) { mPreviewHost.NudgePosition(Vector2.down, mMoveSnapValue); return true; }
            if (aKeyCode == mNudgeKeyBindings[KeyboardNudgeAction.MoveLeft]) { mPreviewHost.NudgePosition(Vector2.left, mMoveSnapValue); return true; }
            if (aKeyCode == mNudgeKeyBindings[KeyboardNudgeAction.MoveRight]) { mPreviewHost.NudgePosition(Vector2.right, mMoveSnapValue); return true; }
            if (aKeyCode == mNudgeKeyBindings[KeyboardNudgeAction.RotateCcw]) { mPreviewHost.NudgeRotationZ(1f, mRotateSnapValue); return true; }
            if (aKeyCode == mNudgeKeyBindings[KeyboardNudgeAction.RotateCw]) { mPreviewHost.NudgeRotationZ(-1f, mRotateSnapValue); return true; }
            if (aKeyCode == mNudgeKeyBindings[KeyboardNudgeAction.ScaleUp]) { mPreviewHost.NudgeScale(1f, mScaleSnapValue); return true; }
            if (aKeyCode == mNudgeKeyBindings[KeyboardNudgeAction.ScaleDown]) { mPreviewHost.NudgeScale(-1f, mScaleSnapValue); return true; }
            return false;
        }

        // Editモードでトラックを選択している間のみ、プレビュー本体の上に固定高さの帯でColor/Spriteの編集フィールドを表示する。
        // Move/Rotate/Scaleと同様、値を変えるとDirtyChannelsへ記録されるだけに留め、実際のキーフレーム化はSpaceキー確定時に行う。
        // Sprite編集は暫定機能(実装後の使用感次第でユーザー判断により削除される可能性がある、SPEC.md参照)
        private void DrawColorSpriteEditStrip(bool aIsEditMode)
        {
            string selectedTrackId = mPreviewHost?.SelectedTrackId;
            if (!aIsEditMode || selectedTrackId == null || !mPreviewHost.TryGetTrackState(selectedTrackId, out AnimSequenceTrackState state))
            {
                return;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("編集中トラック:", GUILayout.Width(80));
            GUILayout.Label(selectedTrackId, EditorStyles.boldLabel, GUILayout.Width(100));
            Color newColor = EditorGUILayout.ColorField(GUIContent.none, state.Color, false, true, false, GUILayout.Width(60));
            var newSprite = (Sprite)EditorGUILayout.ObjectField(state.Sprite, typeof(Sprite), false, GUILayout.Width(140));
            EditorGUILayout.EndHorizontal();

            if (newColor != state.Color)
            {
                mPreviewHost.SetTrackColor(selectedTrackId, newColor);
                mPreviewContainer?.MarkDirtyRepaint();
            }
            if (newSprite != state.Sprite)
            {
                mPreviewHost.SetTrackSprite(selectedTrackId, newSprite);
                mPreviewContainer?.MarkDirtyRepaint();
            }
        }

        // ===== プレビュー編集(ギズモ)からのキーフレーム作成 =====

        // ギズモで編集したプロパティ(DirtyChannels)のみ、現在のスクラブ時刻へキーフレームを作成/上書きする
        private void CreateOrUpdateKeyframesFromGizmoEdit()
        {
            if (mPreviewHost == null || mSelectedEntryProperty == null || mPreviewPlayback == null)
            {
                return;
            }

            string trackId = mPreviewHost.SelectedTrackId;
            if (trackId == null || mPreviewHost.DirtyChannels.Count == 0)
            {
                return;
            }
            if (!mPreviewHost.TryGetTrackState(trackId, out AnimSequenceTrackState state))
            {
                return;
            }

            SerializedProperty tracksProperty = mSelectedEntryProperty.FindPropertyRelative("mTracks");
            SerializedProperty trackProperty = FindTrackProperty(tracksProperty, trackId);
            if (trackProperty == null)
            {
                return;
            }

            // キーフレームを選択している場合は、再生バーの位置に関わらずそのキーフレームの時刻へ書き込む
            // (選択中のキーの値を編集する操作として扱う)。未選択の場合は再生バーの位置にそのまま作成する
            // (同時刻の既存キーがあればUpsert側で上書きされる)。ここで丸め直さないのは、
            // 「見えている再生バーの位置」と「作成される時刻」を必ず一致させるため。スナップはスクラブ側で適用済み
            float time = mSelectedKeyframeTime ?? mPreviewPlayback.CurrentTime;

            if (mPreviewHost.DirtyChannels.Contains("Position"))
            {
                UpsertVector2Keyframe(trackProperty.FindPropertyRelative("mPositionKeyframes"), time, state.AnchoredPosition);
            }
            if (mPreviewHost.DirtyChannels.Contains("Scale"))
            {
                UpsertVector2Keyframe(trackProperty.FindPropertyRelative("mScaleKeyframes"), time, state.Scale);
            }
            if (mPreviewHost.DirtyChannels.Contains("Rotation"))
            {
                UpsertVector3Keyframe(trackProperty.FindPropertyRelative("mRotationKeyframes"), time, state.Rotation);
            }
            if (mPreviewHost.DirtyChannels.Contains("Color"))
            {
                UpsertColorKeyframe(trackProperty.FindPropertyRelative("mColorKeyframes"), time, state.Color);
            }
            if (mPreviewHost.DirtyChannels.Contains("Sprite"))
            {
                UpsertSpriteKeyframe(trackProperty.FindPropertyRelative("mSpriteKeyframes"), time, state.Sprite);
            }

            mSerializedObject.ApplyModifiedProperties();
            mPreviewHost.ClearDirtyChannels();
            mTimelineView?.Rebuild();
            RefreshWarnings();
        }

        private static SerializedProperty FindTrackProperty(SerializedProperty aTracksProperty, string aTrackId)
        {
            for (int i = 0; i < aTracksProperty.arraySize; i++)
            {
                SerializedProperty track = aTracksProperty.GetArrayElementAtIndex(i);
                if (track.FindPropertyRelative("mTrackId").stringValue == aTrackId)
                {
                    return track;
                }
            }
            return null;
        }

        // aTimeと同時刻(誤差KeyframeTimeEpsilon以内)のキーフレームが既にあれば値を上書きし、無ければ新規追加する
        private static void UpsertVector2Keyframe(SerializedProperty aKeyframes, float aTime, Vector2 aValue)
        {
            for (int i = 0; i < aKeyframes.arraySize; i++)
            {
                SerializedProperty element = aKeyframes.GetArrayElementAtIndex(i);
                if (Mathf.Abs(element.FindPropertyRelative("mTime").floatValue - aTime) <= KeyframeTimeEpsilon)
                {
                    element.FindPropertyRelative("mValue").vector2Value = aValue;
                    return;
                }
            }

            int index = aKeyframes.arraySize;
            aKeyframes.InsertArrayElementAtIndex(index);
            SerializedProperty newElement = aKeyframes.GetArrayElementAtIndex(index);
            newElement.FindPropertyRelative("mKeyframeId").stringValue = Guid.NewGuid().ToString("N");
            newElement.FindPropertyRelative("mTime").floatValue = aTime;
            newElement.FindPropertyRelative("mValue").vector2Value = aValue;
        }

        // aTimeと同時刻(誤差KeyframeTimeEpsilon以内)のキーフレームが既にあれば値を上書きし、無ければ新規追加する
        private static void UpsertVector3Keyframe(SerializedProperty aKeyframes, float aTime, Vector3 aValue)
        {
            for (int i = 0; i < aKeyframes.arraySize; i++)
            {
                SerializedProperty element = aKeyframes.GetArrayElementAtIndex(i);
                if (Mathf.Abs(element.FindPropertyRelative("mTime").floatValue - aTime) <= KeyframeTimeEpsilon)
                {
                    element.FindPropertyRelative("mValue").vector3Value = aValue;
                    return;
                }
            }

            int index = aKeyframes.arraySize;
            aKeyframes.InsertArrayElementAtIndex(index);
            SerializedProperty newElement = aKeyframes.GetArrayElementAtIndex(index);
            newElement.FindPropertyRelative("mKeyframeId").stringValue = Guid.NewGuid().ToString("N");
            newElement.FindPropertyRelative("mTime").floatValue = aTime;
            newElement.FindPropertyRelative("mValue").vector3Value = aValue;
        }

        // aTimeと同時刻(誤差KeyframeTimeEpsilon以内)のキーフレームが既にあれば値を上書きし、無ければ新規追加する
        private static void UpsertColorKeyframe(SerializedProperty aKeyframes, float aTime, Color aValue)
        {
            for (int i = 0; i < aKeyframes.arraySize; i++)
            {
                SerializedProperty element = aKeyframes.GetArrayElementAtIndex(i);
                if (Mathf.Abs(element.FindPropertyRelative("mTime").floatValue - aTime) <= KeyframeTimeEpsilon)
                {
                    element.FindPropertyRelative("mValue").colorValue = aValue;
                    return;
                }
            }

            int index = aKeyframes.arraySize;
            aKeyframes.InsertArrayElementAtIndex(index);
            SerializedProperty newElement = aKeyframes.GetArrayElementAtIndex(index);
            newElement.FindPropertyRelative("mKeyframeId").stringValue = Guid.NewGuid().ToString("N");
            newElement.FindPropertyRelative("mTime").floatValue = aTime;
            newElement.FindPropertyRelative("mValue").colorValue = aValue;
        }

        // aTimeと同時刻(誤差KeyframeTimeEpsilon以内)のキーフレームが既にあれば値を上書きし、無ければ新規追加する
        private static void UpsertSpriteKeyframe(SerializedProperty aKeyframes, float aTime, Sprite aValue)
        {
            for (int i = 0; i < aKeyframes.arraySize; i++)
            {
                SerializedProperty element = aKeyframes.GetArrayElementAtIndex(i);
                if (Mathf.Abs(element.FindPropertyRelative("mTime").floatValue - aTime) <= KeyframeTimeEpsilon)
                {
                    element.FindPropertyRelative("mSprite").objectReferenceValue = aValue;
                    return;
                }
            }

            int index = aKeyframes.arraySize;
            aKeyframes.InsertArrayElementAtIndex(index);
            SerializedProperty newElement = aKeyframes.GetArrayElementAtIndex(index);
            newElement.FindPropertyRelative("mKeyframeId").stringValue = Guid.NewGuid().ToString("N");
            newElement.FindPropertyRelative("mTime").floatValue = aTime;
            newElement.FindPropertyRelative("mSprite").objectReferenceValue = aValue;
        }
    }
}
