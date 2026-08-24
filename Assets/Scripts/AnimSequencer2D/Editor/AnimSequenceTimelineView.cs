/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file AnimSequenceTimelineView.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief 選択中エントリのトラック・キーフレーム・イベントキーを横軸=時間で編集するタイムラインUI
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AnimSequencer2D.Editor
{
    // Unity標準のAnimationウィンドウに倣い、ラベル列(トラック名等)を固定したまま、
    // ルーラー・キーフレーム領域だけを水平スクロール/ズームできるようにしている。
    // ラベル列と内容列は横並びのRowとして同じ垂直ScrollView内に置くことで、縦スクロールは自動的に同期する。
    // ルーラーは常に見えるよう縦スクロールの外に置き、水平スクロール位置だけを内容列から一方向で同期する。
    // ドラッグ中はSerializedPropertyへ反映せず見た目のみ更新し、PointerUpで確定する
    // (AnimSequenceDefinition.OnValidateによる時刻昇順ソートが確定のたびに走るため)
    internal class AnimSequenceTimelineView : VisualElement
    {
        // 参照先オブジェクトIDと、その右に並ぶ操作ボタン(表示上書き/M/S/▲/▼/×)が両方とも潰れずに収まる幅
        private const float LabelWidth = 200f;
        private const float RowHeight = 20f;
        private const float RulerHeight = 24f;
        private const float MarkerSize = 9f;
        private const float PlayheadWidth = 3f;
        // ルーラー上部に置く、プレイヘッドをつかみやすくするためのハンドルの幅
        private const float PlayheadHandleWidth = 12f;
        // シーケンスの始端・終端が表示領域の端に密着しないよう確保する余白
        private const float TimelineStartPadding = 24f;
        private const float TimelineEndPadding = 36f;
        // 貼り付け時、同時刻(この誤差以内)の既存キーフレームは上書き対象とみなす
        private const float KeyframeTimeEpsilon = 0.0001f;

        private const float DefaultPixelsPerSecond = 120f;
        private const float MinPixelsPerSecond = 20f;
        private const float MaxPixelsPerSecond = 2000f;
        // ルーラーの目盛りが最低限このピクセル間隔以上になるよう、きりのいい秒数へ丸める
        private const float MinTickSpacingPixels = 60f;
        private static readonly float[] sNiceStepsSeconds = { 0.01f, 0.02f, 0.05f, 0.1f, 0.2f, 0.5f, 1f, 2f, 5f, 10f, 15f, 30f, 60f, 120f, 300f, 600f };

        private readonly SerializedObject mSerializedObject;
        // トラックのオブジェクト参照(TrackId)から基準Material等を読み取るために保持する
        private readonly AnimSequenceDefinition mDefinition;
        // キーフレーム選択が変化した際に呼ぶ(nullはキーフレーム未選択=エントリ自体を表示する合図)
        private readonly Action<SerializedProperty> mOnKeyframeSelectionChanged;
        // トラック/キーフレームの追加・削除等、構造が変わった際に呼ぶ(呼び出し元での警告表示更新等に使う)
        private readonly Action mOnStructureChanged;
        // ルーラーをドラッグしてスクラブした際に呼ぶ(呼び出し元がプレビュー再生時刻へ反映する)
        private readonly Action<float> mOnScrub;
        // トラックのMute/Solo状態の実体(プレビュー描画側と共有する)。null許容(プレビュー未初期化時)
        private readonly PreviewAnimSequenceHost mPreviewHost;
        // Mute/Soloボタンを押した際、プレビューの再描画が必要なことを呼び出し元へ伝える
        private readonly Action mOnPreviewRepaintNeeded;

        private readonly VisualElement mRulerContent;
        private readonly ScrollView mRulerScrollView;
        private readonly ScrollView mBodyScrollView;
        private readonly VisualElement mLabelColumn;
        private readonly ScrollView mContentScrollView;
        private readonly VisualElement mContentColumn;
        private readonly VisualElement mPlayhead;
        private readonly VisualElement mPlayheadHandle;
        // KeyframeIdごとのマーカー要素とSerializedPropertyの対応表。DOMを再生成せずに位置だけ更新する際に使う
        private readonly List<(VisualElement Marker, SerializedProperty Property)> mTrackedMarkers = new();
        // チャンネルの展開状態。トラックIDとチャンネル名の組で識別する
        private readonly HashSet<string> mExpandedChannelIds = new();
        private readonly HashSet<string> mInitializedExpandableChannelIds = new();
        // 「+ キー追加」で明示的に追加されたが、まだキーフレームが1件も無いチャンネル(トラックIDとチャンネルIDの組で識別する)。
        // キーフレームを持つチャンネルは常に表示されるため、ここへ入れる必要があるのは空のチャンネルだけ
        private readonly HashSet<string> mAddedEmptyChannelIds = new();

        // 「+ キー追加」メニューに並べるチャンネル(表示名, チャンネルID, キーフレーム配列のフィールド名)。
        // MaterialのみMaterial切り替え・Materialパラメータ・基準Material表示をまとめて1チャンネルとして扱う
        private static readonly (string Label, string ChannelId, string FieldName)[] sAddableChannels =
        {
            ("位置", "Position", "mPositionKeyframes"),
            ("スケール", "Scale", "mScaleKeyframes"),
            ("回転", "Rotation", "mRotationKeyframes"),
            ("色", "Color", "mColorKeyframes"),
            ("画像", "Sprite", "mSpriteKeyframes"),
            ("Material", "Material", "mMaterialKeyframes"),
        };

        private SerializedProperty mEntryProperty;
        // 選択中の全キーフレームID(Ctrl/Cmd+クリックで複数選択できる)
        private readonly HashSet<string> mSelectedKeyframeIds = new();
        // KeyframeIdごとの所属リストのpropertyPath。複数選択の一括削除で、どの配列から削除すべきかを引くために使う
        private readonly Dictionary<string, string> mKeyframeLocationMap = new();
        // 複数選択ドラッグ開始時、選択中の各キーフレームの開始時刻を記録する(ドラッグ量を全員へ同じ分だけ適用するため)
        private readonly Dictionary<string, float> mDragGroupStartTimes = new();
        private float? mPlayheadTime;
        // Inspector側の値変更を受けて、次回のタイミングで非破壊的な位置再計算を行うためのフラグ
        private bool mPositionRefreshRequested;
        // 水平方向のズーム倍率(1秒あたりのピクセル数)。Ctrl+ホイールで変更する
        private float mPixelsPerSecond = DefaultPixelsPerSecond;
        // キーフレーム時刻のフレームスナップ(0以下でスナップなし)。既定は30fps
        private float mFrameSnapFps = 30f;
        // 矢印キー(←/→)でのフレーム送り/戻し1回あたりの秒数。AnimSequencerWindowのトランスポートバーから設定される
        private float mFrameStepSeconds = 1f / 30f;
        // Ctrl+Cでコピーしたキーフレーム群(セッション限定、ウィンドウ内のみで完結する)
        private readonly List<ClipboardEntry> mKeyframeClipboard = new();
        // トラックIDごとのヘッダ要素。並べ替え後にスクロールでフォーカスする対象を探すために使う
        private readonly Dictionary<string, VisualElement> mTrackHeaderElements = new();
        // 並べ替えボタンを押した直後、次のRebuild()完了後にスクロールでフォーカスすべきトラックID
        private string mPendingFocusTrackId;
        // ドラッグ中にCapturePointerした要素とPointerId。Rebuild()が要素を破棄する前にPointerCaptureが残っていると、
        // 以降のクリックがすべて破棄済みの要素へ着弾し続けてタイムライン全体が無反応になるため、Rebuild()の直前に強制解放する
        private VisualElement mCapturingElement;
        private int mCapturingPointerId = -1;

        // キーフレーム1件分のコピー内容。値の種類ごとに保持するフィールドを分け、貼り付け時にValueTypeで判定して書き戻す。
        // 対象はpropertyPath(配列インデックス依存)ではなくTrackId+チャンネル名で持つ。コピー後に貼り付け前まで
        // トラックの並べ替え・追加・削除・複製が行われてインデックスがずれても、正しいコピー元を再解決できるようにするため
        private struct ClipboardEntry
        {
            public string TrackId; // null/空文字列ならイベントキー(mEventKeys)由来
            public string ChannelFieldName; // トラック内のキーフレーム配列のフィールド名(例: "mPositionKeyframes")。イベントキーの場合は未使用
            public float RelativeTime; // コピー時点で選択していた中で最も早い時刻を基準0とした相対時刻
            public SerializedPropertyType ValueType;
            public Vector2 Vector2Value;
            public Vector3 Vector3Value;
            public Color ColorValue;
            public UnityEngine.Object ObjectValue;
            public string ObjectFieldName; // ObjectValueの書き込み先フィールド名("mSprite"または"mMaterial")
            public string StringValue;
            public float FloatValue;
            public Vector4 Vector4Value;
        }

        // aSerializedObject : 編集対象アセットのSerializedObject
        // aDefinition : トラックのオブジェクト参照(TrackId)から基準Material等を読み取るために保持する
        // aOnKeyframeSelectionChanged : キーフレーム選択変化時のコールバック(null=未選択)
        // aOnStructureChanged : トラック/キーフレームの増減があった際のコールバック
        // aOnScrub : ルーラーのドラッグでスクラブした際のコールバック(引数はスクラブ先の時刻)
        // aPreviewHost : Mute/Solo状態を共有するプレビューホスト(トラックヘッダのM/Sボタンから直接トグルする)
        // aOnPreviewRepaintNeeded : Mute/Soloを切り替えた際、プレビューの再描画が必要なことを伝えるコールバック
        public AnimSequenceTimelineView(SerializedObject aSerializedObject, AnimSequenceDefinition aDefinition, Action<SerializedProperty> aOnKeyframeSelectionChanged,
            Action aOnStructureChanged, Action<float> aOnScrub, PreviewAnimSequenceHost aPreviewHost, Action aOnPreviewRepaintNeeded)
        {
            mSerializedObject = aSerializedObject;
            mDefinition = aDefinition;
            mOnKeyframeSelectionChanged = aOnKeyframeSelectionChanged;
            mOnStructureChanged = aOnStructureChanged;
            mOnScrub = aOnScrub;
            mPreviewHost = aPreviewHost;
            mOnPreviewRepaintNeeded = aOnPreviewRepaintNeeded;

            style.flexDirection = FlexDirection.Column;

            // Ctrl+C/Vでのキーフレームコピー&ペーストを拾うため、このビュー自体をフォーカス可能にする
            // (マーカークリック時にFocus()を呼ぶことで、クリック後すぐにCtrl+C/Vが効くようにする)
            focusable = true;
            RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.ctrlKey || evt.commandKey)
                {
                    if (evt.keyCode == KeyCode.C)
                    {
                        CopySelectedKeyframes();
                        evt.StopPropagation();
                    }
                    else if (evt.keyCode == KeyCode.V)
                    {
                        PasteClipboardKeyframes();
                        evt.StopPropagation();
                    }
                    return;
                }

                // Delete/Backspaceで選択中のキーフレームを削除する(Macのキーボードにあわせ両方受け付ける)
                if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace)
                {
                    DeleteSelectedKeyframes();
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.LeftArrow)
                {
                    StepFrame(-1);
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.RightArrow)
                {
                    StepFrame(1);
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    ClearKeyframeSelection();
                    evt.StopPropagation();
                }
            });

            // ===== ルーラー行(縦スクロールの外。常に見える) =====
            var rulerRow = new VisualElement { style = { flexDirection = FlexDirection.Row, height = RulerHeight, flexShrink = 0 } };
            rulerRow.Add(new VisualElement { style = { width = LabelWidth, flexShrink = 0 } });

            mRulerScrollView = new ScrollView(ScrollViewMode.Horizontal) { style = { flexGrow = 1 } };
            mRulerScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            mRulerContent = new VisualElement { style = { position = Position.Relative, height = RulerHeight } };
            RegisterZoomAndScrub(mRulerContent);
            RegisterRulerScrub();
            mRulerScrollView.Add(mRulerContent);
            rulerRow.Add(mRulerScrollView);
            Add(rulerRow);

            // ===== 本体(縦スクロールでラベル列・内容列を同期させる。内容列だけ横スクロールする) =====
            mBodyScrollView = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            Add(mBodyScrollView);

            var bodyRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            mBodyScrollView.Add(bodyRow);

            mLabelColumn = new VisualElement { style = { width = LabelWidth, flexShrink = 0, flexDirection = FlexDirection.Column } };
            bodyRow.Add(mLabelColumn);

            mContentScrollView = new ScrollView(ScrollViewMode.Horizontal) { style = { flexGrow = 1 } };
            bodyRow.Add(mContentScrollView);

            mContentColumn = new VisualElement { style = { position = Position.Relative, flexDirection = FlexDirection.Column } };
            RegisterZoomAndScrub(mContentColumn);
            mContentScrollView.Add(mContentColumn);

            // 内容列の水平スクロールにルーラーを追従させる(一方向。ルーラー自身のスクロールバーは非表示にしている)
            mContentScrollView.horizontalScroller.valueChanged += v => mRulerScrollView.scrollOffset = new Vector2(v, 0f);

            mPlayhead = new VisualElement { style = { position = Position.Absolute, top = 0, bottom = 0, width = PlayheadWidth, backgroundColor = new Color(1f, 0.3f, 0.2f), display = DisplayStyle.None } };
            RegisterScrubDrag(mPlayhead);

            // ルーラー上部に置く、プレイヘッドよりもつかみやすい大きめのハンドル。ルーラーが再構築(Clear)される
            // たびに消えてしまうため、RefreshRuler側で毎回末尾に付け直す
            mPlayheadHandle = new VisualElement
            {
                style =
                {
                    position = Position.Absolute, top = 0, height = RulerHeight, width = PlayheadHandleWidth,
                    backgroundColor = new Color(1f, 0.3f, 0.2f), display = DisplayStyle.None,
                }
            };
            RegisterScrubDrag(mPlayheadHandle);

            Rebuild();
        }

        // CapturePointerのラッパー。Rebuild()が要素を破棄する前に強制解放できるよう、キャプチャ中の要素を記録する
        private void BeginPointerCapture(VisualElement aElement, int aPointerId)
        {
            aElement.CapturePointer(aPointerId);
            mCapturingElement = aElement;
            mCapturingPointerId = aPointerId;
        }

        // ReleasePointerのラッパー。BeginPointerCaptureで記録した内容を対応して解除する
        private void EndPointerCapture(VisualElement aElement, int aPointerId)
        {
            aElement.ReleasePointer(aPointerId);
            if (mCapturingElement == aElement)
            {
                mCapturingElement = null;
            }
        }

        // 再生ヘッド(縦の赤いライン)・上部ハンドルをドラッグしてスクラブできるようにする共通処理。
        // ルーラーだけでなく、トラック領域を通っている部分やハンドルをつかんでも操作できるようにするため
        private void RegisterScrubDrag(VisualElement aElement)
        {
            bool dragging = false;

            aElement.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || mEntryProperty == null)
                {
                    return;
                }
                Focus(); // スクラブ後すぐに矢印キーでのフレーム送り/戻しが効くよう、このビュー自体へフォーカスを移す
                dragging = true;
                BeginPointerCapture(aElement, evt.pointerId);
                ScrubToLocalX(mContentColumn.WorldToLocal(evt.position).x);
                evt.StopPropagation();
            });
            aElement.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!dragging)
                {
                    return;
                }
                ScrubToLocalX(mContentColumn.WorldToLocal(evt.position).x);
            });
            aElement.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!dragging)
                {
                    return;
                }
                dragging = false;
                EndPointerCapture(aElement, evt.pointerId);
            });
        }

        // 表示対象のエントリを切り替える。null で未選択状態にする
        // aEntryProperty : 選択中エントリのSerializedProperty(mEntriesの要素)
        public void SetEntryProperty(SerializedProperty aEntryProperty)
        {
            mEntryProperty = aEntryProperty;
            mSelectedKeyframeIds.Clear();
            // 別エントリに同名のTrackIdが偶然存在する場合の誤貼り付けを避けるため、エントリ切り替えでクリップボードも破棄する
            mKeyframeClipboard.Clear();
            // 同じオブジェクトを複数エントリで使っている場合、トラックIDだけでは区別できないためエントリ切り替えで破棄する
            mAddedEmptyChannelIds.Clear();
            Rebuild();
            SetPlayheadTime(aEntryProperty != null ? 0f : null);
        }

        // タイムラインツールバーのフレームスナップメニューから呼ぶ。0以下でスナップなしになる
        // aFps : スナップ先のフレームレート
        public void SetFrameSnapFps(float aFps) => mFrameSnapFps = aFps;

        // aTimeを現在のフレームスナップ間隔の倍数に丸める(mFrameSnapFpsが0以下ならそのまま返す)
        private float SnapTime(float aTime) => mFrameSnapFps > 0.0001f ? Mathf.Round(aTime * mFrameSnapFps) / mFrameSnapFps : aTime;

        // トランスポートバーのステップ数値欄から呼ぶ。矢印キー1回あたりの秒数を設定する
        // aSeconds : 1ステップあたりの秒数
        public void SetFrameStepSeconds(float aSeconds) => mFrameStepSeconds = Mathf.Max(0.0001f, aSeconds);

        // 矢印キー(←/→)によるフレーム送り/戻し。既存のルーラードラッグ(mOnScrub)と同じ経路で
        // AnimSequencerWindow側の再生時刻(一時停止・SetTime)へ反映する。
        // UI ToolkitのKeyDownEventはこのビューがフォーカスを持っていないと届かず、フォーカス取得が
        // ポインタイベント依存で環境によっては機能しないため、プレビュー領域のIMGUI側からも呼べるようpublicにしている
        // aDirection : +1で1ステップ進める、-1で戻す
        public void StepFrame(int aDirection)
        {
            if (mEntryProperty == null)
            {
                return;
            }
            float duration = mEntryProperty.FindPropertyRelative("mDuration").floatValue;
            float current = mPlayheadTime ?? 0f;
            // 設定したステップ秒数どおりに進める(フレームスナップで丸めない)。丸めるとステップ秒数の設定が
            // スナップ間隔に吸われて意味を失うため。キーフレームは再生バーの位置にそのまま作成されるので、
            // 丸めなくても「再生バーの位置」と「作成されるキーフレームの時刻」はずれない
            float time = Mathf.Clamp(current + aDirection * mFrameStepSeconds, 0f, duration);
            SetPlayheadTime(time);
            mOnScrub?.Invoke(time);
        }

        // プレビュー再生ヘッド(スクラブ位置)を更新する。null で非表示にする
        // aTime : 現在の再生/スクラブ時刻(秒)。エントリ未選択時は無視される
        public void SetPlayheadTime(float? aTime)
        {
            if (aTime == null || mEntryProperty == null)
            {
                mPlayhead.style.display = DisplayStyle.None;
                mPlayheadHandle.style.display = DisplayStyle.None;
                mPlayheadTime = null;
                return;
            }

            mPlayheadTime = aTime.Value;
            mPlayhead.style.display = DisplayStyle.Flex;
            mPlayheadHandle.style.display = DisplayStyle.Flex;
            UpdatePlayheadVisual();
        }

        // Inspector側でエントリ/キーフレームの値が変化した際に呼ぶ。破壊的なRebuild()は行わず、
        // 次のRefreshPositionsIfDirty()呼び出しで非破壊的にマーカー位置を再計算するようフラグを立てるだけに留める
        // (フォーカス中のInspectorフィールドを巻き込んで再構築するとフォーカスが失われるため)
        public void RequestPositionRefresh() => mPositionRefreshRequested = true;

        // タイムラインを拡大する
        public void ZoomIn() => SetPixelsPerSecond(mPixelsPerSecond * 1.25f);

        // タイムラインを縮小する
        public void ZoomOut() => SetPixelsPerSecond(mPixelsPerSecond / 1.25f);

        // 指定したグリッド幅(1秒あたりのピクセル数)へ表示倍率を変更する
        // aPixelsPerSecond : 1秒を表示する幅(px)
        public void SetGridWidth(float aPixelsPerSecond)
        {
            SetPixelsPerSecond(aPixelsPerSecond);
        }

        // 選択中シーケンスの長さ全体を横方向の表示領域に収める。終端には操作しやすい余白を残す
        public void FitDurationToView()
        {
            if (mEntryProperty == null)
            {
                return;
            }

            float duration = Mathf.Max(mEntryProperty.FindPropertyRelative("mDuration").floatValue, 0.0001f);
            float viewportWidth = mContentScrollView.contentViewport.worldBound.width;
            if (viewportWidth <= TimelineStartPadding + TimelineEndPadding)
            {
                return;
            }

            SetPixelsPerSecond((viewportWidth - TimelineStartPadding - TimelineEndPadding) / duration);
            mContentScrollView.scrollOffset = Vector2.zero;
            mRulerScrollView.scrollOffset = Vector2.zero;
        }

        // 低頻度の定期処理(AnimSequencerWindow側から呼ぶ)。フラグが立っていなければ何もしない
        public void RefreshPositionsIfDirty()
        {
            if (!mPositionRefreshRequested || mEntryProperty == null)
            {
                mPositionRefreshRequested = false;
                return;
            }
            mPositionRefreshRequested = false;

            RefreshRuler();
            UpdateContentWidth();

            foreach ((VisualElement marker, SerializedProperty property) in mTrackedMarkers)
            {
                float time = property.FindPropertyRelative("mTime").floatValue;
                marker.style.left = TimeToPixel(time) - MarkerSize * 0.5f;
            }

            UpdatePlayheadVisual();
        }

        // 現在のSerializedPropertyの内容から全行を再構築する。ドラッグ確定・追加・削除・エントリ切替のたびに呼ぶ
        public void Rebuild()
        {
            // ドラッグ中の要素(マーカー・プレイヘッド等)を破棄する前にPointerCaptureが残っていると、
            // 以降のクリックがすべて破棄済みの要素へ着弾し続けてタイムライン全体が無反応になるため、ここで強制解放する
            if (mCapturingElement != null)
            {
                mCapturingElement.ReleasePointer(mCapturingPointerId);
                mCapturingElement = null;
            }

            mLabelColumn.Clear();
            mContentColumn.Clear();
            mTrackedMarkers.Clear();
            mKeyframeLocationMap.Clear();
            mTrackHeaderElements.Clear();

            if (mEntryProperty == null)
            {
                mLabelColumn.Add(new Label("アニメーションキーを選択してください") { style = { paddingTop = 8, paddingLeft = 8, whiteSpace = WhiteSpace.Normal } });
                mOnKeyframeSelectionChanged?.Invoke(null);
                RefreshRuler();
                return;
            }

            SerializedProperty tracksProperty = mEntryProperty.FindPropertyRelative("mTracks");
            float duration = mEntryProperty.FindPropertyRelative("mDuration").floatValue;

            UpdateContentWidth();

            for (int trackIndex = 0; trackIndex < tracksProperty.arraySize; trackIndex++)
            {
                BuildTrackSection(tracksProperty, trackIndex, duration);
            }

            var addTrackButton = new Button(() => ShowAddObjectTrackMenu(tracksProperty)) { text = "+ オブジェクトから追加", style = { height = RowHeight } };
            mLabelColumn.Add(addTrackButton);
            mContentColumn.Add(new VisualElement { style = { height = RowHeight, flexShrink = 0 } });

            BuildEventRow(duration);

            mContentColumn.Add(mPlayhead);
            UpdatePlayheadVisual();

            RefreshRuler();

            // ドラッグ/削除後の再構築でも、可能な限り選択状態を維持する(KeyframeIdで再検索)
            RestoreSelection(tracksProperty);

            // トラック並べ替え直後は、操作したトラックが見える位置までスクロールする(レイアウト確定後に実行する)
            if (mPendingFocusTrackId != null && mTrackHeaderElements.TryGetValue(mPendingFocusTrackId, out VisualElement focusHeader))
            {
                mBodyScrollView.schedule.Execute(() => mBodyScrollView.ScrollTo(focusHeader));
            }
            mPendingFocusTrackId = null;
        }

        // 内容列・ルーラーの幅を現在のDuration×ズーム倍率に合わせる
        private void UpdateContentWidth()
        {
            if (mEntryProperty == null)
            {
                return;
            }
            float duration = mEntryProperty.FindPropertyRelative("mDuration").floatValue;
            float width = Mathf.Max(1f, TimelineStartPadding + duration * mPixelsPerSecond + TimelineEndPadding);
            mContentColumn.style.width = width;
            mRulerContent.style.width = width;
        }

        // Ctrl+ホイールでのズーム、ルーラーのドラッグでのスクラブを両方の領域(ルーラー/内容列)へ共通登録する
        private void RegisterZoomAndScrub(VisualElement aTarget)
        {
            aTarget.RegisterCallback<WheelEvent>(evt =>
            {
                if (!evt.ctrlKey)
                {
                    return;
                }
                float factor = evt.delta.y < 0f ? 1.1f : 1f / 1.1f;
                SetPixelsPerSecond(mPixelsPerSecond * factor);
                evt.StopPropagation();
            });
        }

        // 表示倍率の変更に伴う、幅・ルーラー・マーカーの更新をまとめる
        private void SetPixelsPerSecond(float aPixelsPerSecond)
        {
            mPixelsPerSecond = Mathf.Clamp(aPixelsPerSecond, MinPixelsPerSecond, MaxPixelsPerSecond);
            // グリッドの刻みも倍率に応じて変わるため、チャンネル行を組み直す
            if (mEntryProperty != null)
            {
                Rebuild();
                return;
            }
            UpdateContentWidth();
            RefreshRuler();
            RepositionAllMarkersImmediate();
            UpdatePlayheadVisual();
        }

        // ルーラー領域のみ、ドラッグでスクラブできるようにする(Unity Animationウィンドウの再生バーと同様)
        private void RegisterRulerScrub()
        {
            bool dragging = false;

            mRulerContent.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || mEntryProperty == null)
                {
                    return;
                }
                dragging = true;
                BeginPointerCapture(mRulerContent, evt.pointerId);
                ScrubToLocalX(mRulerContent.WorldToLocal(evt.position).x);
                evt.StopPropagation();
            });
            mRulerContent.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!dragging)
                {
                    return;
                }
                ScrubToLocalX(mRulerContent.WorldToLocal(evt.position).x);
            });
            mRulerContent.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!dragging)
                {
                    return;
                }
                dragging = false;
                EndPointerCapture(mRulerContent, evt.pointerId);
            });
        }

        private void ScrubToLocalX(float aLocalX)
        {
            float duration = mEntryProperty.FindPropertyRelative("mDuration").floatValue;
            // ドラッグ位置は任意の時刻になるため、ここでフレームスナップに乗せる。キーフレームは再生バーの位置に
            // そのまま作成されるので、この時点で丸めておけば「見えている再生バーの位置=作成される時刻」になる
            float time = Mathf.Clamp(SnapTime(PixelToTime(aLocalX)), 0f, duration);
            SetPlayheadTime(time);
            mOnScrub?.Invoke(time);
        }

        // ===== ルーラー =====

        private void RefreshRuler()
        {
            mRulerContent.Clear();

            if (mEntryProperty == null)
            {
                return;
            }

            float duration = mEntryProperty.FindPropertyRelative("mDuration").floatValue;
            float step = PickNiceStep(MinTickSpacingPixels / mPixelsPerSecond);

            for (float t = 0f; t <= duration + 0.0001f; t += step)
            {
                var label = new Label(t.ToString("F2"))
                {
                    style =
                    {
                        position = Position.Absolute,
                        left = TimeToPixel(t),
                        fontSize = 9,
                        color = new Color(0.7f, 0.7f, 0.7f),
                    }
                };
                mRulerContent.Add(label);
            }

            // Clear()で消えてしまうため、末尾(最前面)に付け直す
            mRulerContent.Add(mPlayheadHandle);
        }

        private static float PickNiceStep(float aMinStepSeconds)
        {
            foreach (float step in sNiceStepsSeconds)
            {
                if (step >= aMinStepSeconds)
                {
                    return step;
                }
            }
            return sNiceStepsSeconds[^1];
        }

        // ===== トラック =====

        private void BuildTrackSection(SerializedProperty aTracksProperty, int aTrackIndex, float aDuration)
        {
            SerializedProperty track = aTracksProperty.GetArrayElementAtIndex(aTrackIndex);
            SerializedProperty trackIdProperty = track.FindPropertyRelative("mTrackId");
            SerializedProperty visibilityProperty = track.FindPropertyRelative("mVisibilityOverride");

            // トラックヘッダ(表示上書き/M/S/▲/▼/×)はUI ToolkitのButton/PointerDownEventだと環境依存でクリックが
            // 一切届かなくなる不具合が確認されたため、IMGUI(GUIUtility.ProcessEvent経由の別入力系統。
            // プレビューのギズモ操作と同じ経路で、こちらは問題なく動作することを確認済み)で描画・操作する
            // オブジェクトIDが長いとボタンに押し出されて読めなくなるため、ID行とボタン行の2段に分ける(高さもRowHeight2つ分)
            var header = new IMGUIContainer(() => DrawTrackHeaderIMGUI(aTracksProperty, aTrackIndex, trackIdProperty, visibilityProperty))
            {
                style = { height = RowHeight * 2f, flexShrink = 0 },
            };
            mLabelColumn.Add(header);
            mTrackHeaderElements[trackIdProperty.stringValue] = header;
            mContentColumn.Add(new VisualElement { style = { height = RowHeight * 2f, flexShrink = 0 } }); // ヘッダ行と縦位置を揃えるための空行

            // 設定済み(キーフレームがある)か、「+ キー追加」で明示的に追加されたチャンネルのみ行を作る
            string trackId = trackIdProperty.stringValue;
            const string removeChannelTooltip = "このチャンネルを削除する(設定済みのキーフレームも削除されます)";
            if (ShouldShowChannel(trackId, "Position", track))
            {
                BuildExpandableVector2Channel(trackId, "位置", "Position", track.FindPropertyRelative("mPositionKeyframes"), aDuration,
                    el => el.FindPropertyRelative("mValue").vector2Value = Vector2.zero, "anim-seq-marker--position",
                    () => RemoveChannel(track, trackId, "Position"));
            }
            if (ShouldShowChannel(trackId, "Scale", track))
            {
                BuildExpandableVector2Channel(trackId, "スケール", "Scale", track.FindPropertyRelative("mScaleKeyframes"), aDuration,
                    el => el.FindPropertyRelative("mValue").vector2Value = Vector2.one, "anim-seq-marker--scale",
                    () => RemoveChannel(track, trackId, "Scale"));
            }
            if (ShouldShowChannel(trackId, "Rotation", track))
            {
                BuildExpandableVector3Channel(trackId, "回転", "Rotation", track.FindPropertyRelative("mRotationKeyframes"), aDuration,
                    el => el.FindPropertyRelative("mValue").vector3Value = Vector3.zero, "anim-seq-marker--rotation",
                    () => RemoveChannel(track, trackId, "Rotation"));
            }
            if (ShouldShowChannel(trackId, "Color", track))
            {
                BuildChannelRow("色", track.FindPropertyRelative("mColorKeyframes"), aDuration,
                    el => el.FindPropertyRelative("mValue").colorValue = Color.white, "anim-seq-marker--color",
                    aOnRemove: () => RemoveChannel(track, trackId, "Color"), aRemoveTooltip: removeChannelTooltip);
            }
            if (ShouldShowChannel(trackId, "Sprite", track))
            {
                BuildChannelRow("画像", track.FindPropertyRelative("mSpriteKeyframes"), aDuration,
                    null, "anim-seq-marker--sprite",
                    aOnRemove: () => RemoveChannel(track, trackId, "Sprite"), aRemoveTooltip: removeChannelTooltip);
            }
            if (ShouldShowChannel(trackId, "Material", track))
            {
                BuildMaterialSettingsRow(track);
                BuildChannelRow("Material", track.FindPropertyRelative("mMaterialKeyframes"), aDuration,
                    null, "anim-seq-marker--material",
                    aOnRemove: () => RemoveChannel(track, trackId, "Material"), aRemoveTooltip: removeChannelTooltip);
                BuildMaterialParameterRows(track, aDuration);
            }

            BuildAddChannelRow(aTracksProperty, aTrackIndex, track, trackId);
        }

        // aTrackId : 対象トラック / aChannelId : sAddableChannelsのチャンネルID / aTrack : トラックのSerializedProperty
        // 戻り値 : そのチャンネル行を表示すべきか(設定済みか、「+ キー追加」で明示的に追加された場合に表示する)
        private bool ShouldShowChannel(string aTrackId, string aChannelId, SerializedProperty aTrack)
            => mAddedEmptyChannelIds.Contains($"{aTrackId}:{aChannelId}") || HasChannelData(aChannelId, aTrack);

        // aChannelId : sAddableChannelsのチャンネルID / aTrack : トラックのSerializedProperty
        // 戻り値 : そのチャンネルに設定済みのデータがあるか
        private static bool HasChannelData(string aChannelId, SerializedProperty aTrack)
        {
            foreach ((string _, string channelId, string fieldName) in sAddableChannels)
            {
                if (channelId != aChannelId)
                {
                    continue;
                }
                if (aTrack.FindPropertyRelative(fieldName).arraySize > 0)
                {
                    return true;
                }
                // MaterialチャンネルはMaterial切り替えだけでなく、Materialパラメータが設定済みでも表示対象とする
                return aChannelId == "Material" && aTrack.FindPropertyRelative("mMaterialParameterTracks").arraySize > 0;
            }
            return false;
        }

        // 未表示のチャンネルを追加するためのボタン行。トラックヘッダと同じ理由(UI ToolkitのButtonだと環境依存で
        // クリックが届かない不具合が確認された)でIMGUIで描画する
        // aTracksProperty : トラック配列 / aTrackIndex : 対象トラックの位置 / aTrack : トラックのSerializedProperty / aTrackId : 対象トラック
        private void BuildAddChannelRow(SerializedProperty aTracksProperty, int aTrackIndex, SerializedProperty aTrack, string aTrackId)
        {
            var row = new IMGUIContainer(() =>
            {
                if (aTrackIndex >= aTracksProperty.arraySize)
                {
                    return; // 削除直後、次回Rebuild()までの一瞬だけインデックスが失効している場合がある(DrawTrackHeaderIMGUIと同じ理由)
                }
                // チャンネル行が1つも無いトラックではこの行だけが操作対象になるため、ここでもショートカットを受け付ける
                HandleShortcutKeys();
                Rect rect = GUILayoutUtility.GetRect(1, RowHeight, GUILayout.ExpandWidth(true));
                if (GUI.Button(rect, new GUIContent("+ キー追加", "このトラックで使うチャンネル(位置/スケール/回転/色/画像/Material)を追加する")))
                {
                    ShowAddChannelMenu(aTrack, aTrackId);
                }
            })
            {
                style = { height = RowHeight, flexShrink = 0 },
            };
            mLabelColumn.Add(row);
            mContentColumn.Add(new VisualElement { style = { height = RowHeight, flexShrink = 0 } });
        }

        // チャンネル行を削除する。設定済みのキーフレーム(Materialの場合はMaterialパラメータも)を破棄したうえで非表示に戻す
        // aTrack : トラックのSerializedProperty / aTrackId : 対象トラック / aChannelId : sAddableChannelsのチャンネルID
        private void RemoveChannel(SerializedProperty aTrack, string aTrackId, string aChannelId)
        {
            // 設定済みのキーフレームがある場合のみ、破棄されることを確認する(空の行を消すだけなら確認は不要)
            if (HasChannelData(aChannelId, aTrack) && !EditorUtility.DisplayDialog("チャンネルの削除",
                    $"「{FindChannelLabel(aChannelId)}」に設定済みのキーフレームも削除されます。よろしいですか?", "削除", "キャンセル"))
            {
                return;
            }

            foreach ((string _, string channelId, string fieldName) in sAddableChannels)
            {
                if (channelId != aChannelId)
                {
                    continue;
                }
                aTrack.FindPropertyRelative(fieldName).ClearArray();
                if (aChannelId == "Material")
                {
                    aTrack.FindPropertyRelative("mMaterialParameterTracks").ClearArray();
                }
                break;
            }

            mAddedEmptyChannelIds.Remove($"{aTrackId}:{aChannelId}");
            mSerializedObject.ApplyModifiedProperties();
            mSelectedKeyframeIds.Clear();
            mOnStructureChanged?.Invoke();
            Rebuild();
        }

        // aChannelId : sAddableChannelsのチャンネルID / 戻り値 : そのチャンネルの表示名(見つからない場合はID自体)
        private static string FindChannelLabel(string aChannelId)
        {
            foreach ((string label, string channelId, string _) in sAddableChannels)
            {
                if (channelId == aChannelId)
                {
                    return label;
                }
            }
            return aChannelId;
        }

        // 「+ キー追加」で開くメニュー。まだ表示していないチャンネルのみを並べる
        // aTrack : トラックのSerializedProperty / aTrackId : 対象トラック
        private void ShowAddChannelMenu(SerializedProperty aTrack, string aTrackId)
        {
            var menu = new GenericMenu();
            foreach ((string label, string channelId, string _) in sAddableChannels)
            {
                if (ShouldShowChannel(aTrackId, channelId, aTrack))
                {
                    continue; // 表示済みのチャンネルはメニューに出さない
                }
                string targetChannelId = channelId;
                menu.AddItem(new GUIContent(label), false, () =>
                {
                    mAddedEmptyChannelIds.Add($"{aTrackId}:{targetChannelId}");
                    Rebuild();
                });
            }
            if (menu.GetItemCount() == 0)
            {
                menu.AddDisabledItem(new GUIContent("追加できるチャンネルはありません"));
            }
            menu.ShowAsContext();
        }

        // トラックヘッダ行(参照先オブジェクトID表示・M/S/▲/▼/×)をIMGUIで描画する。
        // GUILayout(自動レイアウト)はLayoutイベントとRepaintイベントで同じ数・順序の呼び出しが必要という制約があり、
        // 高さを切り詰めたIMGUIContainer内では整合が崩れてクリック判定が信頼できなかったため、GUI.Button+明示的な
        // Rectで描画する(自動レイアウトの整合性に依存しない)。ボタン押下直後にRebuild()で自分自身を破棄するため、
        // 同フレーム内でこれ以上GUI呼び出しを続けないようGUIUtility.ExitGUI()で即座にこの回のOnGUIを打ち切る
        private void DrawTrackHeaderIMGUI(SerializedProperty aTracksProperty, int aTrackIndex, SerializedProperty aTrackIdProperty, SerializedProperty aVisibilityProperty)
        {
            if (aTrackIndex >= aTracksProperty.arraySize)
            {
                return; // 削除直後、次回Rebuild()までの一瞬だけインデックスが失効している場合がある
            }
            string trackId = aTrackIdProperty.stringValue;
            HandleShortcutKeys();

            // 1段目に参照先オブジェクトID(行全体を使うため長いIDでも省略されない)、2段目に操作ボタンを並べる
            Rect idRow = GUILayoutUtility.GetRect(1, RowHeight, GUILayout.ExpandWidth(true));
            GUI.Label(idRow, new GUIContent(trackId, "参照先オブジェクトID(読み取り専用)。変更する場合はトラックを削除し「+ オブジェクトから追加」で選び直してください"));

            Rect row = GUILayoutUtility.GetRect(1, RowHeight, GUILayout.ExpandWidth(true));
            const float gap = 2f;
            float x = row.xMax;

            var removeRect = new Rect(x -= 18f, row.y, 18f, row.height);
            x -= gap;
            var downRect = new Rect(x -= 14f, row.y, 14f, row.height);
            x -= gap;
            var upRect = new Rect(x -= 14f, row.y, 14f, row.height);

            Rect soloRect = default;
            Rect muteRect = default;
            if (mPreviewHost != null)
            {
                x -= gap;
                soloRect = new Rect(x -= 18f, row.y, 18f, row.height);
                x -= gap;
                muteRect = new Rect(x -= 18f, row.y, 18f, row.height);
            }

            x -= gap;
            var visibilityRect = new Rect(x -= 18f, row.y, 18f, row.height);

            // 他のボタン(M/S/▲/▼/×)と同じ幅に収めるため、状態は1文字+背景色で表す(意味はtooltipで補う)
            var currentOverride = (AnimSequenceVisibilityOverride)aVisibilityProperty.enumValueIndex;
            (string visibilityLabel, Color visibilityColor) = currentOverride switch
            {
                AnimSequenceVisibilityOverride.ForceShow => ("表", new Color(0.3f, 0.75f, 0.3f)),
                AnimSequenceVisibilityOverride.ForceHide => ("隠", new Color(0.8f, 0.3f, 0.3f)),
                _ => ("継", GUI.backgroundColor),
            };
            Color savedBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = visibilityColor;
            if (GUI.Button(visibilityRect, new GUIContent(visibilityLabel,
                $"このエントリでの表示上書き:{DescribeVisibilityOverride(currentOverride)}。クリックで継承→強制表示→強制非表示と巡回する")))
            {
                aVisibilityProperty.enumValueIndex = ((int)currentOverride + 1) % 3;
                mSerializedObject.ApplyModifiedProperties();
                mOnPreviewRepaintNeeded?.Invoke(); // 表示可否はShouldDrawTrackが都度解決するため、プレビューの再描画のみ要求すればよい
                GUIUtility.ExitGUI();
            }
            GUI.backgroundColor = savedBackgroundColor;

            if (mPreviewHost != null)
            {
                Color savedColor = GUI.backgroundColor;
                GUI.backgroundColor = mPreviewHost.MutedTrackIds.Contains(trackId) ? new Color(0.86f, 0.24f, 0.24f) : savedColor;
                if (GUI.Button(muteRect, new GUIContent("M", "プレビューでこのトラックを非表示にする(Mute)")))
                {
                    ToggleMute(trackId);
                    GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = mPreviewHost.SoloedTrackIds.Contains(trackId) ? new Color(1f, 0.76f, 0.03f) : savedColor;
                if (GUI.Button(soloRect, new GUIContent("S", "プレビューでこのトラックのみ表示する(Solo)")))
                {
                    ToggleSolo(trackId);
                    GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = savedColor;
            }

            using (new EditorGUI.DisabledScope(aTrackIndex <= 0))
            {
                if (GUI.Button(upRect, new GUIContent("▲", "トラックを上へ移動")))
                {
                    MoveTrack(aTracksProperty, aTrackIndex, aTrackIndex - 1);
                    GUIUtility.ExitGUI();
                }
            }
            using (new EditorGUI.DisabledScope(aTrackIndex >= aTracksProperty.arraySize - 1))
            {
                if (GUI.Button(downRect, new GUIContent("▼", "トラックを下へ移動")))
                {
                    MoveTrack(aTracksProperty, aTrackIndex, aTrackIndex + 1);
                    GUIUtility.ExitGUI();
                }
            }
            if (GUI.Button(removeRect, "×"))
            {
                RemoveTrack(aTracksProperty, aTrackIndex);
                GUIUtility.ExitGUI();
            }
        }

        // タイムライン内のIMGUI領域から呼ぶ、矢印キー(←/→)でのフレーム送り/戻しとEscapeキーでの選択解除。
        // コンストラクタで登録しているUI ToolkitのKeyDownEventはこのビューがフォーカスを持っている必要があり、
        // そのフォーカス取得がポインタイベント依存で環境によっては機能しないため、IMGUI側にも同じ操作を用意する
        private void HandleShortcutKeys()
        {
            if (Event.current.type != EventType.KeyDown)
            {
                return;
            }
            if (Event.current.keyCode == KeyCode.LeftArrow)
            {
                StepFrame(-1);
                Event.current.Use();
            }
            else if (Event.current.keyCode == KeyCode.RightArrow)
            {
                StepFrame(1);
                Event.current.Use();
            }
            else if (Event.current.keyCode == KeyCode.Escape)
            {
                ClearKeyframeSelection();
                Event.current.Use();
            }
        }

        // 選択中のキーフレームを解除する。選択中はギズモ編集の書き込み先がそのキーフレームの時刻に固定されるため、
        // 「再生バーの位置に新しくキーを作る」操作へ戻すには明示的な解除手段が必要になる(Escapeキーから呼ぶ)
        public void ClearKeyframeSelection()
        {
            if (mSelectedKeyframeIds.Count == 0)
            {
                return;
            }
            mSelectedKeyframeIds.Clear();
            ApplySelectionVisual();
            mOnKeyframeSelectionChanged?.Invoke(null);
        }

        // 表示上書きボタンのtooltipに出す、現在の状態の説明文を返す
        // aOverride : 現在の表示上書き設定
        private static string DescribeVisibilityOverride(AnimSequenceVisibilityOverride aOverride) => aOverride switch
        {
            AnimSequenceVisibilityOverride.ForceShow => "強制表示(オブジェクト側の設定に関わらず常に表示)",
            AnimSequenceVisibilityOverride.ForceHide => "強制非表示(オブジェクト側の設定に関わらず常に非表示)",
            _ => "継承(参照先オブジェクトのデフォルト表示状態に従う)",
        };

        // 基準Materialの表示(参照先オブジェクト側の設定、ここでは編集不可)・パラメータ追加ボタンをまとめた設定行(キーフレームは持たない)。
        // 基準Material・インスタンス化フラグの編集は初期配置画面でのみ行う(SPEC.md参照)
        private void BuildMaterialSettingsRow(SerializedProperty aTrack)
        {
            string trackId = aTrack.FindPropertyRelative("mTrackId").stringValue;
            Material baseMaterial = mDefinition?.FindObject(trackId)?.BaseMaterial;

            mLabelColumn.Add(new Label("基準Material") { style = { height = RowHeight, fontSize = 10, unityTextAlign = TextAnchor.MiddleLeft, paddingLeft = 12, marginTop = 4 } });

            var content = new VisualElement { style = { flexDirection = FlexDirection.Row, height = RowHeight, flexShrink = 0, alignItems = Align.Center, marginTop = 4 } };
            var materialField = new ObjectField { objectType = typeof(Material), value = baseMaterial, style = { width = 160 } };
            materialField.SetEnabled(false); // 参照先オブジェクトの設定を表示するのみ。編集は初期配置画面で行う
            content.Add(materialField);

            var addParamButton = new Button(() => ShowAddMaterialParameterMenu(aTrack, baseMaterial)) { text = "+ パラメータ追加", style = { marginLeft = 8 } };
            content.Add(addParamButton);

            mContentColumn.Add(content);
        }

        // 基準Materialのシェーダを読み取り、Float/Color/Vectorプロパティの選択メニューを表示する。
        // 既に同名プロパティのパラメータトラックが追加済みの場合はメニューから除外する(重複追加防止)
        // aBaseMaterial : 参照先オブジェクトが持つ基準Material(BuildMaterialSettingsRowで解決済みのものを渡す)
        private void ShowAddMaterialParameterMenu(SerializedProperty aTrack, Material aBaseMaterial)
        {
            Material baseMaterial = aBaseMaterial;
            List<(string Name, MaterialParameterType Type)> properties = MaterialParameterUtility.EnumerateAnimatableProperties(baseMaterial);

            SerializedProperty paramTracksProperty = aTrack.FindPropertyRelative("mMaterialParameterTracks");
            var existingNames = new HashSet<string>();
            for (int i = 0; i < paramTracksProperty.arraySize; i++)
            {
                existingNames.Add(paramTracksProperty.GetArrayElementAtIndex(i).FindPropertyRelative("mPropertyName").stringValue);
            }

            var menu = new GenericMenu();
            if (baseMaterial == null)
            {
                menu.AddDisabledItem(new GUIContent("基準Materialを設定してください"));
            }
            foreach ((string name, MaterialParameterType type) in properties)
            {
                if (existingNames.Contains(name))
                {
                    continue;
                }
                menu.AddItem(new GUIContent($"{name} ({type})"), false, () => AddMaterialParameter(paramTracksProperty, name, type));
            }
            menu.ShowAsContext();
        }

        // aName : 追加するプロパティ名 / aType : プロパティの型
        private void AddMaterialParameter(SerializedProperty aParamTracksProperty, string aName, MaterialParameterType aType)
        {
            int index = aParamTracksProperty.arraySize;
            aParamTracksProperty.InsertArrayElementAtIndex(index);
            SerializedProperty element = aParamTracksProperty.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("mPropertyName").stringValue = aName;
            element.FindPropertyRelative("mType").enumValueIndex = (int)aType;
            element.FindPropertyRelative("mFloatKeyframes").ClearArray();
            element.FindPropertyRelative("mColorKeyframes").ClearArray();
            element.FindPropertyRelative("mVector4Keyframes").ClearArray();

            mSerializedObject.ApplyModifiedProperties();
            Rebuild();
        }

        // トラックが持つ全Materialパラメータトラックのチャンネル行を、型に応じたキーフレームリストで構築する
        private void BuildMaterialParameterRows(SerializedProperty aTrack, float aDuration)
        {
            SerializedProperty paramTracksProperty = aTrack.FindPropertyRelative("mMaterialParameterTracks");
            for (int i = 0; i < paramTracksProperty.arraySize; i++)
            {
                SerializedProperty paramTrack = paramTracksProperty.GetArrayElementAtIndex(i);
                string propertyName = paramTrack.FindPropertyRelative("mPropertyName").stringValue;
                var type = (MaterialParameterType)paramTrack.FindPropertyRelative("mType").enumValueIndex;

                SerializedProperty keyframeList = type switch
                {
                    MaterialParameterType.Float => paramTrack.FindPropertyRelative("mFloatKeyframes"),
                    MaterialParameterType.Color => paramTrack.FindPropertyRelative("mColorKeyframes"),
                    _ => paramTrack.FindPropertyRelative("mVector4Keyframes"),
                };
                Action<SerializedProperty> initializer = type switch
                {
                    MaterialParameterType.Float => el => el.FindPropertyRelative("mValue").floatValue = 0f,
                    MaterialParameterType.Color => el => el.FindPropertyRelative("mValue").colorValue = Color.white,
                    _ => el => el.FindPropertyRelative("mValue").vector4Value = Vector4.zero,
                };

                int removeIndex = i; // ラムダキャプチャ用にローカル変数へ退避する
                BuildChannelRow(propertyName, keyframeList, aDuration, initializer, "anim-seq-marker--material-param",
                    aOnRemove: () => RemoveMaterialParameterTrack(paramTracksProperty, removeIndex), aRemoveTooltip: "このパラメータを削除");
            }
        }

        private void RemoveMaterialParameterTrack(SerializedProperty aParamTracksProperty, int aIndex)
        {
            aParamTracksProperty.DeleteArrayElementAtIndex(aIndex);
            mSerializedObject.ApplyModifiedProperties();
            Rebuild();
        }

        // 初期配置画面に登録済みのオブジェクト一覧から、このエントリでまだ使われていないものを選ぶメニューを表示する
        private void ShowAddObjectTrackMenu(SerializedProperty aTracksProperty)
        {
            var menu = new GenericMenu();
            if (mDefinition == null || mDefinition.Objects.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("初期配置画面でオブジェクトを配置してください"));
                menu.ShowAsContext();
                return;
            }

            var usedObjectIds = new HashSet<string>();
            for (int i = 0; i < aTracksProperty.arraySize; i++)
            {
                usedObjectIds.Add(aTracksProperty.GetArrayElementAtIndex(i).FindPropertyRelative("mTrackId").stringValue);
            }

            bool hasAddableObject = false;
            foreach (AnimSequenceObject obj in mDefinition.Objects)
            {
                if (usedObjectIds.Contains(obj.ObjectId))
                {
                    continue;
                }
                hasAddableObject = true;
                menu.AddItem(new GUIContent(obj.ObjectId), false, () => AddTrackForObject(aTracksProperty, obj.ObjectId));
            }
            if (!hasAddableObject)
            {
                menu.AddDisabledItem(new GUIContent("追加できるオブジェクトがありません(すべて使用済み)"));
            }
            menu.ShowAsContext();
        }

        // 指定オブジェクトを参照する新規トラックを追加する。InsertArrayElementAtIndexは直前要素のコピーになるため、
        // 全フィールドを明示的に初期化する。基準値(Sprite/Position/Scale/Rotation/Color/Material)はオブジェクト側が
        // 持つためここでは扱わない(キーフレームリストのみ空で初期化する)
        // aObjectId : 参照先オブジェクトのID
        private void AddTrackForObject(SerializedProperty aTracksProperty, string aObjectId)
        {
            int index = aTracksProperty.arraySize;
            aTracksProperty.InsertArrayElementAtIndex(index);
            SerializedProperty track = aTracksProperty.GetArrayElementAtIndex(index);

            track.FindPropertyRelative("mTrackId").stringValue = aObjectId;
            track.FindPropertyRelative("mPositionKeyframes").ClearArray();
            track.FindPropertyRelative("mScaleKeyframes").ClearArray();
            track.FindPropertyRelative("mRotationKeyframes").ClearArray();
            track.FindPropertyRelative("mColorKeyframes").ClearArray();
            track.FindPropertyRelative("mSpriteKeyframes").ClearArray();
            track.FindPropertyRelative("mMaterialKeyframes").ClearArray();
            track.FindPropertyRelative("mMaterialParameterTracks").ClearArray();

            mSerializedObject.ApplyModifiedProperties();
            mOnStructureChanged?.Invoke();
            Rebuild();
        }

        // Mute/Soloはエディタのプレビュー表示にのみ影響する(アセットには保存されず、ランタイム再生にも影響しない)
        private void ToggleMute(string aTrackId)
        {
            if (!mPreviewHost.MutedTrackIds.Add(aTrackId))
            {
                mPreviewHost.MutedTrackIds.Remove(aTrackId);
            }
            mOnPreviewRepaintNeeded?.Invoke();
            Rebuild(); // ボタンのハイライト表示を更新するため
        }

        private void ToggleSolo(string aTrackId)
        {
            if (!mPreviewHost.SoloedTrackIds.Add(aTrackId))
            {
                mPreviewHost.SoloedTrackIds.Remove(aTrackId);
            }
            mOnPreviewRepaintNeeded?.Invoke();
            Rebuild();
        }

        private void RemoveTrack(SerializedProperty aTracksProperty, int aTrackIndex)
        {
            aTracksProperty.DeleteArrayElementAtIndex(aTrackIndex);
            mSerializedObject.ApplyModifiedProperties();
            mSelectedKeyframeIds.Clear();
            mOnStructureChanged?.Invoke();
            Rebuild();
        }

        // トラックの表示順を入れ替える(評価・再生の挙動には影響しない。トラックはIDで解決されるため)
        private void MoveTrack(SerializedProperty aTracksProperty, int aFromIndex, int aToIndex)
        {
            if (aToIndex < 0 || aToIndex >= aTracksProperty.arraySize)
            {
                return;
            }
            string trackId = aTracksProperty.GetArrayElementAtIndex(aFromIndex).FindPropertyRelative("mTrackId").stringValue;
            aTracksProperty.MoveArrayElement(aFromIndex, aToIndex);
            mSerializedObject.ApplyModifiedProperties();
            mPendingFocusTrackId = trackId;
            Rebuild();
        }

        // ===== キーフレーム行(位置/スケール/回転/色/画像) =====

        // aLabel : 行ラベル(ラベル列側に表示) / aListProperty : 対象キーフレームリスト / aDuration : エントリの長さ(秒)
        // aValueInitializer : 新規キーフレーム追加時の値初期化(null可)
        // aOnRemove : 非nullの場合、ラベル行に行自体を削除する「×」ボタンを添える(チャンネル行・Materialパラメータ行など、動的に追加/削除できる行向け)
        // aRemoveTooltip : 「×」ボタンのtooltip(aOnRemoveが非nullの場合のみ使う)
        private void BuildChannelRow(string aLabel, SerializedProperty aListProperty, float aDuration,
            Action<SerializedProperty> aValueInitializer, string aMarkerClass, float aPaddingLeft = 12f, Action aOnRemove = null,
            string aRemoveTooltip = "この行を削除")
        {
            if (aOnRemove == null)
            {
                mLabelColumn.Add(new Label(aLabel) { style = { height = RowHeight, fontSize = 10, unityTextAlign = TextAnchor.MiddleLeft, paddingLeft = aPaddingLeft } });
            }
            else
            {
                // トラックヘッダと同じ理由(UI ToolkitのButtonだと環境依存でクリックが届かない)でIMGUIで描画する
                mLabelColumn.Add(new IMGUIContainer(() =>
                {
                    Rect rect = GUILayoutUtility.GetRect(1, RowHeight, GUILayout.ExpandWidth(true));
                    var removeRect = new Rect(rect.xMax - 18f, rect.y, 18f, rect.height);
                    var labelRect = new Rect(rect.x + aPaddingLeft, rect.y, Mathf.Max(0f, removeRect.x - rect.x - aPaddingLeft - 2f), rect.height);
                    GUI.Label(labelRect, aLabel, EditorStyles.miniLabel);
                    if (GUI.Button(removeRect, new GUIContent("×", aRemoveTooltip)))
                    {
                        aOnRemove();
                        GUIUtility.ExitGUI();
                    }
                })
                {
                    style = { height = RowHeight, flexShrink = 0 },
                });
            }

            var content = new VisualElement { style = { height = RowHeight, flexShrink = 0, position = Position.Relative } };
            content.AddToClassList("anim-seq-track-content");
            mContentColumn.Add(content);
            BuildGridLines(content, aDuration);

            RegisterAddKeyframeMenu(content, aListProperty, aValueInitializer);

            for (int i = 0; i < aListProperty.arraySize; i++)
            {
                SerializedProperty element = aListProperty.GetArrayElementAtIndex(i);
                BuildMarker(content, aListProperty, i, element, aMarkerClass);
            }
        }

        // チャンネル行または要約行の右クリックからキーフレームを追加できるようにする
        private void RegisterAddKeyframeMenu(VisualElement aContent, SerializedProperty aListProperty, Action<SerializedProperty> aValueInitializer)
        {
            aContent.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 1 || evt.target != aContent)
                {
                    return;
                }
                Vector2 localPos = aContent.WorldToLocal(evt.position);
                ShowAddKeyframeMenu(localPos.x, aListProperty, aValueInitializer);
                evt.StopPropagation();
            });
        }

        // Unity標準のAnimationウィンドウに近い、Vector2プロパティの展開可能なチャンネルを作る
        private void BuildExpandableVector2Channel(string aTrackId, string aLabel, string aChannelId, SerializedProperty aListProperty,
            float aDuration, Action<SerializedProperty> aValueInitializer, string aMarkerClass, Action aOnRemove)
        {
            bool expanded = BuildExpandableChannelHeader(aTrackId, aLabel, aChannelId, aListProperty, aDuration, aValueInitializer, aMarkerClass, aOnRemove);
            if (!expanded)
            {
                return;
            }

            BuildChannelRow("X", aListProperty, aDuration, aValueInitializer, aMarkerClass, 28);
            BuildChannelRow("Y", aListProperty, aDuration, aValueInitializer, aMarkerClass, 28);
        }

        // Unity標準のAnimationウィンドウに近い、Vector3プロパティの展開可能なチャンネルを作る(回転X/Y/Zに使う)
        private void BuildExpandableVector3Channel(string aTrackId, string aLabel, string aChannelId, SerializedProperty aListProperty,
            float aDuration, Action<SerializedProperty> aValueInitializer, string aMarkerClass, Action aOnRemove)
        {
            bool expanded = BuildExpandableChannelHeader(aTrackId, aLabel, aChannelId, aListProperty, aDuration, aValueInitializer, aMarkerClass, aOnRemove);
            if (!expanded)
            {
                return;
            }

            BuildChannelRow("X", aListProperty, aDuration, aValueInitializer, aMarkerClass, 28);
            BuildChannelRow("Y", aListProperty, aDuration, aValueInitializer, aMarkerClass, 28);
            BuildChannelRow("Z", aListProperty, aDuration, aValueInitializer, aMarkerClass, 28);
        }

        // 展開ヘッダーと、折りたたみ時にもキーを確認できる要約行を作る
        // aOnRemove : 非nullの場合、ヘッダー行にチャンネル自体を削除する「×」ボタンを添える
        private bool BuildExpandableChannelHeader(string aTrackId, string aLabel, string aChannelId, SerializedProperty aListProperty,
            float aDuration, Action<SerializedProperty> aValueInitializer, string aMarkerClass, Action aOnRemove)
        {
            string expansionId = $"{aTrackId}:{aChannelId}";
            if (mInitializedExpandableChannelIds.Add(expansionId))
            {
                mExpandedChannelIds.Add(expansionId);
            }
            bool expanded = mExpandedChannelIds.Contains(expansionId);

            // 折りたたみの開閉・チャンネル削除ともに、トラックヘッダと同じ理由(UI Toolkitだと環境依存で
            // クリックが届かない)でIMGUIで描画する
            var toggle = new IMGUIContainer(() =>
            {
                Rect rect = GUILayoutUtility.GetRect(1, RowHeight, GUILayout.ExpandWidth(true));
                var removeRect = new Rect(rect.xMax - 18f, rect.y, 18f, rect.height);
                float toggleWidth = aOnRemove != null ? Mathf.Max(0f, removeRect.x - rect.x - 2f) : rect.width;
                var toggleRect = new Rect(rect.x + 6f, rect.y, Mathf.Max(0f, toggleWidth - 6f), rect.height);

                bool isExpanded = mExpandedChannelIds.Contains(expansionId);
                if (GUI.Button(toggleRect, isExpanded ? $"▼ {aLabel}" : $"▶ {aLabel}", EditorStyles.label))
                {
                    if (!mExpandedChannelIds.Add(expansionId))
                    {
                        mExpandedChannelIds.Remove(expansionId);
                    }
                    Rebuild();
                    GUIUtility.ExitGUI();
                }
                if (aOnRemove != null && GUI.Button(removeRect, new GUIContent("×", "このチャンネルを削除する(設定済みのキーフレームも削除されます)")))
                {
                    aOnRemove();
                    GUIUtility.ExitGUI();
                }
            })
            {
                style = { height = RowHeight, flexShrink = 0 },
            };
            toggle.AddToClassList("anim-seq-channel-foldout");
            mLabelColumn.Add(toggle);

            var content = new VisualElement { style = { height = RowHeight, flexShrink = 0, position = Position.Relative } };
            content.AddToClassList("anim-seq-track-content");
            mContentColumn.Add(content);
            BuildGridLines(content, aDuration);
            RegisterAddKeyframeMenu(content, aListProperty, aValueInitializer);

            for (int i = 0; i < aListProperty.arraySize; i++)
            {
                BuildMarker(content, aListProperty, i, aListProperty.GetArrayElementAtIndex(i), aMarkerClass);
            }
            return expanded;
        }

        private void BuildMarker(VisualElement aContent, SerializedProperty aListProperty, int aElementIndex, SerializedProperty aElement, string aMarkerClass)
        {
            string keyframeId = aElement.FindPropertyRelative("mKeyframeId").stringValue;
            float time = aElement.FindPropertyRelative("mTime").floatValue;

            var marker = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = TimeToPixel(time) - MarkerSize * 0.5f,
                    width = MarkerSize,
                    height = MarkerSize,
                    top = (RowHeight - MarkerSize) * 0.5f,
                    rotate = new Rotate(45),
                },
            };
            marker.AddToClassList("anim-seq-marker");
            marker.AddToClassList(aMarkerClass);
            if (mSelectedKeyframeIds.Contains(keyframeId))
            {
                marker.AddToClassList("anim-seq-marker--selected");
            }
            aContent.Add(marker);
            mTrackedMarkers.Add((marker, aElement));
            mKeyframeLocationMap[keyframeId] = aListProperty.propertyPath;

            bool dragging = false;
            bool moved = false;
            bool wasPartOfMultiSelection = false;

            marker.RegisterCallback<PointerDownEvent>(evt =>
            {
                Focus(); // クリック後すぐにCtrl+C/Vが効くよう、このビュー自体へフォーカスを移す
                if (evt.button == 1)
                {
                    if (mSelectedKeyframeIds.Count > 1 && mSelectedKeyframeIds.Contains(keyframeId))
                    {
                        ShowDeleteMultipleKeyframesMenu();
                    }
                    else
                    {
                        ShowDeleteKeyframeMenu(aListProperty, aElementIndex, keyframeId);
                    }
                    evt.StopPropagation();
                    return;
                }
                if (evt.button != 0)
                {
                    return;
                }

                if (evt.ctrlKey || evt.commandKey)
                {
                    // Ctrl/Cmd+クリックは選択のトグルのみ行う(ドラッグは開始しない)
                    if (!mSelectedKeyframeIds.Add(keyframeId))
                    {
                        mSelectedKeyframeIds.Remove(keyframeId);
                    }
                    Rebuild();
                    evt.StopPropagation();
                    return;
                }

                dragging = true;
                moved = false;
                wasPartOfMultiSelection = mSelectedKeyframeIds.Count > 1 && mSelectedKeyframeIds.Contains(keyframeId);
                if (!mSelectedKeyframeIds.Contains(keyframeId))
                {
                    // 未選択のマーカーへの単純クリック → 単一選択に切り替える(ドラッグ開始前に確定してよい)。
                    // ここでRebuild()すると直後のCapturePointerが壊れるため、マーカーを再構築せずCSSクラスとInspectorだけを即座に反映する
                    mSelectedKeyframeIds.Clear();
                    mSelectedKeyframeIds.Add(keyframeId);
                    ApplySelectionVisual();
                    mOnKeyframeSelectionChanged?.Invoke(aElement);
                }
                CaptureDragGroupStartTimes();
                BeginPointerCapture(marker, evt.pointerId);
                evt.StopPropagation();
            });
            marker.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!dragging || !mDragGroupStartTimes.TryGetValue(keyframeId, out float anchorStartTime))
                {
                    return;
                }
                moved = true;

                float duration = mEntryProperty.FindPropertyRelative("mDuration").floatValue;
                float localX = aContent.WorldToLocal(evt.position).x;
                float delta = PixelToTime(localX) - anchorStartTime;
                foreach (float startTime in mDragGroupStartTimes.Values)
                {
                    delta = Mathf.Clamp(delta, -startTime, duration - startTime);
                }

                foreach ((VisualElement m, SerializedProperty p) in mTrackedMarkers)
                {
                    string id = p.FindPropertyRelative("mKeyframeId").stringValue;
                    if (mDragGroupStartTimes.TryGetValue(id, out float startTime))
                    {
                        m.style.left = TimeToPixel(startTime + delta) - MarkerSize * 0.5f;
                    }
                }
            });
            marker.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!dragging)
                {
                    return;
                }
                dragging = false;
                EndPointerCapture(marker, evt.pointerId);

                if (moved && mDragGroupStartTimes.TryGetValue(keyframeId, out float anchorStartTime))
                {
                    float duration = mEntryProperty.FindPropertyRelative("mDuration").floatValue;
                    float localX = aContent.WorldToLocal(evt.position).x;
                    float rawDelta = PixelToTime(localX) - anchorStartTime;
                    float snappedDelta = SnapTime(rawDelta);
                    foreach (float startTime in mDragGroupStartTimes.Values)
                    {
                        snappedDelta = Mathf.Clamp(snappedDelta, -startTime, duration - startTime);
                    }

                    foreach ((VisualElement m, SerializedProperty p) in mTrackedMarkers)
                    {
                        string id = p.FindPropertyRelative("mKeyframeId").stringValue;
                        if (mDragGroupStartTimes.TryGetValue(id, out float startTime))
                        {
                            p.FindPropertyRelative("mTime").floatValue = startTime + snappedDelta;
                        }
                    }
                    mSerializedObject.ApplyModifiedProperties();
                    Rebuild();
                }
                else if (wasPartOfMultiSelection)
                {
                    // 複数選択の一員をドラッグ無しでクリック → 単一選択へ収束する
                    mSelectedKeyframeIds.Clear();
                    mSelectedKeyframeIds.Add(keyframeId);
                    Rebuild();
                }
            });
        }

        // mSelectedKeyframeIdsの内容を、マーカーを再構築せずに全マーカーのCSSクラスへ反映する(ドラッグ開始直後などRebuild()を避けたい場面用)
        private void ApplySelectionVisual()
        {
            foreach ((VisualElement marker, SerializedProperty property) in mTrackedMarkers)
            {
                string id = property.FindPropertyRelative("mKeyframeId").stringValue;
                marker.EnableInClassList("anim-seq-marker--selected", mSelectedKeyframeIds.Contains(id));
            }
        }

        // 複数選択ドラッグの開始時、選択中の全キーフレームの現在時刻を記録する
        private void CaptureDragGroupStartTimes()
        {
            mDragGroupStartTimes.Clear();
            foreach ((VisualElement _, SerializedProperty property) in mTrackedMarkers)
            {
                string id = property.FindPropertyRelative("mKeyframeId").stringValue;
                if (mSelectedKeyframeIds.Contains(id) && !mDragGroupStartTimes.ContainsKey(id))
                {
                    mDragGroupStartTimes[id] = property.FindPropertyRelative("mTime").floatValue;
                }
            }
        }

        // ===== イベント行 =====

        private void BuildEventRow(float aDuration)
        {
            mLabelColumn.Add(new Label("イベント") { style = { height = RowHeight, fontSize = 10, unityTextAlign = TextAnchor.MiddleLeft, paddingLeft = 12, marginTop = 4 } });

            var content = new VisualElement { style = { height = RowHeight, flexShrink = 0, position = Position.Relative, marginTop = 4 } };
            content.AddToClassList("anim-seq-track-content");
            mContentColumn.Add(content);
            BuildGridLines(content, aDuration);

            SerializedProperty eventKeysProperty = mEntryProperty.FindPropertyRelative("mEventKeys");

            content.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 1 || evt.target != content)
                {
                    return;
                }
                Vector2 localPos = content.WorldToLocal(evt.position);
                ShowAddEventKeyMenu(localPos.x, eventKeysProperty);
                evt.StopPropagation();
            });

            for (int i = 0; i < eventKeysProperty.arraySize; i++)
            {
                SerializedProperty element = eventKeysProperty.GetArrayElementAtIndex(i);
                BuildMarker(content, eventKeysProperty, i, element, "anim-seq-marker--event");
            }
        }

        // ===== キーフレーム/イベントキーの追加・削除 =====

        private void ShowAddKeyframeMenu(float aLocalX, SerializedProperty aListProperty, Action<SerializedProperty> aValueInitializer)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("ここにキーフレームを追加"), false, () =>
            {
                float duration = mEntryProperty.FindPropertyRelative("mDuration").floatValue;
                float time = Mathf.Clamp(SnapTime(Mathf.Clamp(PixelToTime(aLocalX), 0f, duration)), 0f, duration);

                InsertKeyframe(aListProperty, time, aValueInitializer, out string newId);
                mSerializedObject.ApplyModifiedProperties();
                mSelectedKeyframeIds.Clear();
                mSelectedKeyframeIds.Add(newId);
                mOnStructureChanged?.Invoke();
                Rebuild();
            });
            menu.ShowAsContext();
        }

        private void ShowAddEventKeyMenu(float aLocalX, SerializedProperty aEventKeysProperty)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("ここにイベントキーを追加"), false, () =>
            {
                float duration = mEntryProperty.FindPropertyRelative("mDuration").floatValue;
                float time = Mathf.Clamp(SnapTime(Mathf.Clamp(PixelToTime(aLocalX), 0f, duration)), 0f, duration);

                int index = aEventKeysProperty.arraySize;
                aEventKeysProperty.InsertArrayElementAtIndex(index);
                SerializedProperty element = aEventKeysProperty.GetArrayElementAtIndex(index);
                string newId = Guid.NewGuid().ToString("N");
                element.FindPropertyRelative("mKeyframeId").stringValue = newId;
                element.FindPropertyRelative("mTime").floatValue = time;
                element.FindPropertyRelative("mEventKey").stringValue = "NewEvent";

                mSerializedObject.ApplyModifiedProperties();
                mSelectedKeyframeIds.Clear();
                mSelectedKeyframeIds.Add(newId);
                mOnStructureChanged?.Invoke();
                Rebuild();
            });
            menu.ShowAsContext();
        }

        private void ShowDeleteKeyframeMenu(SerializedProperty aListProperty, int aElementIndex, string aKeyframeId)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("削除"), false, () =>
            {
                aListProperty.DeleteArrayElementAtIndex(aElementIndex);
                mSerializedObject.ApplyModifiedProperties();
                mSelectedKeyframeIds.Remove(aKeyframeId);
                mOnStructureChanged?.Invoke();
                Rebuild();
            });
            menu.ShowAsContext();
        }

        // 複数選択中のキーフレームをまとめて削除する。チャンネル・トラックをまたいだ選択にも対応するため、
        // 所属リストのpropertyPathごとにグルーピングしてから降順インデックスで削除する(添字ずれ防止)
        private void ShowDeleteMultipleKeyframesMenu()
        {
            var menu = new GenericMenu();
            int count = mSelectedKeyframeIds.Count;
            menu.AddItem(new GUIContent($"選択した{count}件を削除"), false, DeleteSelectedKeyframes);
            menu.ShowAsContext();
        }

        // 現在選択中の全キーフレームを削除する(単一選択・複数選択どちらでも動作する)。Deleteキー・複数選択の
        // 右クリックメニューの両方から呼ぶ共通処理。所属リストのpropertyPathごとにグルーピングしてから
        // 降順インデックスで削除する(添字ずれ防止)
        private void DeleteSelectedKeyframes()
        {
            if (mSelectedKeyframeIds.Count == 0)
            {
                return;
            }

            var indicesByListPath = new Dictionary<string, List<int>>();
            foreach (string id in mSelectedKeyframeIds)
            {
                if (!mKeyframeLocationMap.TryGetValue(id, out string listPath))
                {
                    continue;
                }
                SerializedProperty listProperty = mSerializedObject.FindProperty(listPath);
                int index = listProperty != null ? FindIndexByKeyframeId(listProperty, id) : -1;
                if (index < 0)
                {
                    continue;
                }
                if (!indicesByListPath.TryGetValue(listPath, out List<int> indices))
                {
                    indicesByListPath[listPath] = indices = new List<int>();
                }
                indices.Add(index);
            }

            foreach (KeyValuePair<string, List<int>> pair in indicesByListPath)
            {
                SerializedProperty listProperty = mSerializedObject.FindProperty(pair.Key);
                pair.Value.Sort((a, b) => b.CompareTo(a)); // 降順に削除して添字ずれを防ぐ
                foreach (int index in pair.Value)
                {
                    listProperty.DeleteArrayElementAtIndex(index);
                }
            }

            mSerializedObject.ApplyModifiedProperties();
            mSelectedKeyframeIds.Clear();
            mOnStructureChanged?.Invoke();
            Rebuild();
        }

        private static int FindIndexByKeyframeId(SerializedProperty aListProperty, string aKeyframeId)
        {
            for (int i = 0; i < aListProperty.arraySize; i++)
            {
                if (aListProperty.GetArrayElementAtIndex(i).FindPropertyRelative("mKeyframeId").stringValue == aKeyframeId)
                {
                    return i;
                }
            }
            return -1;
        }

        // ===== キーフレームのコピー&ペースト(Ctrl+C/V) =====

        // 選択中の全キーフレームを、所属リスト・時刻(最早のものを基準0とした相対値)・値と共にクリップボードへ記録する
        private void CopySelectedKeyframes()
        {
            if (mSelectedKeyframeIds.Count == 0)
            {
                return;
            }

            var entries = new List<ClipboardEntry>();
            float earliestTime = float.MaxValue;

            foreach (string id in mSelectedKeyframeIds)
            {
                if (!mKeyframeLocationMap.TryGetValue(id, out string listPath))
                {
                    continue;
                }
                SerializedProperty listProperty = mSerializedObject.FindProperty(listPath);
                int index = listProperty != null ? FindIndexByKeyframeId(listProperty, id) : -1;
                if (index < 0)
                {
                    continue;
                }

                SerializedProperty element = listProperty.GetArrayElementAtIndex(index);
                float time = element.FindPropertyRelative("mTime").floatValue;
                earliestTime = Mathf.Min(earliestTime, time);

                ResolveTrackIdAndChannel(listPath, out string trackId, out string channelFieldName);
                var entry = new ClipboardEntry { TrackId = trackId, ChannelFieldName = channelFieldName, RelativeTime = time };
                SerializedProperty valueProperty = element.FindPropertyRelative("mValue");
                if (valueProperty != null)
                {
                    entry.ValueType = valueProperty.propertyType;
                    switch (valueProperty.propertyType)
                    {
                        case SerializedPropertyType.Vector2: entry.Vector2Value = valueProperty.vector2Value; break;
                        case SerializedPropertyType.Vector3: entry.Vector3Value = valueProperty.vector3Value; break;
                        case SerializedPropertyType.Color: entry.ColorValue = valueProperty.colorValue; break;
                        case SerializedPropertyType.Float: entry.FloatValue = valueProperty.floatValue; break;
                        case SerializedPropertyType.Vector4: entry.Vector4Value = valueProperty.vector4Value; break;
                    }
                }
                else
                {
                    SerializedProperty spriteProperty = element.FindPropertyRelative("mSprite");
                    SerializedProperty materialProperty = element.FindPropertyRelative("mMaterial");
                    if (spriteProperty != null)
                    {
                        entry.ValueType = SerializedPropertyType.ObjectReference;
                        entry.ObjectFieldName = "mSprite";
                        entry.ObjectValue = spriteProperty.objectReferenceValue;
                    }
                    else if (materialProperty != null)
                    {
                        entry.ValueType = SerializedPropertyType.ObjectReference;
                        entry.ObjectFieldName = "mMaterial";
                        entry.ObjectValue = materialProperty.objectReferenceValue;
                    }
                    else
                    {
                        entry.ValueType = SerializedPropertyType.String;
                        entry.StringValue = element.FindPropertyRelative("mEventKey").stringValue;
                    }
                }
                entries.Add(entry);
            }

            mKeyframeClipboard.Clear();
            foreach (ClipboardEntry entry in entries)
            {
                ClipboardEntry shifted = entry;
                shifted.RelativeTime -= earliestTime; // 最早時刻を基準0にする
                mKeyframeClipboard.Add(shifted);
            }
        }

        // aListPathは"mTracks.Array.data[N].mXxxKeyframes"(トラックのチャンネル)または"mEventKeys"(イベントキー)のいずれか。
        // コピーした時点でトラックIDへ変換しておくことで、貼り付け前にトラックの並べ替え・追加・削除・複製が
        // 行われて配列インデックスがずれても、貼り付け時にIDで正しいコピー元を再解決できるようにする
        private void ResolveTrackIdAndChannel(string aListPath, out string aTrackId, out string aChannelFieldName)
        {
            aTrackId = null;
            aChannelFieldName = null;
            if (aListPath.EndsWith(".mEventKeys", StringComparison.Ordinal) || aListPath == "mEventKeys")
            {
                return; // イベントキー(トラック非依存)
            }

            // 実際のpropertyPathは "mEntries.Array.data[X].mTracks.Array.data[N].mXxxKeyframes" という形式になり、
            // "data[" がエントリのインデックスX・トラックのインデックスNの2箇所に現れる。欲しいのは末尾に近い方
            // (トラック側)のため、IndexOfではなくLastIndexOfを使う(IndexOfだとエントリ側を誤って拾ってしまう)
            int dataStart = aListPath.LastIndexOf("data[", StringComparison.Ordinal);
            int dataEnd = dataStart >= 0 ? aListPath.IndexOf(']', dataStart) : -1;
            if (dataStart < 0 || dataEnd < 0)
            {
                return;
            }
            int index = int.Parse(aListPath.Substring(dataStart + 5, dataEnd - dataStart - 5));
            aChannelFieldName = aListPath.Substring(aListPath.LastIndexOf('.') + 1);

            // mSerializedObjectはAnimSequenceDefinitionのルートであり、mTracksはエントリ配下にネストされたフィールドのため
            // "mTracks"という文字列だけではFindPropertyできない(常にnullが返る)。選択中エントリ(mEntryProperty)からの
            // 相対参照で解決する
            SerializedProperty tracksProperty = mEntryProperty?.FindPropertyRelative("mTracks");
            if (tracksProperty != null && index >= 0 && index < tracksProperty.arraySize)
            {
                aTrackId = tracksProperty.GetArrayElementAtIndex(index).FindPropertyRelative("mTrackId").stringValue;
            }
        }

        // クリップボードの内容を、現在のプレイヘッド位置を基準に相対間隔を保ったまま元のトラック・チャンネルへ貼り付ける
        private void PasteClipboardKeyframes()
        {
            if (mKeyframeClipboard.Count == 0 || mPlayheadTime == null || mEntryProperty == null)
            {
                return;
            }

            float duration = mEntryProperty.FindPropertyRelative("mDuration").floatValue;
            mSelectedKeyframeIds.Clear();

            foreach (ClipboardEntry entry in mKeyframeClipboard)
            {
                SerializedProperty listProperty = ResolveClipboardTargetList(entry);
                if (listProperty == null)
                {
                    continue; // コピー元のトラック/チャンネルが無くなっていればスキップする
                }
                float time = Mathf.Clamp(SnapTime(mPlayheadTime.Value + entry.RelativeTime), 0f, duration);
                string newId = UpsertClipboardValue(listProperty, time, entry);
                if (newId != null)
                {
                    mSelectedKeyframeIds.Add(newId);
                }
            }

            mSerializedObject.ApplyModifiedProperties();
            mOnStructureChanged?.Invoke();
            Rebuild();
        }

        // クリップボードのエントリが指すリストを、貼り付け時点の状態から改めてトラックIDで解決する
        // (コピー後にトラックの並べ替え・追加・削除・複製が行われていても正しい対象を指す)
        private SerializedProperty ResolveClipboardTargetList(ClipboardEntry aEntry)
        {
            if (string.IsNullOrEmpty(aEntry.TrackId))
            {
                return mEntryProperty.FindPropertyRelative("mEventKeys");
            }

            SerializedProperty tracksProperty = mEntryProperty.FindPropertyRelative("mTracks");
            for (int i = 0; i < tracksProperty.arraySize; i++)
            {
                SerializedProperty track = tracksProperty.GetArrayElementAtIndex(i);
                if (track.FindPropertyRelative("mTrackId").stringValue == aEntry.TrackId)
                {
                    return track.FindPropertyRelative(aEntry.ChannelFieldName);
                }
            }
            return null;
        }

        // aTimeと同時刻(誤差KeyframeTimeEpsilon以内)のキーフレームが既にあれば値を上書きし、無ければ新規追加する。
        // 既存のUpsertVector2Keyframe等(AnimSequencerWindow.cs)と同じ「同時刻なら上書き」パターンを、
        // ClipboardEntryのValueTypeに応じて分岐する形でまとめたもの。挿入/上書きしたキーフレームIDを返す
        private static string UpsertClipboardValue(SerializedProperty aKeyframes, float aTime, ClipboardEntry aEntry)
        {
            for (int i = 0; i < aKeyframes.arraySize; i++)
            {
                SerializedProperty element = aKeyframes.GetArrayElementAtIndex(i);
                if (Mathf.Abs(element.FindPropertyRelative("mTime").floatValue - aTime) <= KeyframeTimeEpsilon)
                {
                    ApplyClipboardValue(element, aEntry);
                    return element.FindPropertyRelative("mKeyframeId").stringValue;
                }
            }

            int index = aKeyframes.arraySize;
            aKeyframes.InsertArrayElementAtIndex(index);
            SerializedProperty newElement = aKeyframes.GetArrayElementAtIndex(index);
            string newId = Guid.NewGuid().ToString("N");
            newElement.FindPropertyRelative("mKeyframeId").stringValue = newId;
            newElement.FindPropertyRelative("mTime").floatValue = aTime;
            ApplyClipboardValue(newElement, aEntry);
            return newId;
        }

        // ClipboardEntryのValueTypeに応じて、対応する値フィールド(Vector2/Vector3/Color/Float/Vector4/Sprite/Material/イベントキー)へ書き込む
        private static void ApplyClipboardValue(SerializedProperty aElement, ClipboardEntry aEntry)
        {
            switch (aEntry.ValueType)
            {
                case SerializedPropertyType.Vector2:
                    aElement.FindPropertyRelative("mValue").vector2Value = aEntry.Vector2Value;
                    break;
                case SerializedPropertyType.Vector3:
                    aElement.FindPropertyRelative("mValue").vector3Value = aEntry.Vector3Value;
                    break;
                case SerializedPropertyType.Color:
                    aElement.FindPropertyRelative("mValue").colorValue = aEntry.ColorValue;
                    break;
                case SerializedPropertyType.Float:
                    aElement.FindPropertyRelative("mValue").floatValue = aEntry.FloatValue;
                    break;
                case SerializedPropertyType.Vector4:
                    aElement.FindPropertyRelative("mValue").vector4Value = aEntry.Vector4Value;
                    break;
                case SerializedPropertyType.ObjectReference:
                    // SpriteキーフレームはmSprite、Materialキーフレームは mMaterial というように、
                    // コピー時に記録したObjectFieldNameへ書き込む(両方ともObjectReference型のため名前で区別する)
                    aElement.FindPropertyRelative(aEntry.ObjectFieldName).objectReferenceValue = aEntry.ObjectValue;
                    break;
                case SerializedPropertyType.String:
                    aElement.FindPropertyRelative("mEventKey").stringValue = aEntry.StringValue;
                    break;
            }
        }

        // 指定チャンネルの配列へキーフレームを1件追加する
        // aKeyframes : 追加先の配列プロパティ / aTime : 追加する時刻
        // aValueInitializer : 値フィールドの初期化(null可) / aNewKeyframeId : 生成したID
        private static void InsertKeyframe(SerializedProperty aKeyframes, float aTime,
            Action<SerializedProperty> aValueInitializer, out string aNewKeyframeId)
        {
            int index = aKeyframes.arraySize;
            aKeyframes.InsertArrayElementAtIndex(index);
            SerializedProperty element = aKeyframes.GetArrayElementAtIndex(index);

            // 直前要素のコピーが入るため、全フィールドを明示的に初期化する
            aNewKeyframeId = Guid.NewGuid().ToString("N");
            element.FindPropertyRelative("mKeyframeId").stringValue = aNewKeyframeId;
            element.FindPropertyRelative("mTime").floatValue = aTime;
            aValueInitializer?.Invoke(element);
        }

        // ルーラーと同じ刻みの縦線をチャンネル行へ描画する。マーカーの背面に置くため、先に追加する
        // aContent : グリッドを配置するチャンネル行 / aDuration : シーケンスの長さ(秒)
        private void BuildGridLines(VisualElement aContent, float aDuration)
        {
            float step = PickNiceStep(MinTickSpacingPixels / mPixelsPerSecond);
            for (float time = 0f; time <= aDuration + 0.0001f; time += step)
            {
                var line = new VisualElement
                {
                    pickingMode = PickingMode.Ignore,
                    style =
                    {
                        position = Position.Absolute,
                        left = TimeToPixel(time),
                        top = 0,
                        bottom = 0,
                        width = 1,
                        backgroundColor = new Color(1f, 1f, 1f, 0.09f),
                    }
                };
                aContent.Add(line);
            }
        }

        // ===== 選択・再配置 =====

        // Rebuild後、選択中キーフレームが1件だけならそのSerializedProperty参照をInspectorへ渡し直す
        // (配列並び替えでpropertyPathがずれるため)。0件または複数選択中はInspectorを未選択表示にする
        // (複数選択時の一括値編集はSPEC.mdの対象外のため、単一選択の表示のみサポートする)
        private void RestoreSelection(SerializedProperty aTracksProperty)
        {
            // 削除等で存在しなくなったIDは選択から取り除く
            mSelectedKeyframeIds.RemoveWhere(id => !mKeyframeLocationMap.ContainsKey(id));

            if (mSelectedKeyframeIds.Count != 1)
            {
                mOnKeyframeSelectionChanged?.Invoke(null);
                return;
            }

            string selectedId = mSelectedKeyframeIds.First();

            for (int trackIndex = 0; trackIndex < aTracksProperty.arraySize; trackIndex++)
            {
                SerializedProperty track = aTracksProperty.GetArrayElementAtIndex(trackIndex);
                if (TryFindByKeyframeId(track.FindPropertyRelative("mPositionKeyframes"), selectedId, out SerializedProperty found) ||
                    TryFindByKeyframeId(track.FindPropertyRelative("mScaleKeyframes"), selectedId, out found) ||
                    TryFindByKeyframeId(track.FindPropertyRelative("mRotationKeyframes"), selectedId, out found) ||
                    TryFindByKeyframeId(track.FindPropertyRelative("mColorKeyframes"), selectedId, out found) ||
                    TryFindByKeyframeId(track.FindPropertyRelative("mSpriteKeyframes"), selectedId, out found) ||
                    TryFindByKeyframeId(track.FindPropertyRelative("mMaterialKeyframes"), selectedId, out found))
                {
                    mOnKeyframeSelectionChanged?.Invoke(found);
                    return;
                }

                SerializedProperty paramTracksProperty = track.FindPropertyRelative("mMaterialParameterTracks");
                for (int paramIndex = 0; paramIndex < paramTracksProperty.arraySize; paramIndex++)
                {
                    SerializedProperty paramTrack = paramTracksProperty.GetArrayElementAtIndex(paramIndex);
                    if (TryFindByKeyframeId(paramTrack.FindPropertyRelative("mFloatKeyframes"), selectedId, out SerializedProperty foundParam) ||
                        TryFindByKeyframeId(paramTrack.FindPropertyRelative("mColorKeyframes"), selectedId, out foundParam) ||
                        TryFindByKeyframeId(paramTrack.FindPropertyRelative("mVector4Keyframes"), selectedId, out foundParam))
                    {
                        mOnKeyframeSelectionChanged?.Invoke(foundParam);
                        return;
                    }
                }
            }

            SerializedProperty eventKeysProperty = mEntryProperty.FindPropertyRelative("mEventKeys");
            if (TryFindByKeyframeId(eventKeysProperty, selectedId, out SerializedProperty foundEvent))
            {
                mOnKeyframeSelectionChanged?.Invoke(foundEvent);
                return;
            }

            // 削除等で見つからなくなった場合は選択解除する
            mSelectedKeyframeIds.Clear();
            mOnKeyframeSelectionChanged?.Invoke(null);
        }

        private static bool TryFindByKeyframeId(SerializedProperty aListProperty, string aKeyframeId, out SerializedProperty aFound)
        {
            for (int i = 0; i < aListProperty.arraySize; i++)
            {
                SerializedProperty element = aListProperty.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative("mKeyframeId").stringValue == aKeyframeId)
                {
                    aFound = element;
                    return true;
                }
            }
            aFound = null;
            return false;
        }

        // Ctrl+ホイールでのズーム直後など、レイアウトイベントを待たず即座に全マーカーを再配置する
        private void RepositionAllMarkersImmediate()
        {
            foreach ((VisualElement marker, SerializedProperty property) in mTrackedMarkers)
            {
                float time = property.FindPropertyRelative("mTime").floatValue;
                marker.style.left = TimeToPixel(time) - MarkerSize * 0.5f;
            }
        }

        private void UpdatePlayheadVisual()
        {
            if (mPlayheadTime == null)
            {
                return;
            }
            float x = TimeToPixel(mPlayheadTime.Value);
            mPlayhead.style.left = x - PlayheadWidth * 0.5f;
            mPlayheadHandle.style.left = x - PlayheadHandleWidth * 0.5f;
        }

        // 秒数とタイムライン内容列の座標を相互変換する。左端の操作余白を必ず含める
        private float TimeToPixel(float aTime) => TimelineStartPadding + aTime * mPixelsPerSecond;

        private float PixelToTime(float aPixel) => (aPixel - TimelineStartPadding) / mPixelsPerSecond;
    }
}
