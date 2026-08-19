/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequenceNodeInspectorPanel.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief 選択中ノードのフィールドを編集する、ウィンドウ右側のインスペクタ相当パネル
 * ノード種別ごとに保持するフィールドが異なるため、SerializedPropertyを列挙して存在するフィールドだけを表示する
 * (Label/EditConditionAttributeのPropertyDrawerがPropertyField経由でもそのまま適用される)
 * =====================================*/

using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace VFXUtility.Editor
{
    internal class VFXSequenceNodeInspectorPanel : VisualElement
    {
        private readonly VFXSequencerGraphView mGraphView;
        private readonly VisualElement mContentContainer;
        private readonly Action<string> mOnTriggerEvent;
        private SerializedProperty mNodeProperty;

        // aGraphView : ノード一覧参照・ノードピッカーに使うGraphView
        // aOnTriggerEvent : イベントノード選択時の「発火」ボタンから呼ぶコールバック(埋め込みプレビューのPlayEventを実行する)
        public VFXSequenceNodeInspectorPanel(VFXSequencerGraphView aGraphView, Action<string> aOnTriggerEvent)
        {
            mGraphView = aGraphView;
            mOnTriggerEvent = aOnTriggerEvent;

            var scrollView = new ScrollView();
            Add(scrollView);
            mContentContainer = scrollView.contentContainer;

            SetTargetProperty(null);
        }

        // 現在表示中のノードの内容を再描画する。分岐ノードの接続先ごとの重み/true-false一覧など、
        // 選択中ノード自身は変わらないままグラフ構造(接続)だけが変化した際に呼ぶ
        public void Refresh()
        {
            SetTargetProperty(mNodeProperty);
        }

        // 表示対象を切り替える
        // aNodeProperty : 表示するノードのSerializedProperty(mNodesの要素)。未選択時はnull
        public void SetTargetProperty(SerializedProperty aNodeProperty)
        {
            mNodeProperty = aNodeProperty;
            mContentContainer.Clear();

            if (mNodeProperty == null)
            {
                mContentContainer.Add(new Label("ノードを選択してください") { style = { paddingTop = 8, paddingLeft = 8 } });
                return;
            }

            SerializedProperty iterator = mNodeProperty.Copy();
            SerializedProperty end = mNodeProperty.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;

                if (iterator.name is "mNodeId" or "mNextNodeIds")
                {
                    continue; // 内部管理用フィールドはグラフの接続・削除操作経由でのみ変更させる
                }

                if (iterator.name == "mTargetNodeId")
                {
                    AddNodePickerField(iterator.Copy());
                    continue;
                }

                if (iterator.name == "mTargetBranchNodeId")
                {
                    AddBranchHeadPickerField(iterator.Copy());
                    continue;
                }

                if (iterator.name == "mTargetLoopNodeId")
                {
                    AddLoopNodePickerField(iterator.Copy());
                    continue;
                }

                if (iterator.name == "mWeights")
                {
                    AddBranchWeightList(iterator.Copy());
                    continue;
                }

                if (iterator.name == "mBranches")
                {
                    AddBranchConditionList(iterator.Copy());
                    continue;
                }

                if (iterator.name == "mDisplayName")
                {
                    AddDisplayNameField(iterator.Copy());
                    continue;
                }

                var field = new PropertyField(iterator.Copy());
                field.Bind(mNodeProperty.serializedObject);
                mContentContainer.Add(field);
            }

            if (mNodeProperty.managedReferenceValue is VFXSequenceEventNode eventNode)
            {
                AddTriggerEventButton(eventNode);
            }
        }

        // ノードの表示名を編集するフィールド。同じ種別のノードが複数あっても見分けられるよう、
        // 編集のたびにグラフ上のノードタイトルを即座に反映する(空にすると種別名の表示へ戻る)
        private void AddDisplayNameField(SerializedProperty aDisplayNameProperty)
        {
            string nodeId = mNodeProperty.FindPropertyRelative("mNodeId").stringValue;

            var field = new PropertyField(aDisplayNameProperty);
            field.Bind(mNodeProperty.serializedObject);
            field.RegisterValueChangeCallback(evt => mGraphView.RefreshNodeTitle(nodeId, evt.changedProperty.stringValue));
            mContentContainer.Add(field);
        }

        // イベントノードは自動開始しないため、埋め込みプレビューで動作確認できるよう「このイベントを発火」ボタンを出す
        // (StopVFX等の対象としてイベントノードを狙ったテストにも使う)
        private void AddTriggerEventButton(VFXSequenceEventNode aEventNode)
        {
            var button = new Button(() => mOnTriggerEvent?.Invoke(aEventNode.EventName))
            {
                text = "このイベントを発火(PlayEvent)"
            };
            button.SetEnabled(mOnTriggerEvent != null && !string.IsNullOrEmpty(aEventNode.EventName));
            mContentContainer.Add(button);
        }

        // StopVFXノードの対象ノード参照は、専用のノードピッカーボタンで選択する
        private void AddNodePickerField(SerializedProperty aTargetNodeIdProperty)
        {
            string currentLabel = VFXSequenceNodePickerUtility.GetNodeLabel(mGraphView, aTargetNodeIdProperty.stringValue);

            var button = new Button(() =>
            {
                VFXSequenceNodePickerUtility.ShowPicker(mGraphView, aNodeId =>
                {
                    aTargetNodeIdProperty.stringValue = aNodeId;
                    aTargetNodeIdProperty.serializedObject.ApplyModifiedProperties();
                    SetTargetProperty(mNodeProperty);
                });
            })
            {
                text = $"対象ノード : {currentLabel}"
            };

            mContentContainer.Add(button);
        }

        // StopNodeノードの対象ノードは、ルートノードの直接の接続先(ブランチの先頭)一覧から選択する
        private void AddBranchHeadPickerField(SerializedProperty aTargetBranchNodeIdProperty)
        {
            string currentLabel = VFXSequenceNodePickerUtility.GetBranchHeadLabel(mGraphView, aTargetBranchNodeIdProperty.stringValue);

            var button = new Button(() =>
            {
                VFXSequenceNodePickerUtility.ShowBranchHeadPicker(mGraphView, aNodeId =>
                {
                    aTargetBranchNodeIdProperty.stringValue = aNodeId;
                    aTargetBranchNodeIdProperty.serializedObject.ApplyModifiedProperties();
                    SetTargetProperty(mNodeProperty);
                });
            })
            {
                text = $"対象ノード : {currentLabel}"
            };

            mContentContainer.Add(button);
        }

        // ループ継続ノードの対象ループノードは、グラフ内のループノード一覧から選択する
        private void AddLoopNodePickerField(SerializedProperty aTargetLoopNodeIdProperty)
        {
            string currentLabel = VFXSequenceNodePickerUtility.GetGenericNodeLabel(mGraphView, aTargetLoopNodeIdProperty.stringValue);

            var button = new Button(() =>
            {
                VFXSequenceNodePickerUtility.ShowLoopNodePicker(mGraphView, aNodeId =>
                {
                    aTargetLoopNodeIdProperty.stringValue = aNodeId;
                    aTargetLoopNodeIdProperty.serializedObject.ApplyModifiedProperties();
                    SetTargetProperty(mNodeProperty);
                });
            })
            {
                text = $"対象ループノード : {currentLabel}"
            };

            mContentContainer.Add(button);
        }

        // ランダム分岐ノードの接続先ごとの重みを一覧表示する。接続先自体はグラフの接続操作でのみ増減するため、
        // ここでは接続先ラベル(読み取り専用)と重み(編集可能)のみを並べる
        private void AddBranchWeightList(SerializedProperty aWeightsProperty)
        {
            mContentContainer.Add(new Label("接続先ごとの重み") { style = { paddingTop = 4, paddingLeft = 2, unityFontStyleAndWeight = FontStyle.Bold } });

            if (aWeightsProperty.arraySize == 0)
            {
                mContentContainer.Add(new Label("(接続先がありません)") { style = { paddingLeft = 8 } });
                return;
            }

            for (int i = 0; i < aWeightsProperty.arraySize; i++)
            {
                SerializedProperty element = aWeightsProperty.GetArrayElementAtIndex(i);
                string targetLabel = VFXSequenceNodePickerUtility.GetGenericNodeLabel(mGraphView, element.FindPropertyRelative("mTargetNodeId").stringValue);

                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, paddingLeft = 8 } };
                row.Add(new Label(targetLabel) { style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleLeft, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis } });

                // PropertyFieldだとLabelAttributeの描画側で強制的に「重み」ラベルが付き、接続先ラベルと合わせて
                // 横幅が収まらなくなるため、ネイティブのFloatFieldへ直接バインドしてコンパクトに表示する
                var weightField = new FloatField { bindingPath = element.FindPropertyRelative("mWeight").propertyPath, style = { width = 60, flexShrink = 0 } };
                weightField.Bind(mNodeProperty.serializedObject);
                row.Add(weightField);

                mContentContainer.Add(row);
            }
        }

        // 条件分岐ノードの接続先ごとのtrue/falseを一覧表示する。接続先自体はグラフの接続操作でのみ増減する
        private void AddBranchConditionList(SerializedProperty aBranchesProperty)
        {
            mContentContainer.Add(new Label("接続先ごとのtrue/false") { style = { paddingTop = 4, paddingLeft = 2, unityFontStyleAndWeight = FontStyle.Bold } });

            if (aBranchesProperty.arraySize == 0)
            {
                mContentContainer.Add(new Label("(接続先がありません)") { style = { paddingLeft = 8 } });
                return;
            }

            for (int i = 0; i < aBranchesProperty.arraySize; i++)
            {
                SerializedProperty element = aBranchesProperty.GetArrayElementAtIndex(i);
                string targetLabel = VFXSequenceNodePickerUtility.GetGenericNodeLabel(mGraphView, element.FindPropertyRelative("mTargetNodeId").stringValue);

                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, paddingLeft = 8 } };
                row.Add(new Label(targetLabel) { style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleLeft, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis } });

                // FloatField同様、PropertyFieldだと強制ラベルで横幅が収まらないためToggleへ直接バインドする
                var fireOnTrueField = new Toggle("true") { bindingPath = element.FindPropertyRelative("mFireOnTrue").propertyPath, style = { flexShrink = 0 } };
                fireOnTrueField.Bind(mNodeProperty.serializedObject);
                row.Add(fireOnTrueField);

                mContentContainer.Add(row);
            }
        }
    }
}
