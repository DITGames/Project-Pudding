/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file AnimSequenceEntryGraphView.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief アニメーションキー(AnimSequenceEntry)の一覧をノードグラフとして表示・編集するGraphView
 * ノード=アニメーションキー、ノード間の接続線=Transition設定。ノードの追加・削除・接続・移動は
 * SerializedProperty経由でアセットへ反映し、Undo/Redo・Dirty管理を得る(VFXSequencerGraphViewと同じ方針)
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace AnimSequencer2D.Editor
{
    internal class AnimSequenceEntryGraphView : GraphView
    {
        private readonly SerializedObject mSerializedObject;
        private readonly AnimSequenceDefinition mDefinition;
        private readonly Dictionary<string, AnimSequenceEntryNodeView> mNodeViews = new();
        // 現在プレビュー再生中としてハイライト表示しているアニメーションキー(未再生時はnull)
        private string mPlayingKey;

        // 選択中ノードが変わった際に、対応するSerializedPropertyを渡して通知する(未選択時はnull)
        public event Action<SerializedProperty> OnEntrySelectionChanged;
        // ノードの追加・削除・接続変更でグラフ構造が変わった際に通知する(警告表示更新等に使う)
        public event Action OnGraphStructureChanged;

        // aSerializedObject : mTargetのSerializedObject / aDefinition : 表示対象のエントリ一覧
        public AnimSequenceEntryGraphView(SerializedObject aSerializedObject, AnimSequenceDefinition aDefinition)
        {
            mSerializedObject = aSerializedObject;
            mDefinition = aDefinition;

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            graphViewChanged = OnGraphViewChanged;

            RebuildFromDefinition();

            // 初回表示時に全ノードが収まるようズーム倍率と表示位置を調整する。生成直後はレイアウトが未確定で
            // FrameAll()が正しい矩形を計算できないため、最初のGeometryChangedEventまで待ってから一度だけ実行する
            RegisterCallback<GeometryChangedEvent>(OnFirstGeometryChanged);
        }

        // 初回のレイアウト確定時に全ノードを表示範囲へ収める(以降のリサイズでは表示をユーザー操作に任せる)
        private void OnFirstGeometryChanged(GeometryChangedEvent aEvent)
        {
            UnregisterCallback<GeometryChangedEvent>(OnFirstGeometryChanged);
            FrameAll();
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            Vector2 graphPosition = contentViewContainer.WorldToLocal(evt.mousePosition);
            evt.menu.AppendAction("新規アニメーションキーを追加", _ => AddEntry(graphPosition));
            base.BuildContextualMenu(evt);
        }

        public override List<Port> GetCompatiblePorts(Port aStartPort, NodeAdapter aNodeAdapter)
        {
            // 自己遷移(SPECで許容)を接続可能にするため、p.node != aStartPort.node の制約は付けない
            return ports.ToList().Where(p => p != aStartPort && p.direction != aStartPort.direction).ToList();
        }

        // Key変更(Inspectorでのリネーム)をグラフのノードタイトルへ即時反映する。AnimSequencerWindowから呼ぶ
        // aOldKey : リネーム前のキー(ノード検索用) / aNewKey : リネーム後のキー
        public void RefreshNodeTitle(string aOldKey, string aNewKey)
        {
            if (string.IsNullOrEmpty(aOldKey) || aOldKey == aNewKey)
            {
                return;
            }
            if (!mNodeViews.TryGetValue(aOldKey, out AnimSequenceEntryNodeView nodeView))
            {
                return;
            }
            mNodeViews.Remove(aOldKey);
            mNodeViews[aNewKey] = nodeView;
            nodeView.RefreshTitle(aNewKey);
        }

        // 指定キーのノードを選択し、グラフ表示の中央へ移動する
        // aKey : 選択・フォーカスするアニメーションキー
        public void SelectAndFocusEntry(string aKey)
        {
            if (!mNodeViews.TryGetValue(aKey, out AnimSequenceEntryNodeView nodeView))
            {
                return;
            }

            ClearSelection();
            AddToSelection(nodeView);

            Vector2 nodeCenter = nodeView.GetPosition().center;
            Vector3 scale = resolvedStyle.scale.value;
            Vector3 position = new(
                resolvedStyle.width * 0.5f - nodeCenter.x * scale.x,
                resolvedStyle.height * 0.5f - nodeCenter.y * scale.y,
                0f);
            UpdateViewTransform(position, scale);
            nodeView.Focus();
        }

        // 外部要因(Undo/Redo等)でmDefinitionのデータが変わった際に、グラフの表示を最新の内容へ再構築する
        public void RefreshFromExternalChange() => RebuildFromDefinition();

        // プレビュー再生中のアニメーションキーをハイライトする。null/空文字列で解除する。AnimSequencerWindowから呼ぶ
        public void SetPlayingKey(string aKey)
        {
            if (mPlayingKey == aKey)
            {
                return;
            }
            if (mPlayingKey != null && mNodeViews.TryGetValue(mPlayingKey, out AnimSequenceEntryNodeView previous))
            {
                previous.SetPlaying(false);
            }
            mPlayingKey = aKey;
            if (mPlayingKey != null && mNodeViews.TryGetValue(mPlayingKey, out AnimSequenceEntryNodeView current))
            {
                current.SetPlaying(true);
            }
        }

        private void RebuildFromDefinition()
        {
            foreach (AnimSequenceEntryNodeView nodeView in mNodeViews.Values)
            {
                RemoveElement(nodeView);
            }
            mNodeViews.Clear();

            foreach (AnimSequenceEntry entry in mDefinition.Entries)
            {
                CreateNodeView(entry);
            }

            foreach (AnimSequenceEntry entry in mDefinition.Entries)
            {
                if (entry.EndBehavior != AnimSequenceEndBehavior.Transition)
                {
                    continue;
                }
                if (!mNodeViews.TryGetValue(entry.Key, out AnimSequenceEntryNodeView source))
                {
                    continue;
                }
                // 未解決の遷移先(存在しないキー)は接続線を描かない。HelpBoxの警告表示で別途通知される
                if (!mNodeViews.TryGetValue(entry.TransitionTargetKey, out AnimSequenceEntryNodeView target))
                {
                    continue;
                }

                Edge edge = source.OutputPort.ConnectTo(target.InputPort);
                AddElement(edge);
            }

            // 再構築でノードが作り直されるため、再生中ハイライトが立っていれば新しいノードへ再適用する
            if (mPlayingKey != null && mNodeViews.TryGetValue(mPlayingKey, out AnimSequenceEntryNodeView playingNode))
            {
                playingNode.SetPlaying(true);
            }
        }

        private void CreateNodeView(AnimSequenceEntry aEntry)
        {
            var nodeView = new AnimSequenceEntryNodeView(aEntry.Key, aEntry.GraphPosition);
            nodeView.OnDuplicateRequested += DuplicateEntry;
            mNodeViews[aEntry.Key] = nodeView;
            AddElement(nodeView);
        }

        // 新規エントリを追加する。InsertArrayElementAtIndexは直前要素のコピーになるため、全フィールドを明示的に初期化する
        // aPosition : グラフ上の追加位置
        private void AddEntry(Vector2 aPosition)
        {
            SerializedProperty entriesProperty = mSerializedObject.FindProperty("mEntries");
            int index = entriesProperty.arraySize;
            entriesProperty.InsertArrayElementAtIndex(index);
            SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(index);
            SerializedProperty keyProperty = entry.FindPropertyRelative("mKey");

            string key = MakeUniqueKey(entriesProperty, keyProperty, "NewAnimation");
            keyProperty.stringValue = key;
            entry.FindPropertyRelative("mDuration").floatValue = 1f;
            entry.FindPropertyRelative("mTracks").ClearArray();
            entry.FindPropertyRelative("mEventKeys").ClearArray();
            entry.FindPropertyRelative("mEndBehavior").enumValueIndex = (int)AnimSequenceEndBehavior.Stop;
            entry.FindPropertyRelative("mTransitionTargetKey").stringValue = string.Empty;
            entry.FindPropertyRelative("mTimeMode").enumValueIndex = (int)AnimSequenceTimeMode.Scaled;
            entry.FindPropertyRelative("mPlayWhilePaused").boolValue = false;
            entry.FindPropertyRelative("mGraphPosition").vector2Value = aPosition;

            mSerializedObject.ApplyModifiedProperties();

            var nodeView = new AnimSequenceEntryNodeView(key, aPosition);
            nodeView.OnDuplicateRequested += DuplicateEntry;
            mNodeViews[key] = nodeView;
            AddElement(nodeView);
            OnGraphStructureChanged?.Invoke();
        }

        // 指定キーのエントリを複製する(全トラック・キーフレーム・EventKeys・Transition設定含め、複製元と同じ内容をコピーする)
        // aSourceKey : 複製元のアニメーションキー
        private void DuplicateEntry(string aSourceKey)
        {
            SerializedProperty entriesProperty = mSerializedObject.FindProperty("mEntries");
            int sourceIndex = FindEntryIndex(entriesProperty, aSourceKey);
            if (sourceIndex < 0)
            {
                return;
            }

            // Unity標準APIには任意の入れ子構造(SerializedProperty)を丸ごとコピーする手段が無いため、
            // 既知のフィールド構成に沿って明示的にコピーする(CopyEntryFields、末尾に追加してから内容を埋める)
            SerializedProperty source = entriesProperty.GetArrayElementAtIndex(sourceIndex);
            int newIndex = entriesProperty.arraySize;
            entriesProperty.InsertArrayElementAtIndex(newIndex);
            SerializedProperty duplicated = entriesProperty.GetArrayElementAtIndex(newIndex);
            CopyEntryFields(duplicated, source);

            SerializedProperty keyProperty = duplicated.FindPropertyRelative("mKey");
            string newKey = MakeUniqueKey(entriesProperty, keyProperty, keyProperty.stringValue);
            keyProperty.stringValue = newKey;

            SerializedProperty graphPositionProperty = duplicated.FindPropertyRelative("mGraphPosition");
            Vector2 offsetPosition = graphPositionProperty.vector2Value + new Vector2(40f, 40f);
            graphPositionProperty.vector2Value = offsetPosition;
            // TransitionTargetKey・EndBehavior・Duration等はSPEC通りコピーされたまま変更しない

            mSerializedObject.ApplyModifiedProperties();

            var nodeView = new AnimSequenceEntryNodeView(newKey, offsetPosition);
            nodeView.OnDuplicateRequested += DuplicateEntry;
            mNodeViews[newKey] = nodeView;
            AddElement(nodeView);
            OnGraphStructureChanged?.Invoke();
        }

        private static int FindEntryIndex(SerializedProperty aEntriesProperty, string aKey)
        {
            for (int i = 0; i < aEntriesProperty.arraySize; i++)
            {
                if (aEntriesProperty.GetArrayElementAtIndex(i).FindPropertyRelative("mKey").stringValue == aKey)
                {
                    return i;
                }
            }
            return -1;
        }

        // ===== SerializedPropertyの明示的なディープコピー(複製機能専用) =====
        // Unity標準にはSerializedProperty同士を丸ごとコピーするAPIが無いため、既知のフィールド構成に沿って
        // 手動でコピーする。複製したキーフレームのmKeyframeIdは複製元と重複しないよう、コピーの時点で振り直す

        // aTarget : 複製先(末尾に追加済みの空のエントリ) / aSource : 複製元エントリ
        internal static void CopyEntryFields(SerializedProperty aTarget, SerializedProperty aSource)
        {
            aTarget.FindPropertyRelative("mKey").stringValue = aSource.FindPropertyRelative("mKey").stringValue;
            aTarget.FindPropertyRelative("mDuration").floatValue = aSource.FindPropertyRelative("mDuration").floatValue;
            aTarget.FindPropertyRelative("mEndBehavior").enumValueIndex = aSource.FindPropertyRelative("mEndBehavior").enumValueIndex;
            aTarget.FindPropertyRelative("mTransitionTargetKey").stringValue = aSource.FindPropertyRelative("mTransitionTargetKey").stringValue;
            aTarget.FindPropertyRelative("mTimeMode").enumValueIndex = aSource.FindPropertyRelative("mTimeMode").enumValueIndex;
            aTarget.FindPropertyRelative("mPlayWhilePaused").boolValue = aSource.FindPropertyRelative("mPlayWhilePaused").boolValue;
            aTarget.FindPropertyRelative("mGraphPosition").vector2Value = aSource.FindPropertyRelative("mGraphPosition").vector2Value;

            CopyTracksList(aTarget.FindPropertyRelative("mTracks"), aSource.FindPropertyRelative("mTracks"));
            CopyEventKeysList(aTarget.FindPropertyRelative("mEventKeys"), aSource.FindPropertyRelative("mEventKeys"));
        }

        // aTarget : 複製先(末尾に追加済みの空のトラック) / aSource : 複製元トラック。エントリ複製(CopyEntryFields)から使う
        internal static void CopyTrackFields(SerializedProperty aTarget, SerializedProperty aSource)
        {
            aTarget.FindPropertyRelative("mTrackId").stringValue = aSource.FindPropertyRelative("mTrackId").stringValue;
            CopyVector2KeyframeList(aTarget.FindPropertyRelative("mPositionKeyframes"), aSource.FindPropertyRelative("mPositionKeyframes"));
            CopyVector2KeyframeList(aTarget.FindPropertyRelative("mScaleKeyframes"), aSource.FindPropertyRelative("mScaleKeyframes"));
            CopyVector3KeyframeList(aTarget.FindPropertyRelative("mRotationKeyframes"), aSource.FindPropertyRelative("mRotationKeyframes"));
            CopyColorKeyframeList(aTarget.FindPropertyRelative("mColorKeyframes"), aSource.FindPropertyRelative("mColorKeyframes"));
            CopySpriteKeyframeList(aTarget.FindPropertyRelative("mSpriteKeyframes"), aSource.FindPropertyRelative("mSpriteKeyframes"));
            CopyMaterialKeyframeList(aTarget.FindPropertyRelative("mMaterialKeyframes"), aSource.FindPropertyRelative("mMaterialKeyframes"));
            CopyMaterialParameterTrackList(aTarget.FindPropertyRelative("mMaterialParameterTracks"), aSource.FindPropertyRelative("mMaterialParameterTracks"));
        }

        private static void CopyTracksList(SerializedProperty aTargetList, SerializedProperty aSourceList)
        {
            aTargetList.ClearArray();
            for (int i = 0; i < aSourceList.arraySize; i++)
            {
                aTargetList.InsertArrayElementAtIndex(i);
                CopyTrackFields(aTargetList.GetArrayElementAtIndex(i), aSourceList.GetArrayElementAtIndex(i));
            }
        }

        private static void CopyEventKeysList(SerializedProperty aTargetList, SerializedProperty aSourceList)
        {
            aTargetList.ClearArray();
            for (int i = 0; i < aSourceList.arraySize; i++)
            {
                aTargetList.InsertArrayElementAtIndex(i);
                SerializedProperty t = aTargetList.GetArrayElementAtIndex(i);
                SerializedProperty s = aSourceList.GetArrayElementAtIndex(i);
                t.FindPropertyRelative("mTime").floatValue = s.FindPropertyRelative("mTime").floatValue;
                t.FindPropertyRelative("mEventKey").stringValue = s.FindPropertyRelative("mEventKey").stringValue;
                t.FindPropertyRelative("mKeyframeId").stringValue = Guid.NewGuid().ToString("N");
            }
        }

        private static void CopyVector2KeyframeList(SerializedProperty aTargetList, SerializedProperty aSourceList)
        {
            aTargetList.ClearArray();
            for (int i = 0; i < aSourceList.arraySize; i++)
            {
                aTargetList.InsertArrayElementAtIndex(i);
                SerializedProperty t = aTargetList.GetArrayElementAtIndex(i);
                SerializedProperty s = aSourceList.GetArrayElementAtIndex(i);
                t.FindPropertyRelative("mTime").floatValue = s.FindPropertyRelative("mTime").floatValue;
                t.FindPropertyRelative("mValue").vector2Value = s.FindPropertyRelative("mValue").vector2Value;
                t.FindPropertyRelative("mKeyframeId").stringValue = Guid.NewGuid().ToString("N");
            }
        }

        private static void CopyVector3KeyframeList(SerializedProperty aTargetList, SerializedProperty aSourceList)
        {
            aTargetList.ClearArray();
            for (int i = 0; i < aSourceList.arraySize; i++)
            {
                aTargetList.InsertArrayElementAtIndex(i);
                SerializedProperty t = aTargetList.GetArrayElementAtIndex(i);
                SerializedProperty s = aSourceList.GetArrayElementAtIndex(i);
                t.FindPropertyRelative("mTime").floatValue = s.FindPropertyRelative("mTime").floatValue;
                t.FindPropertyRelative("mValue").vector3Value = s.FindPropertyRelative("mValue").vector3Value;
                t.FindPropertyRelative("mKeyframeId").stringValue = Guid.NewGuid().ToString("N");
            }
        }

        private static void CopyColorKeyframeList(SerializedProperty aTargetList, SerializedProperty aSourceList)
        {
            aTargetList.ClearArray();
            for (int i = 0; i < aSourceList.arraySize; i++)
            {
                aTargetList.InsertArrayElementAtIndex(i);
                SerializedProperty t = aTargetList.GetArrayElementAtIndex(i);
                SerializedProperty s = aSourceList.GetArrayElementAtIndex(i);
                t.FindPropertyRelative("mTime").floatValue = s.FindPropertyRelative("mTime").floatValue;
                t.FindPropertyRelative("mValue").colorValue = s.FindPropertyRelative("mValue").colorValue;
                t.FindPropertyRelative("mKeyframeId").stringValue = Guid.NewGuid().ToString("N");
            }
        }

        private static void CopySpriteKeyframeList(SerializedProperty aTargetList, SerializedProperty aSourceList)
        {
            aTargetList.ClearArray();
            for (int i = 0; i < aSourceList.arraySize; i++)
            {
                aTargetList.InsertArrayElementAtIndex(i);
                SerializedProperty t = aTargetList.GetArrayElementAtIndex(i);
                SerializedProperty s = aSourceList.GetArrayElementAtIndex(i);
                t.FindPropertyRelative("mTime").floatValue = s.FindPropertyRelative("mTime").floatValue;
                t.FindPropertyRelative("mSprite").objectReferenceValue = s.FindPropertyRelative("mSprite").objectReferenceValue;
                t.FindPropertyRelative("mKeyframeId").stringValue = Guid.NewGuid().ToString("N");
            }
        }

        private static void CopyMaterialKeyframeList(SerializedProperty aTargetList, SerializedProperty aSourceList)
        {
            aTargetList.ClearArray();
            for (int i = 0; i < aSourceList.arraySize; i++)
            {
                aTargetList.InsertArrayElementAtIndex(i);
                SerializedProperty t = aTargetList.GetArrayElementAtIndex(i);
                SerializedProperty s = aSourceList.GetArrayElementAtIndex(i);
                t.FindPropertyRelative("mTime").floatValue = s.FindPropertyRelative("mTime").floatValue;
                t.FindPropertyRelative("mMaterial").objectReferenceValue = s.FindPropertyRelative("mMaterial").objectReferenceValue;
                t.FindPropertyRelative("mKeyframeId").stringValue = Guid.NewGuid().ToString("N");
            }
        }

        private static void CopyFloatKeyframeList(SerializedProperty aTargetList, SerializedProperty aSourceList)
        {
            aTargetList.ClearArray();
            for (int i = 0; i < aSourceList.arraySize; i++)
            {
                aTargetList.InsertArrayElementAtIndex(i);
                SerializedProperty t = aTargetList.GetArrayElementAtIndex(i);
                SerializedProperty s = aSourceList.GetArrayElementAtIndex(i);
                t.FindPropertyRelative("mTime").floatValue = s.FindPropertyRelative("mTime").floatValue;
                t.FindPropertyRelative("mValue").floatValue = s.FindPropertyRelative("mValue").floatValue;
                t.FindPropertyRelative("mKeyframeId").stringValue = Guid.NewGuid().ToString("N");
            }
        }

        private static void CopyVector4KeyframeList(SerializedProperty aTargetList, SerializedProperty aSourceList)
        {
            aTargetList.ClearArray();
            for (int i = 0; i < aSourceList.arraySize; i++)
            {
                aTargetList.InsertArrayElementAtIndex(i);
                SerializedProperty t = aTargetList.GetArrayElementAtIndex(i);
                SerializedProperty s = aSourceList.GetArrayElementAtIndex(i);
                t.FindPropertyRelative("mTime").floatValue = s.FindPropertyRelative("mTime").floatValue;
                t.FindPropertyRelative("mValue").vector4Value = s.FindPropertyRelative("mValue").vector4Value;
                t.FindPropertyRelative("mKeyframeId").stringValue = Guid.NewGuid().ToString("N");
            }
        }

        // Materialパラメータトラックのリストを複製する(プロパティ名+型+3種のキーフレームリストをまとめて持つネスト構造)
        private static void CopyMaterialParameterTrackList(SerializedProperty aTargetList, SerializedProperty aSourceList)
        {
            aTargetList.ClearArray();
            for (int i = 0; i < aSourceList.arraySize; i++)
            {
                aTargetList.InsertArrayElementAtIndex(i);
                SerializedProperty t = aTargetList.GetArrayElementAtIndex(i);
                SerializedProperty s = aSourceList.GetArrayElementAtIndex(i);
                t.FindPropertyRelative("mPropertyName").stringValue = s.FindPropertyRelative("mPropertyName").stringValue;
                t.FindPropertyRelative("mType").enumValueIndex = s.FindPropertyRelative("mType").enumValueIndex;
                CopyFloatKeyframeList(t.FindPropertyRelative("mFloatKeyframes"), s.FindPropertyRelative("mFloatKeyframes"));
                CopyColorKeyframeList(t.FindPropertyRelative("mColorKeyframes"), s.FindPropertyRelative("mColorKeyframes"));
                CopyVector4KeyframeList(t.FindPropertyRelative("mVector4Keyframes"), s.FindPropertyRelative("mVector4Keyframes"));
            }
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange aChange)
        {
            bool modified = false;

            if (aChange.elementsToRemove != null)
            {
                foreach (GraphElement element in aChange.elementsToRemove)
                {
                    switch (element)
                    {
                        case Edge edge:
                            DisconnectEdge(edge);
                            modified = true;
                            break;
                        case AnimSequenceEntryNodeView nodeView:
                            RemoveEntry(nodeView);
                            modified = true;
                            break;
                    }
                }
            }

            if (aChange.edgesToCreate != null)
            {
                foreach (Edge edge in aChange.edgesToCreate)
                {
                    ConnectEdge(edge);
                    modified = true;
                }
            }

            if (aChange.movedElements != null)
            {
                foreach (GraphElement element in aChange.movedElements)
                {
                    if (element is AnimSequenceEntryNodeView nodeView)
                    {
                        UpdateEntryPosition(nodeView);
                        modified = true;
                    }
                }
            }

            if (modified)
            {
                mSerializedObject.ApplyModifiedProperties();
                OnGraphStructureChanged?.Invoke();
            }

            return aChange;
        }

        // 接続元エントリのEndBehaviorをTransitionにし、TransitionTargetKeyを接続先のKeyに設定する
        private void ConnectEdge(Edge aEdge)
        {
            if (aEdge.output?.node is not AnimSequenceEntryNodeView sourceView || aEdge.input?.node is not AnimSequenceEntryNodeView targetView)
            {
                return;
            }

            SerializedProperty entryProperty = FindEntryProperty(sourceView.Key);
            if (entryProperty == null)
            {
                return;
            }

            entryProperty.FindPropertyRelative("mEndBehavior").enumValueIndex = (int)AnimSequenceEndBehavior.Transition;
            entryProperty.FindPropertyRelative("mTransitionTargetKey").stringValue = targetView.Key;
        }

        // 接続元エントリのEndBehaviorをStopへ戻し、TransitionTargetKeyを空にする
        private void DisconnectEdge(Edge aEdge)
        {
            if (aEdge.output?.node is not AnimSequenceEntryNodeView sourceView)
            {
                return;
            }

            SerializedProperty entryProperty = FindEntryProperty(sourceView.Key);
            if (entryProperty == null)
            {
                return;
            }

            entryProperty.FindPropertyRelative("mEndBehavior").enumValueIndex = (int)AnimSequenceEndBehavior.Stop;
            entryProperty.FindPropertyRelative("mTransitionTargetKey").stringValue = string.Empty;
        }

        private void RemoveEntry(AnimSequenceEntryNodeView aNodeView)
        {
            mNodeViews.Remove(aNodeView.Key);

            SerializedProperty entriesProperty = mSerializedObject.FindProperty("mEntries");
            for (int i = 0; i < entriesProperty.arraySize; i++)
            {
                if (entriesProperty.GetArrayElementAtIndex(i).FindPropertyRelative("mKey").stringValue == aNodeView.Key)
                {
                    entriesProperty.DeleteArrayElementAtIndex(i);
                    break;
                }
            }

            // 他エントリの残存TransitionTargetKeyはダングリング参照として残す。
            // 既存の「存在しない遷移先キーへの警告フォールバック」がそのまま機能するため、ここでは触らない
        }

        private void UpdateEntryPosition(AnimSequenceEntryNodeView aNodeView)
        {
            SerializedProperty entryProperty = FindEntryProperty(aNodeView.Key);
            if (entryProperty == null)
            {
                return;
            }
            entryProperty.FindPropertyRelative("mGraphPosition").vector2Value = aNodeView.GetPosition().position;
        }

        private SerializedProperty FindEntryProperty(string aKey)
        {
            SerializedProperty entriesProperty = mSerializedObject.FindProperty("mEntries");
            for (int i = 0; i < entriesProperty.arraySize; i++)
            {
                SerializedProperty element = entriesProperty.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative("mKey").stringValue == aKey)
                {
                    return element;
                }
            }
            return null;
        }

        // 既存のアニメーションキーと重複しない名前を生成する。AnimSequenceInspectorPanelのリネーム時とも共用する
        // aEntriesProperty : mEntriesのSerializedProperty / aSelfKeyProperty : 重複チェックから除外する自分自身のmKey(新規追加時は追加した要素自身)
        // aBaseName : 基準名
        internal static string MakeUniqueKey(SerializedProperty aEntriesProperty, SerializedProperty aSelfKeyProperty, string aBaseName)
        {
            string baseName = string.IsNullOrEmpty(aBaseName) ? "NewAnimation" : aBaseName;
            string candidate = baseName;
            int suffix = 1;
            while (IsKeyUsedByOther(aEntriesProperty, aSelfKeyProperty, candidate))
            {
                candidate = $"{baseName}_{suffix}";
                suffix++;
            }
            return candidate;
        }

        private static bool IsKeyUsedByOther(SerializedProperty aEntriesProperty, SerializedProperty aSelfKeyProperty, string aKey)
        {
            for (int i = 0; i < aEntriesProperty.arraySize; i++)
            {
                SerializedProperty keyProperty = aEntriesProperty.GetArrayElementAtIndex(i).FindPropertyRelative("mKey");
                if (aSelfKeyProperty != null && SerializedProperty.EqualContents(keyProperty, aSelfKeyProperty))
                {
                    continue; // 自分自身は除外
                }
                if (keyProperty.stringValue == aKey)
                {
                    return true;
                }
            }
            return false;
        }

        // ===== 選択通知 =====

        public override void AddToSelection(ISelectable aSelectable)
        {
            base.AddToSelection(aSelectable);
            NotifySelectionChanged();
        }

        public override void RemoveFromSelection(ISelectable aSelectable)
        {
            base.RemoveFromSelection(aSelectable);
            NotifySelectionChanged();
        }

        public override void ClearSelection()
        {
            base.ClearSelection();
            NotifySelectionChanged();
        }

        private void NotifySelectionChanged()
        {
            if (selection.Count == 1 && selection[0] is AnimSequenceEntryNodeView nodeView)
            {
                OnEntrySelectionChanged?.Invoke(FindEntryProperty(nodeView.Key));
            }
            else
            {
                OnEntrySelectionChanged?.Invoke(null);
            }
        }
    }
}
