/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PreviewAnimSequenceHost.cs
 * @author hqrse
 * @date 2026/08/22
 * @brief エディタ埋め込みプレビュー用に、IMGUIで2D描画を行うIAnimSequenceHost実装
 * =====================================*/

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AnimSequencer2D.Editor
{
    // プレビュー編集モード(一時停止中)で使うギズモの種類。SceneViewのMove/Rotate/Scaleツールに相当する
    internal enum GizmoMode
    {
        Move,
        Rotate,
        Scale,
    }

    // ギズモ操作時のスナップ間隔。各値が0以下ならそのチャンネルはスナップなし(自由入力)として扱う
    internal struct GizmoSnapSettings
    {
        public float MoveSnap;   // 位置のスナップ間隔(基準解像度上の単位)
        public float RotateSnap; // 回転のスナップ間隔(度)
        public float ScaleSnap;  // 拡大縮小のスナップ間隔(倍率)
    }

    // AnimSequencePlaybackを差し替えるだけでランタイムと同じ評価ロジックを共有できる
    internal class PreviewAnimSequenceHost : IAnimSequenceHost
    {
        // プレビューが基準とする仮想解像度。この矩形をウィンドウのプレビュー領域へ収まるよう等倍縮小して描画する
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        // 描画サイズの基準値。AnimSequencePlayerがランタイムで自動生成するImageのRectTransformは
        // Unity既定のsizeDelta(100,100)のままのため、プレビューもスプライトの実ピクセルサイズではなく
        // この既定サイズを基準にする(実ピクセルサイズを使うと高解像度スプライトでランタイムと見た目が大きく食い違う)
        private const float DefaultImageSize = 100f;

        // ギズモハンドルのヒット判定半径(スクリーンピクセル)
        private const float HandleHitRadius = 8f;
        // Z回転ハンドルを中心から離す距離(スクリーンピクセル)。現在の角度に応じて位置が変わる
        private const float RotateHandleDistance = 70f;
        // Move/ScaleのX軸・Y軸ハンドルを中心から離す距離(スクリーンピクセル、画面基準の固定位置)
        private const float AxisHandleDistance = 90f;
        // RotateのX軸・Y軸ハンドルを中心から離す距離(スクリーンピクセル、画面基準の固定位置)。
        // Zハンドル(RotateHandleDistance=70px)と重ならないよう別の距離にする
        private const float RotateAxisHandleDistance = 90f;
        // X/Y回転ハンドルのドラッグ操作の角度感度(度/ピクセル)
        private const float DegreesPerPixel = 0.5f;
        // コサインスクイッシュ近似で幅/高さが0に潰れてヒットテスト・ハンドル表示が破綻しないための下限係数
        private const float MinSquishFactor = 0.02f;

        // オブジェクト参照(AnimSequenceTrack.TrackId)の解決に使う。エントリのアイドル表示・初期配置画面の
        // 両方で、参照先AnimSequenceObjectの基準値を読み取るために必要
        private readonly AnimSequenceDefinition mDefinition;

        // トラックIDごとの最新適用状態。DrawPreviewはこれを読んで描画するだけにする
        private readonly Dictionary<string, AnimSequenceTrackState> mTrackStates = new();
        // 直近のRepaintで描画した各トラックのスクリーン矩形(回転は考慮しない簡易AABB)。ギズモのヒットテストに使う
        private readonly Dictionary<string, Rect> mLastDrawnRects = new();
        // トラックIDごとに、直近ApplyTrackStateで実際に適用したMaterial(インスタンス化していればそのコピー)。
        // AnimSequencePlayerと同じ仕組み(ResolveActiveMaterial経由でPlaybackがパラメータを直接書き込む)
        private readonly Dictionary<string, Material> mActiveMaterials = new();
        // トラックIDごとに、直近インスタンス化の元にしたMaterial(切り替え検出用。インスタンス化していない場合は未登録)
        private readonly Dictionary<string, Material> mInstantiatedFrom = new();
        // SetTargetEntry時点でのオブジェクトID→そのエントリでの参照トラックの対応表。ShouldDrawTrackが毎回この場で
        // 表示可否を解決し直す(トラックヘッダの表示上書きボタンを押した直後にも、次のRepaintで即座に反映されるようにするため)
        private readonly Dictionary<string, AnimSequenceTrack> mCurrentEntryTracksByObjectId = new();
        // true:SetTargetEntry(アニメーション編集画面のプレビュー)時、表示可否解決(DefaultVisible/VisibilityOverride)を適用する。
        // false:LoadObjectsForPlacement(初期配置画面)時、解決を行わず常に全オブジェクトを表示する
        private bool mApplyResolvedVisibility;

        // ===== ギズモ(移動/回転/拡大縮小)の選択・ドラッグ状態 =====

        // XY = 自由(既存の1ハンドル)、X/Yは各軸拘束の追加ハンドル。RotateはX/Y/Zそれぞれ独立したハンドル
        private enum DragKind { None, MoveX, MoveY, MoveXY, ScaleX, ScaleY, ScaleXY, RotateX, RotateY, RotateZ }

        private DragKind mDragKind;
        private Vector2 mDragStartMousePos;
        private AnimSequenceTrackState mDragStartState;

        // ギズモ操作の対象として選択中のトラックID(未選択はnull)
        public string SelectedTrackId { get; private set; }
        // 選択中トラックについて、直近の操作で変更されたチャンネル("Position"/"Rotation"/"Scale"/"Color"/"Sprite")の集合。
        // Spaceキーでのキーフレーム作成時に、編集したプロパティのみをキーフレーム化する目的で使う
        public HashSet<string> DirtyChannels { get; } = new();

        // Mute/Soloされているトラックの集合(エディタのプレビュー表示にのみ影響し、データには保存しない)。
        // AnimSequenceTimelineViewのトラックヘッダM/Sボタンが直接トグルする
        public HashSet<string> MutedTrackIds { get; } = new();
        public HashSet<string> SoloedTrackIds { get; } = new();

        // aDefinition : トラックのオブジェクト参照(TrackId)を解決するために保持する
        public PreviewAnimSequenceHost(AnimSequenceDefinition aDefinition)
        {
            mDefinition = aDefinition;
        }

        void IAnimSequenceHost.ApplyTrackState(string aTrackId, in AnimSequenceTrackState aState)
        {
            mTrackStates[aTrackId] = aState;
            ApplyMaterialSwitch(aTrackId, aState);
        }

        // Material切り替えを適用する。インスタンス化が有効なトラックは、切り替え元が変わった時だけ
        // new Material(...)でコピーを作り直す(AnimSequencePlayer.ApplyMaterialSwitchと同じロジック)
        private void ApplyMaterialSwitch(string aTrackId, in AnimSequenceTrackState aState)
        {
            if (aState.Material == null)
            {
                return;
            }

            if (aState.InstantiateMaterial)
            {
                if (!mInstantiatedFrom.TryGetValue(aTrackId, out Material source) || source != aState.Material)
                {
                    DestroyPreviousInstanceIfAny(aTrackId);
                    var instance = new Material(aState.Material);
                    mInstantiatedFrom[aTrackId] = aState.Material;
                    mActiveMaterials[aTrackId] = instance;
                }
            }
            else
            {
                DestroyPreviousInstanceIfAny(aTrackId);
                mInstantiatedFrom.Remove(aTrackId);
                mActiveMaterials[aTrackId] = aState.Material;
            }
        }

        // mActiveMaterials[aTrackId]が直近インスタンス化したコピーであれば破棄する(共有アセットそのものの場合は何もしない)。
        // エディタ上でランタイム生成したMaterialはDestroyImmediateで即座に解放する(AnimSequencePlayer.DestroyPreviousInstanceIfAnyと同じ考え方)
        private void DestroyPreviousInstanceIfAny(string aTrackId)
        {
            if (mInstantiatedFrom.ContainsKey(aTrackId) && mActiveMaterials.TryGetValue(aTrackId, out Material previous) && previous != null)
            {
                Object.DestroyImmediate(previous);
            }
        }

        Material IAnimSequenceHost.ResolveActiveMaterial(string aTrackId) => mActiveMaterials.GetValueOrDefault(aTrackId);

        // 選択中エントリを切り替える。参照先オブジェクトの基準値を取り直し、未再生時の見た目(アイドル状態)として即座に反映する。
        // トラックの有無に関わらず定義済みの全オブジェクトを対象にする(未選択・トラック無しでも配置画面同様に常時表示するため)
        // aEntry : 選択中のエントリ。未選択の場合はnull
        public void SetTargetEntry(AnimSequenceEntry aEntry)
        {
            // 辞書をClearする前に、インスタンス化済みのMaterialがあれば破棄する(ApplyMaterialSwitchの切り替え時と同様、
            // Clearだけでは辞書からの参照が外れるだけでネイティブリソースが解放されないため)
            foreach (string trackId in mInstantiatedFrom.Keys)
            {
                DestroyPreviousInstanceIfAny(trackId);
            }

            mTrackStates.Clear();
            mActiveMaterials.Clear();
            mInstantiatedFrom.Clear();
            mCurrentEntryTracksByObjectId.Clear();
            mApplyResolvedVisibility = true;
            ClearGizmoSelection();

            if (mDefinition == null)
            {
                return;
            }

            // このエントリのトラックをオブジェクトIDでマップ化する(aEntryがnullの場合は空のまま=全オブジェクトがトラック無し扱いになる)。
            // ShouldDrawTrackがRepaintのたびにこの対応表を読んで表示可否を解決するため、キャッシュせずここでは対応表の構築のみ行う
            if (aEntry != null)
            {
                foreach (AnimSequenceTrack track in aEntry.Tracks)
                {
                    mCurrentEntryTracksByObjectId[track.TrackId] = track;
                }
            }

            foreach (AnimSequenceObject obj in mDefinition.Objects)
            {
                mTrackStates[obj.ObjectId] = obj.ToBaseState();
                if (obj.BaseMaterial != null)
                {
                    mActiveMaterials[obj.ObjectId] = obj.BaseMaterial;
                }
            }
        }

        // 初期配置画面用。全オブジェクトの基準値をmTrackStatesへロードし、既存のDrawPreview/HandleGizmoInputで
        // そのままプレビュー・ギズモ操作できるようにする(アニメーションキー再生とは独立した表示モード)
        public void LoadObjectsForPlacement(IReadOnlyList<AnimSequenceObject> aObjects)
        {
            foreach (string trackId in mInstantiatedFrom.Keys)
            {
                DestroyPreviousInstanceIfAny(trackId);
            }

            mTrackStates.Clear();
            mActiveMaterials.Clear();
            mInstantiatedFrom.Clear();
            mCurrentEntryTracksByObjectId.Clear();
            mApplyResolvedVisibility = false; // 初期配置画面は表示可否解決の対象外。常に全オブジェクトを表示する
            ClearGizmoSelection();

            foreach (AnimSequenceObject obj in aObjects)
            {
                mTrackStates[obj.ObjectId] = obj.ToBaseState();
                if (obj.BaseMaterial != null)
                {
                    mActiveMaterials[obj.ObjectId] = obj.BaseMaterial;
                }
            }
        }

        // 指定トラックの現在の表示状態を取得する(Spaceキーでのキーフレーム作成時、編集後の値を読み出すために使う)
        public bool TryGetTrackState(string aTrackId, out AnimSequenceTrackState aState) => mTrackStates.TryGetValue(aTrackId, out aState);

        // 初期配置画面の詳細パネルでオブジェクトのフィールドがテキスト入力等で編集された際に呼ぶ。LoadObjectsForPlacementと
        // 異なり選択状態(SelectedTrackId)は変更しない。指定オブジェクトの表示状態のみを更新する
        public void RefreshObjectBaseState(AnimSequenceObject aObject)
        {
            if (aObject == null)
            {
                return;
            }
            mTrackStates[aObject.ObjectId] = aObject.ToBaseState();
            if (aObject.BaseMaterial != null)
            {
                mActiveMaterials[aObject.ObjectId] = aObject.BaseMaterial;
            }
            else
            {
                mActiveMaterials.Remove(aObject.ObjectId);
            }
        }

        // Editモードでのプレビュー内Color/Sprite編集フィールドから呼ぶ。ギズモのドラッグ操作と同じく、
        // 値を直接書き換えつつDirtyChannelsへ記録するだけに留め、実際のキーフレーム化はSpaceキー確定時に行う
        public void SetTrackColor(string aTrackId, Color aColor)
        {
            if (!mTrackStates.TryGetValue(aTrackId, out AnimSequenceTrackState state))
            {
                return;
            }
            state.Color = aColor;
            mTrackStates[aTrackId] = state;
            DirtyChannels.Add("Color");
        }

        public void SetTrackSprite(string aTrackId, Sprite aSprite)
        {
            if (!mTrackStates.TryGetValue(aTrackId, out AnimSequenceTrackState state))
            {
                return;
            }
            state.Sprite = aSprite;
            mTrackStates[aTrackId] = state;
            DirtyChannels.Add("Sprite");
        }

        // ===== キーボードNudge(エディットモードのWASD/QE/RF等) =====
        // ギズモのドラッグ操作(HandleMouseDrag)と同じく、選択中トラックの状態を直接加減算しDirtyChannelsへ記録するだけに
        // 留める。実際のキーフレーム化はキーフレーム作成ショートカット確定時に行う

        // aDirection : 移動方向(単位ベクトル相当。呼び出し側でW=(0,1)/S=(0,-1)/A=(-1,0)/D=(1,0)を渡す)
        // aSnap : 1回の押下で移動する量(既存のMoveSnapをそのまま使う)
        public void NudgePosition(Vector2 aDirection, float aSnap)
        {
            if (SelectedTrackId == null || !mTrackStates.TryGetValue(SelectedTrackId, out AnimSequenceTrackState state))
            {
                return;
            }
            state.AnchoredPosition += aDirection * Mathf.Max(0f, aSnap);
            mTrackStates[SelectedTrackId] = state;
            DirtyChannels.Add("Position");
        }

        // aSign : +1で反時計回り、-1で時計回り(呼び出し側の意味づけに委ねる) / aSnap : 1回の押下で回転する角度(RotateSnap)
        public void NudgeRotationZ(float aSign, float aSnap)
        {
            if (SelectedTrackId == null || !mTrackStates.TryGetValue(SelectedTrackId, out AnimSequenceTrackState state))
            {
                return;
            }
            Vector3 rotation = state.Rotation;
            rotation.z += aSign * Mathf.Max(0f, aSnap);
            state.Rotation = rotation;
            mTrackStates[SelectedTrackId] = state;
            DirtyChannels.Add("Rotation");
        }

        // aSign : +1で拡大、-1で縮小 / aSnap : 1回の押下で増減する量(ScaleSnap)。X/Yへ一様に適用する(既存のXY自由スケールと同じ意味づけ)
        public void NudgeScale(float aSign, float aSnap)
        {
            if (SelectedTrackId == null || !mTrackStates.TryGetValue(SelectedTrackId, out AnimSequenceTrackState state))
            {
                return;
            }
            float delta = aSign * Mathf.Max(0f, aSnap);
            state.Scale = new Vector2(Mathf.Max(0.01f, state.Scale.x + delta), Mathf.Max(0.01f, state.Scale.y + delta));
            mTrackStates[SelectedTrackId] = state;
            DirtyChannels.Add("Scale");
        }

        public void ClearGizmoSelection()
        {
            SelectedTrackId = null;
            DirtyChannels.Clear();
            mDragKind = DragKind.None;
        }

        // 初期配置画面のオブジェクト一覧パネルなど、キャンバス外のクリックからギズモの選択対象を切り替える際に使う
        // aTrackId : 選択するトラック/オブジェクトID
        public void SelectTrack(string aTrackId)
        {
            SelectedTrackId = aTrackId;
            DirtyChannels.Clear();
            mDragKind = DragKind.None;
        }

        public void ClearDirtyChannels() => DirtyChannels.Clear();

        // aRect : 描画領域全体 / aAspectRatioOrZero : 固定したいアスペクト比(幅/高さ)。0以下ならFree Aspect(aRectをそのまま使う)
        public void DrawPreview(Rect aRect, float aAspectRatioOrZero)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            ComputeLayout(aRect, aAspectRatioOrZero, out Rect drawRect, out float scale, out Vector2 origin);

            if (aAspectRatioOrZero > 0f)
            {
                // レターボックス(アスペクト比固定時の余白)。Game ビューの解像度固定表示と同様の見た目にする
                EditorGUI.DrawRect(aRect, new Color(0f, 0f, 0f, 0.6f));
            }

            // IMGUIContainerはaRectの範囲外へのGUI描画を自動でクリップしないため、
            // 内容がaRectをはみ出して他のUI要素に重なって見えないよう明示的にクリップする
            GUI.BeginClip(aRect);
            var localOrigin = new Vector2(origin.x - aRect.x, origin.y - aRect.y);

            foreach (KeyValuePair<string, AnimSequenceTrackState> pair in mTrackStates)
            {
                if (!ShouldDrawTrack(pair.Key))
                {
                    continue;
                }
                DrawTrack(pair.Key, pair.Value, localOrigin, scale);
            }

            GUI.EndClip();
        }

        // 表示可否(DefaultVisible/VisibilityOverrideの解決結果)にMute/Soloを重ねて最終的な描画要否を判定する。内部の
        // 時刻評価・キーフレーム編集には影響させない(Muteされたトラックも裏側では通常通り評価され続け、見た目の描画だけをスキップする)。
        // 優先順位: Solo(デバッグ用に解決済み非表示でも強制的に見せる) > Mute(解決済み表示でも強制的に隠す) > 解決済み表示可否。
        // 表示可否はキャッシュせずここで都度解決する(トラックヘッダの表示上書きボタンを押した直後の次のRepaintに即反映するため)
        private bool ShouldDrawTrack(string aTrackId)
        {
            if (SoloedTrackIds.Count > 0)
            {
                return SoloedTrackIds.Contains(aTrackId);
            }
            if (MutedTrackIds.Contains(aTrackId))
            {
                return false;
            }
            if (!mApplyResolvedVisibility)
            {
                return true; // 初期配置画面は常に全オブジェクトを表示する
            }

            AnimSequenceObject obj = mDefinition?.FindObject(aTrackId);
            if (obj == null)
            {
                return true; // 参照解決できない場合は表示可否の判断対象外とする(警告表示は別途行う)
            }
            return mCurrentEntryTracksByObjectId.TryGetValue(aTrackId, out AnimSequenceTrack track) ? track.ResolveVisible(obj) : obj.DefaultVisible;
        }

        // プレビュー編集モード(一時停止中)専用。ギズモの描画とドラッグ入力の処理を行う。
        // Repaintイベントではハンドルを描画し、Mouseイベントでは選択・ドラッグを処理する
        // aRect : 描画領域全体(DrawPreviewと同じ値を渡すこと) / aAspectRatioOrZero : DrawPreviewと同じ値
        // aMode : 現在アクティブなギズモの種類 / aSnap : 各チャンネルのスナップ間隔
        // 戻り値 : このイベント処理でいずれかのトラックの見た目が変化したか
        public bool HandleGizmoInput(Rect aRect, float aAspectRatioOrZero, GizmoMode aMode, GizmoSnapSettings aSnap)
        {
            ComputeLayout(aRect, aAspectRatioOrZero, out _, out float scale, out Vector2 origin);

            Event evt = Event.current;
            bool changed = false;

            // DrawPreview同様、aRect内のローカル座標で統一する(BeginClip中はEvent.current.mousePositionも
            // 自動的にローカル座標になるため、以降はaRectのオフセットを意識しなくてよい)
            GUI.BeginClip(aRect);
            var localOrigin = new Vector2(origin.x - aRect.x, origin.y - aRect.y);

            switch (evt.type)
            {
                case EventType.MouseDown:
                    if (evt.button == 0)
                    {
                        HandleMouseDown(evt.mousePosition, localOrigin, scale, aMode);
                        evt.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (mDragKind != DragKind.None)
                    {
                        HandleMouseDrag(evt.mousePosition, localOrigin, scale, aSnap);
                        changed = true;
                        evt.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (mDragKind != DragKind.None)
                    {
                        mDragKind = DragKind.None;
                        evt.Use();
                    }
                    break;

                case EventType.Repaint:
                    DrawGizmoHandles(localOrigin, scale, aMode);
                    break;
            }

            GUI.EndClip();
            return changed;
        }

        // 初期配置画面用。Spriteアセットのドラッグ&ドロップを処理する。ドロップが成立した場合、
        // ドロップされたSpriteとドロップ位置(基準Position相当のワールド座標)を返す
        // 戻り値 : ドロップが成立した場合true
        public bool HandleObjectDrop(Rect aRect, float aAspectRatioOrZero, out Sprite aDroppedSprite, out Vector2 aDropPosition)
        {
            aDroppedSprite = null;
            aDropPosition = Vector2.zero;

            Event evt = Event.current;
            if (!aRect.Contains(evt.mousePosition) || (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform))
            {
                return false;
            }

            Sprite sprite = null;
            UnityEngine.Object[] draggedObjects = DragAndDrop.objectReferences;
            for (int i = 0; i < draggedObjects.Length; i++)
            {
                if (draggedObjects[i] is Sprite draggedSprite)
                {
                    sprite = draggedSprite;
                    break;
                }
            }

            DragAndDrop.visualMode = sprite != null ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            if (evt.type != EventType.DragPerform || sprite == null)
            {
                return false;
            }

            DragAndDrop.AcceptDrag();
            ComputeLayout(aRect, aAspectRatioOrZero, out _, out float scale, out Vector2 origin);
            aDroppedSprite = sprite;
            aDropPosition = new Vector2((evt.mousePosition.x - origin.x) / scale, (origin.y - evt.mousePosition.y) / scale);
            evt.Use();
            return true;
        }

        // 編集モード時、プレビュー領域に位置スナップ間隔のグリッドを描画する(Repaintイベントでのみ)
        // aGridSpacing : グリッド間隔(基準解像度上の単位)。0以下なら描画しない
        public void DrawGrid(Rect aRect, float aAspectRatioOrZero, float aGridSpacing)
        {
            if (Event.current.type != EventType.Repaint || aGridSpacing <= 0.0001f)
            {
                return;
            }

            ComputeLayout(aRect, aAspectRatioOrZero, out Rect drawRect, out float scale, out Vector2 origin);
            float spacingPixels = aGridSpacing * scale;
            if (spacingPixels < 2f)
            {
                return; // 密集しすぎる場合は描画しない
            }

            GUI.BeginClip(aRect);
            var localOrigin = new Vector2(origin.x - aRect.x, origin.y - aRect.y);
            var localDrawRect = new Rect(drawRect.x - aRect.x, drawRect.y - aRect.y, drawRect.width, drawRect.height);

            var gridColor = new Color(1f, 1f, 1f, 0.08f);
            // 原点がプレビュー領域の外にある場合でも、範囲全体を欠けなく描画する
            int minXIndex = Mathf.FloorToInt((localDrawRect.xMin - localOrigin.x) / spacingPixels);
            int maxXIndex = Mathf.CeilToInt((localDrawRect.xMax - localOrigin.x) / spacingPixels);
            int minYIndex = Mathf.FloorToInt((localDrawRect.yMin - localOrigin.y) / spacingPixels);
            int maxYIndex = Mathf.CeilToInt((localDrawRect.yMax - localOrigin.y) / spacingPixels);

            for (int index = minXIndex; index <= maxXIndex; index++)
            {
                float x = localOrigin.x + index * spacingPixels;
                EditorGUI.DrawRect(new Rect(x, localDrawRect.yMin, 1f, localDrawRect.height), gridColor);
            }
            for (int index = minYIndex; index <= maxYIndex; index++)
            {
                float y = localOrigin.y + index * spacingPixels;
                EditorGUI.DrawRect(new Rect(localDrawRect.xMin, y, localDrawRect.width, 1f), gridColor);
            }

            // 原点(基準位置)を強調表示する
            var originColor = new Color(1f, 1f, 1f, 0.2f);
            EditorGUI.DrawRect(new Rect(localOrigin.x - 0.75f, localDrawRect.yMin, 1.5f, localDrawRect.height), originColor);
            EditorGUI.DrawRect(new Rect(localDrawRect.xMin, localOrigin.y - 0.75f, localDrawRect.width, 1.5f), originColor);

            GUI.EndClip();
        }

        private void HandleMouseDown(Vector2 aMousePos, Vector2 aOrigin, float aScale, GizmoMode aMode)
        {
            // 選択中トラックがあれば、まずアクティブなギズモのハンドルへのヒットを優先する
            if (SelectedTrackId != null && mTrackStates.TryGetValue(SelectedTrackId, out AnimSequenceTrackState selectedState))
            {
                Vector2 center = ScreenCenter(selectedState, aOrigin, aScale);

                if (aMode == GizmoMode.Move)
                {
                    if (Vector2.Distance(aMousePos, AxisXHandleScreenPos(center)) <= HandleHitRadius)
                    {
                        BeginDrag(DragKind.MoveX, aMousePos, selectedState);
                        return;
                    }
                    if (Vector2.Distance(aMousePos, AxisYHandleScreenPos(center)) <= HandleHitRadius)
                    {
                        BeginDrag(DragKind.MoveY, aMousePos, selectedState);
                        return;
                    }
                }
                else if (aMode == GizmoMode.Rotate)
                {
                    if (Vector2.Distance(aMousePos, RotateXHandleScreenPos(center)) <= HandleHitRadius)
                    {
                        BeginDrag(DragKind.RotateX, aMousePos, selectedState);
                        return;
                    }
                    if (Vector2.Distance(aMousePos, RotateYHandleScreenPos(center)) <= HandleHitRadius)
                    {
                        BeginDrag(DragKind.RotateY, aMousePos, selectedState);
                        return;
                    }
                    Vector2 zHandlePos = RotateHandleScreenPos(selectedState, center);
                    if (Vector2.Distance(aMousePos, zHandlePos) <= HandleHitRadius)
                    {
                        BeginDrag(DragKind.RotateZ, aMousePos, selectedState);
                        return;
                    }
                }
                else if (aMode == GizmoMode.Scale)
                {
                    if (Vector2.Distance(aMousePos, ScaleAxisXHandleScreenPos(selectedState, center, aScale)) <= HandleHitRadius)
                    {
                        BeginDrag(DragKind.ScaleX, aMousePos, selectedState);
                        return;
                    }
                    if (Vector2.Distance(aMousePos, ScaleAxisYHandleScreenPos(selectedState, center, aScale)) <= HandleHitRadius)
                    {
                        BeginDrag(DragKind.ScaleY, aMousePos, selectedState);
                        return;
                    }
                    Vector2 xyHandlePos = ScaleHandleScreenPos(selectedState, center, aScale);
                    if (Vector2.Distance(aMousePos, xyHandlePos) <= HandleHitRadius)
                    {
                        BeginDrag(DragKind.ScaleXY, aMousePos, selectedState);
                        return;
                    }
                }
            }

            // ハンドルに当たらなければ、いずれかのトラックの表示矩形内クリックで選択する
            // (Moveモードの場合は選択と同時にXY自由移動のドラッグを開始し、Image自体を直接つかんで動かせるようにする)
            foreach (KeyValuePair<string, Rect> pair in mLastDrawnRects)
            {
                if (!pair.Value.Contains(aMousePos))
                {
                    continue;
                }

                SelectedTrackId = pair.Key;
                DirtyChannels.Clear();
                if (aMode == GizmoMode.Move && mTrackStates.TryGetValue(pair.Key, out AnimSequenceTrackState hitState))
                {
                    BeginDrag(DragKind.MoveXY, aMousePos, hitState);
                }
                return;
            }

            // 何にも当たらなければ選択解除する
            ClearGizmoSelection();
        }

        private void BeginDrag(DragKind aKind, Vector2 aMousePos, AnimSequenceTrackState aState)
        {
            mDragKind = aKind;
            mDragStartMousePos = aMousePos;
            mDragStartState = aState;
        }

        private void HandleMouseDrag(Vector2 aMousePos, Vector2 aOrigin, float aScale, GizmoSnapSettings aSnap)
        {
            if (SelectedTrackId == null)
            {
                return;
            }

            AnimSequenceTrackState state = mDragStartState;
            Vector2 delta = aMousePos - mDragStartMousePos;

            switch (mDragKind)
            {
                case DragKind.MoveXY:
                {
                    // uGUIは+Yが上、IMGUIは+Yが下のため反転する
                    Vector2 raw = mDragStartState.AnchoredPosition + new Vector2(delta.x / aScale, -delta.y / aScale);
                    state.AnchoredPosition = new Vector2(SnapValue(raw.x, aSnap.MoveSnap), SnapValue(raw.y, aSnap.MoveSnap));
                    DirtyChannels.Add("Position");
                    break;
                }
                case DragKind.MoveX:
                {
                    float rawX = mDragStartState.AnchoredPosition.x + delta.x / aScale;
                    state.AnchoredPosition = new Vector2(SnapValue(rawX, aSnap.MoveSnap), mDragStartState.AnchoredPosition.y);
                    DirtyChannels.Add("Position");
                    break;
                }
                case DragKind.MoveY:
                {
                    // uGUIは+Yが上、IMGUIは+Yが下のため反転する
                    float rawY = mDragStartState.AnchoredPosition.y - delta.y / aScale;
                    state.AnchoredPosition = new Vector2(mDragStartState.AnchoredPosition.x, SnapValue(rawY, aSnap.MoveSnap));
                    DirtyChannels.Add("Position");
                    break;
                }

                case DragKind.RotateZ:
                {
                    Vector2 center = ScreenCenter(mDragStartState, aOrigin, aScale);
                    float startAngle = AngleDegrees(mDragStartMousePos - center);
                    float currentAngle = AngleDegrees(aMousePos - center);
                    // IMGUIの角度(時計回り正)からuGUIの回転(反時計回り正)への変換のため差分の符号を反転する
                    float rawRotation = mDragStartState.Rotation.z - (currentAngle - startAngle);
                    Vector3 rotZ = mDragStartState.Rotation;
                    rotZ.z = SnapValue(rawRotation, aSnap.RotateSnap);
                    state.Rotation = rotZ;
                    DirtyChannels.Add("Rotation");
                    break;
                }
                case DragKind.RotateX:
                {
                    // 上へドラッグ(delta.yが負)するとX軸の正方向へ回転するようにする
                    float rawX = mDragStartState.Rotation.x - delta.y * DegreesPerPixel;
                    Vector3 rotX = mDragStartState.Rotation;
                    rotX.x = SnapValue(rawX, aSnap.RotateSnap);
                    state.Rotation = rotX;
                    DirtyChannels.Add("Rotation");
                    break;
                }
                case DragKind.RotateY:
                {
                    float rawY = mDragStartState.Rotation.y + delta.x * DegreesPerPixel;
                    Vector3 rotY = mDragStartState.Rotation;
                    rotY.y = SnapValue(rawY, aSnap.RotateSnap);
                    state.Rotation = rotY;
                    DirtyChannels.Add("Rotation");
                    break;
                }

                case DragKind.ScaleXY:
                {
                    Vector2 center = ScreenCenter(mDragStartState, aOrigin, aScale);
                    float startDist = Mathf.Max(1f, Vector2.Distance(mDragStartMousePos, center));
                    float currentDist = Vector2.Distance(aMousePos, center);
                    float rawFactor = currentDist / startDist;
                    float snappedFactor = Mathf.Max(0.01f, SnapValue(rawFactor, aSnap.ScaleSnap));
                    state.Scale = mDragStartState.Scale * snappedFactor;
                    DirtyChannels.Add("Scale");
                    break;
                }
                case DragKind.ScaleX:
                {
                    Vector2 center = ScreenCenter(mDragStartState, aOrigin, aScale);
                    float startDist = Mathf.Max(1f, Mathf.Abs(mDragStartMousePos.x - center.x));
                    float currentDist = Mathf.Abs(aMousePos.x - center.x);
                    float snappedFactor = Mathf.Max(0.01f, SnapValue(currentDist / startDist, aSnap.ScaleSnap));
                    state.Scale = new Vector2(mDragStartState.Scale.x * snappedFactor, mDragStartState.Scale.y);
                    DirtyChannels.Add("Scale");
                    break;
                }
                case DragKind.ScaleY:
                {
                    Vector2 center = ScreenCenter(mDragStartState, aOrigin, aScale);
                    float startDist = Mathf.Max(1f, Mathf.Abs(mDragStartMousePos.y - center.y));
                    float currentDist = Mathf.Abs(aMousePos.y - center.y);
                    float snappedFactor = Mathf.Max(0.01f, SnapValue(currentDist / startDist, aSnap.ScaleSnap));
                    state.Scale = new Vector2(mDragStartState.Scale.x, mDragStartState.Scale.y * snappedFactor);
                    DirtyChannels.Add("Scale");
                    break;
                }
            }

            mTrackStates[SelectedTrackId] = state;
        }

        // aSnapが0以下ならスナップなし(aValueをそのまま返す)。それ以外はaSnapの倍数に丸める
        private static float SnapValue(float aValue, float aSnap) => aSnap > 0.0001f ? Mathf.Round(aValue / aSnap) * aSnap : aValue;

        private void DrawGizmoHandles(Vector2 aOrigin, float aScale, GizmoMode aMode)
        {
            if (SelectedTrackId == null || !mTrackStates.TryGetValue(SelectedTrackId, out AnimSequenceTrackState state))
            {
                return;
            }

            Vector2 center = ScreenCenter(state, aOrigin, aScale);
            Vector2 halfSize = HalfSizeOnScreen(state, aScale);
            var outlineColor = new Color(1f, 0.85f, 0.2f);
            DrawRectOutline(new Rect(center.x - halfSize.x, center.y - halfSize.y, halfSize.x * 2f, halfSize.y * 2f), outlineColor, 2f);

            var xAxisColor = new Color(1f, 0.3f, 0.3f);
            var yAxisColor = new Color(0.3f, 1f, 0.3f);
            var freeColor = new Color(0.3f, 0.7f, 1f);

            switch (aMode)
            {
                case GizmoMode.Rotate:
                {
                    Vector2 zHandlePos = RotateHandleScreenPos(state, center);
                    DrawLine(center, zHandlePos, outlineColor, 1.5f);
                    DrawHandleSquare(zHandlePos, freeColor);
                    DrawHandleSquare(RotateXHandleScreenPos(center), xAxisColor);
                    DrawHandleSquare(RotateYHandleScreenPos(center), yAxisColor);
                    break;
                }
                case GizmoMode.Scale:
                    DrawHandleSquare(ScaleHandleScreenPos(state, center, aScale), freeColor);
                    DrawHandleSquare(ScaleAxisXHandleScreenPos(state, center, aScale), xAxisColor);
                    DrawHandleSquare(ScaleAxisYHandleScreenPos(state, center, aScale), yAxisColor);
                    break;
                default: // Move
                    DrawHandleSquare(center, freeColor);
                    DrawHandleSquare(AxisXHandleScreenPos(center), xAxisColor);
                    DrawHandleSquare(AxisYHandleScreenPos(center), yAxisColor);
                    break;
            }
        }

        // aRect内に収まるよう中央配置でアスペクト比を合わせたdrawRect・仮想解像度の縮小率・原点(スクリーン座標)を求める
        private static void ComputeLayout(Rect aRect, float aAspectRatioOrZero, out Rect aDrawRect, out float aScale, out Vector2 aOrigin)
        {
            aDrawRect = aAspectRatioOrZero > 0f ? FitAspect(aRect, aAspectRatioOrZero) : aRect;
            aScale = Mathf.Min(aDrawRect.width / ReferenceWidth, aDrawRect.height / ReferenceHeight);
            aOrigin = new Vector2(aDrawRect.center.x, aDrawRect.center.y);
        }

        // aContainer内に収まるよう中央配置でアスペクト比を合わせたRectを返す(Game ビューの解像度固定表示と同じ考え方)
        // aContainer : 描画可能な領域全体 / aAspectRatio : 目標アスペクト比(幅/高さ)
        private static Rect FitAspect(Rect aContainer, float aAspectRatio)
        {
            float containerAspect = aContainer.width / aContainer.height;
            float width = containerAspect > aAspectRatio ? aContainer.height * aAspectRatio : aContainer.width;
            float height = containerAspect > aAspectRatio ? aContainer.height : aContainer.width / aAspectRatio;
            return new Rect(
                aContainer.x + (aContainer.width - width) * 0.5f,
                aContainer.y + (aContainer.height - height) * 0.5f,
                width, height);
        }

        private void DrawTrack(string aTrackId, in AnimSequenceTrackState aState, Vector2 aOrigin, float aScale)
        {
            Sprite sprite = aState.Sprite;
            Vector2 size = Vector2.Scale(Vector2.Scale(new Vector2(DefaultImageSize, DefaultImageSize), aState.Scale), SquishFactor(aState.Rotation)) * aScale;

            Vector2 center = ScreenCenter(aState, aOrigin, aScale);
            var drawRect = new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y);
            mLastDrawnRects[aTrackId] = drawRect; // 回転は考慮しない簡易AABB。ギズモのヒットテストに使う

            Matrix4x4 savedMatrix = GUI.matrix;
            Color savedColor = GUI.color;

            // uGUIのZ回転は反時計回りが正、GUIの回転は時計回りが正なので符号を反転する
            GUIUtility.RotateAroundPivot(-aState.Rotation.z, center);
            GUI.color = aState.Color;

            Material activeMaterial = mActiveMaterials.GetValueOrDefault(aTrackId);
            if (sprite != null && activeMaterial != null)
            {
                // GUI.DrawTextureWithTexCoordsにはMaterialを渡せないため、Materialを反映したい場合はこちらを使う。
                // ただしアトラス内の部分矩形(UV)は指定できないため、アトラス化されたSpriteの場合はテクスチャ全体が
                // 縮小表示される近似になる(SPEC.mdの「可能な限り正確に」の範囲内の既知の制約として許容する)
                EditorGUI.DrawPreviewTexture(drawRect, sprite.texture, activeMaterial, ScaleMode.StretchToFill);
            }
            else if (sprite != null)
            {
                // アトラス化されたスプライトでも正しい領域を切り出すため、テクスチャ内の正規化矩形を指定して描画する
                Rect textureRect = sprite.textureRect;
                var normalizedRect = new Rect(
                    textureRect.x / sprite.texture.width,
                    textureRect.y / sprite.texture.height,
                    textureRect.width / sprite.texture.width,
                    textureRect.height / sprite.texture.height);
                GUI.DrawTextureWithTexCoords(drawRect, sprite.texture, normalizedRect);
            }
            else
            {
                EditorGUI.DrawRect(drawRect, new Color(1f, 1f, 1f, 0.25f));
            }

            GUI.color = savedColor;
            GUI.matrix = savedMatrix;
        }

        private static Vector2 ScreenCenter(in AnimSequenceTrackState aState, Vector2 aOrigin, float aScale)
            => new(aOrigin.x + aState.AnchoredPosition.x * aScale, aOrigin.y - aState.AnchoredPosition.y * aScale);

        private static Vector2 HalfSizeOnScreen(in AnimSequenceTrackState aState, float aScale)
            => Vector2.Scale(Vector2.Scale(new Vector2(DefaultImageSize, DefaultImageSize) * 0.5f, aState.Scale), SquishFactor(aState.Rotation)) * aScale;

        // X/Y軸回転による疑似3D的な見た目の近似(コサインスクイッシュ)。Y回転で幅を、X回転で高さを縮める。
        // 90度に近づくほど0に近づくが、ヒットテスト・ハンドル表示が破綻しないようMinSquishFactorで下限を設ける
        private static Vector2 SquishFactor(Vector3 aRotation) => new(
            Mathf.Max(MinSquishFactor, Mathf.Abs(Mathf.Cos(aRotation.y * Mathf.Deg2Rad))),
            Mathf.Max(MinSquishFactor, Mathf.Abs(Mathf.Cos(aRotation.x * Mathf.Deg2Rad))));

        // Z回転ハンドルの位置。現在のRotation.zの方向(uGUI基準)へ、中心からRotateHandleDistanceだけ離れた位置に置く
        private static Vector2 RotateHandleScreenPos(in AnimSequenceTrackState aState, Vector2 aCenter)
        {
            float angleRad = -aState.Rotation.z * Mathf.Deg2Rad - Mathf.PI * 0.5f;
            return aCenter + new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * RotateHandleDistance;
        }

        // X回転ハンドルの位置。画面基準で右側に固定(現在の角度には追従しない)。垂直方向のドラッグで操作する
        private static Vector2 RotateXHandleScreenPos(Vector2 aCenter) => aCenter + new Vector2(RotateAxisHandleDistance, 0f);

        // Y回転ハンドルの位置。画面基準で右上に固定(現在の角度には追従しない)。水平方向のドラッグで操作する
        private static Vector2 RotateYHandleScreenPos(Vector2 aCenter) => aCenter + new Vector2(RotateAxisHandleDistance * 0.7071f, -RotateAxisHandleDistance * 0.7071f);

        // Move/ScaleのX軸ハンドルの位置。画面基準で右側に固定
        private static Vector2 AxisXHandleScreenPos(Vector2 aCenter) => aCenter + new Vector2(AxisHandleDistance, 0f);

        // Move/ScaleのY軸ハンドルの位置。画面基準で上側に固定
        private static Vector2 AxisYHandleScreenPos(Vector2 aCenter) => aCenter + new Vector2(0f, -AxisHandleDistance);

        // 拡大縮小(XY自由/一様)ハンドルの位置。右下角に置く(簡易実装のため現在の回転は考慮しない)
        private static Vector2 ScaleHandleScreenPos(in AnimSequenceTrackState aState, Vector2 aCenter, float aScale)
            => aCenter + HalfSizeOnScreen(aState, aScale);

        // ScaleモードのX軸ハンドル位置。既存のXY自由ハンドル(ScaleHandleScreenPos)と同じHalfSizeOnScreenを使い、
        // 見た目のサイズ(Scale・コサインスクイッシュ反映済み)に追従させる。Move用のAxisXHandleScreenPos(固定距離)とは別物
        private static Vector2 ScaleAxisXHandleScreenPos(in AnimSequenceTrackState aState, Vector2 aCenter, float aScale)
            => aCenter + new Vector2(HalfSizeOnScreen(aState, aScale).x, 0f);

        // ScaleモードのY軸ハンドル位置。ScaleAxisXHandleScreenPosと同様、見た目のサイズに追従させる
        private static Vector2 ScaleAxisYHandleScreenPos(in AnimSequenceTrackState aState, Vector2 aCenter, float aScale)
            => aCenter + new Vector2(0f, -HalfSizeOnScreen(aState, aScale).y);

        private static float AngleDegrees(Vector2 aVector) => Mathf.Atan2(aVector.y, aVector.x) * Mathf.Rad2Deg;

        private static void DrawHandleSquare(Vector2 aCenter, Color aColor)
        {
            EditorGUI.DrawRect(new Rect(aCenter.x - HandleHitRadius * 0.5f, aCenter.y - HandleHitRadius * 0.5f, HandleHitRadius, HandleHitRadius), aColor);
        }

        private static void DrawRectOutline(Rect aRect, Color aColor, float aThickness)
        {
            EditorGUI.DrawRect(new Rect(aRect.x, aRect.y, aRect.width, aThickness), aColor);
            EditorGUI.DrawRect(new Rect(aRect.x, aRect.yMax - aThickness, aRect.width, aThickness), aColor);
            EditorGUI.DrawRect(new Rect(aRect.x, aRect.y, aThickness, aRect.height), aColor);
            EditorGUI.DrawRect(new Rect(aRect.xMax - aThickness, aRect.y, aThickness, aRect.height), aColor);
        }

        private static void DrawLine(Vector2 aFrom, Vector2 aTo, Color aColor, float aThickness)
        {
            Vector2 delta = aTo - aFrom;
            float length = delta.magnitude;
            if (length < 0.01f)
            {
                return;
            }
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            Matrix4x4 savedMatrix = GUI.matrix;
            Color savedColor = GUI.color;
            GUIUtility.RotateAroundPivot(angle, aFrom);
            GUI.color = aColor;
            GUI.DrawTexture(new Rect(aFrom.x, aFrom.y - aThickness * 0.5f, length, aThickness), Texture2D.whiteTexture);
            GUI.color = savedColor;
            GUI.matrix = savedMatrix;
        }
    }
}
