/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequencerGraphView.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief VFXSequenceDefinitionのノードグラフを表示・編集するGraphView
 * ノードの追加・削除・接続・移動はSerializedProperty経由でアセットへ反映し、Undo/Redo・Dirty管理を得る
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace VFXUtility.Editor
{
    internal class VFXSequencerGraphView : GraphView
    {
        private readonly SerializedObject mSerializedObject;
        private readonly VFXSequenceDefinition mDefinition;
        private readonly Dictionary<string, VFXSequenceNodeView> mNodeViews = new();

        // 選択中ノードが変わった際に、対応するSerializedPropertyを渡して通知する(未選択時はnull)
        public event Action<SerializedProperty> OnNodeSelectionChanged;

        // ノードの追加・削除・接続変更でグラフ構造が変わった際に通知する(ゴール到達可能性の警告更新などに使う)
        public event Action OnGraphStructureChanged;

        // aSerializedObject : mTargetのSerializedObject / aDefinition : 表示対象のノードグラフ
        public VFXSequencerGraphView(SerializedObject aSerializedObject, VFXSequenceDefinition aDefinition)
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
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            Vector2 graphPosition = contentViewContainer.WorldToLocal(evt.mousePosition);

            foreach ((Type type, string displayName) in VFXSequenceNodeTypeMenuUtility.NodeTypes)
            {
                evt.menu.AppendAction($"ノードを追加/{displayName}", _ => AddNode(type, graphPosition));
            }

            base.BuildContextualMenu(evt);
        }

        public override List<Port> GetCompatiblePorts(Port aStartPort, NodeAdapter aNodeAdapter)
        {
            return ports.ToList()
                .Where(p => p != aStartPort && p.node != aStartPort.node && p.direction != aStartPort.direction)
                .ToList();
        }

        // 指定ノードIDに対応するSerializedPropertyを取得する(見つからない場合はnull)
        // aNodeId : 検索するノードのID
        public SerializedProperty GetNodeProperty(string aNodeId)
        {
            SerializedProperty nodesProp = mSerializedObject.FindProperty("mNodes");
            for (int i = 0; i < nodesProp.arraySize; i++)
            {
                SerializedProperty element = nodesProp.GetArrayElementAtIndex(i);
                if (element.managedReferenceValue is VFXSequenceNodeBase node && node.NodeId == aNodeId)
                {
                    return element;
                }
            }
            return null;
        }

        // 現在グラフ上に存在する全ノードの一覧(ノードピッカー等が利用する)
        public IReadOnlyList<VFXSequenceNodeBase> GetAllNodes() => mDefinition.Nodes;

        // Inspectorで表示名を編集した際、グラフ上のノードタイトルを即座に反映する
        // aNodeId : 対象ノードのID / aDisplayName : 編集後の表示名(空ならノード種別名にフォールバックする)
        public void RefreshNodeTitle(string aNodeId, string aDisplayName)
        {
            if (!mNodeViews.TryGetValue(aNodeId, out VFXSequenceNodeView nodeView))
            {
                return;
            }
            nodeView.title = string.IsNullOrEmpty(aDisplayName) ? nodeView.DefaultTitle : aDisplayName;
        }

        private void RebuildFromDefinition()
        {
            foreach (VFXSequenceNodeView nodeView in mNodeViews.Values)
            {
                RemoveElement(nodeView);
            }
            mNodeViews.Clear();

            foreach (VFXSequenceNodeBase node in mDefinition.Nodes)
            {
                CreateNodeView(node);
            }

            foreach (VFXSequenceNodeBase node in mDefinition.Nodes)
            {
                if (!mNodeViews.TryGetValue(node.NodeId, out VFXSequenceNodeView sourceView))
                {
                    continue;
                }

                foreach (string nextId in node.NextNodeIds)
                {
                    if (!mNodeViews.TryGetValue(nextId, out VFXSequenceNodeView targetView) || targetView.InputPort == null)
                    {
                        continue; // 削除済みノード、または入力ポートを持たないルートノードへの参照は表示しない
                    }

                    Edge edge = sourceView.OutputPort.ConnectTo(targetView.InputPort);
                    AddElement(edge);
                }
            }
        }

        private void CreateNodeView(VFXSequenceNodeBase aNode)
        {
            var nodeView = new VFXSequenceNodeView(aNode);
            mNodeViews[aNode.NodeId] = nodeView;
            AddElement(nodeView);
        }

        // 戻り値 : 追加したノードのデータ(空白ドロップ時の自動接続で使う)
        private VFXSequenceNodeBase AddNode(Type aNodeType, Vector2 aPosition)
        {
            SerializedProperty nodesProp = mSerializedObject.FindProperty("mNodes");
            int newIndex = nodesProp.arraySize;
            nodesProp.InsertArrayElementAtIndex(newIndex);
            SerializedProperty elementProp = nodesProp.GetArrayElementAtIndex(newIndex);

            var newNode = (VFXSequenceNodeBase)Activator.CreateInstance(aNodeType);
            elementProp.managedReferenceValue = newNode;

            SerializedProperty positionProp = elementProp.FindPropertyRelative("mPosition");
            positionProp.vector2Value = aPosition;
            newNode.Position = aPosition;

            mSerializedObject.ApplyModifiedProperties();

            CreateNodeView(newNode);
            return newNode;
        }

        // ポートから空白領域へドロップされた際に呼ばれる。ノード追加メニューを表示し、選択されたノードを生成して自動接続する
        // aSourcePort : ドラッグ元のポート / aPosition : ドロップ位置(ワールド座標)
        public void ShowNodeCreationMenuForDrop(Port aSourcePort, Vector2 aPosition)
        {
            bool isFromOutput = aSourcePort.direction == Direction.Output;
            Vector2 graphPosition = contentViewContainer.WorldToLocal(aPosition);

            var menu = new GenericMenu();
            foreach ((Type type, string displayName) in VFXSequenceNodeTypeMenuUtility.NodeTypes)
            {
                menu.AddItem(new GUIContent(displayName), false,
                    () => AddNodeAndConnect(type, graphPosition, aSourcePort, isFromOutput));
            }
            menu.ShowAsContext();
        }

        // 新規ノードを生成し、ドラッグ元ポートと接続する
        // aIsFromOutput : trueならドラッグ元が出力ポート(新規ノードが後続になる)、falseなら入力ポート(新規ノードが前段になる)
        private void AddNodeAndConnect(Type aNodeType, Vector2 aPosition, Port aSourcePort, bool aIsFromOutput)
        {
            VFXSequenceNodeBase newNode = AddNode(aNodeType, aPosition);
            if (!mNodeViews.TryGetValue(newNode.NodeId, out VFXSequenceNodeView newNodeView))
            {
                return;
            }

            Port outputPort = aIsFromOutput ? aSourcePort : newNodeView.OutputPort;
            Port inputPort = aIsFromOutput ? newNodeView.InputPort : aSourcePort;

            Edge edge = outputPort.ConnectTo(inputPort);
            AddElement(edge);
            ConnectEdge(edge);
            mSerializedObject.ApplyModifiedProperties();
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
                        case VFXSequenceNodeView nodeView:
                            RemoveNode(nodeView);
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
                    if (element is VFXSequenceNodeView nodeView)
                    {
                        UpdateNodePosition(nodeView);
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

        private void ConnectEdge(Edge aEdge)
        {
            if (aEdge.output?.node is not VFXSequenceNodeView sourceView || aEdge.input?.node is not VFXSequenceNodeView targetView)
            {
                return;
            }

            SerializedProperty sourceProp = GetNodeProperty(sourceView.NodeId);
            if (sourceProp == null)
            {
                return;
            }

            SerializedProperty nextIdsProp = sourceProp.FindPropertyRelative("mNextNodeIds");
            for (int i = 0; i < nextIdsProp.arraySize; i++)
            {
                if (nextIdsProp.GetArrayElementAtIndex(i).stringValue == targetView.NodeId)
                {
                    return; // 既に接続済み
                }
            }

            int newIndex = nextIdsProp.arraySize;
            nextIdsProp.InsertArrayElementAtIndex(newIndex);
            nextIdsProp.GetArrayElementAtIndex(newIndex).stringValue = targetView.NodeId;

            // 分岐ノードは接続先ごとのメタ情報(重み/true-false)も同期する
            SyncBranchMetadataOnConnect(sourceProp, targetView.NodeId);
        }

        private void DisconnectEdge(Edge aEdge)
        {
            if (aEdge.output?.node is not VFXSequenceNodeView sourceView || aEdge.input?.node is not VFXSequenceNodeView targetView)
            {
                return;
            }

            SerializedProperty sourceProp = GetNodeProperty(sourceView.NodeId);
            if (sourceProp == null)
            {
                return;
            }

            SerializedProperty nextIdsProp = sourceProp.FindPropertyRelative("mNextNodeIds");
            for (int i = 0; i < nextIdsProp.arraySize; i++)
            {
                if (nextIdsProp.GetArrayElementAtIndex(i).stringValue == targetView.NodeId)
                {
                    nextIdsProp.DeleteArrayElementAtIndex(i);
                    break;
                }
            }

            // 分岐ノードの接続先メタ情報も、接続の削除に合わせて取り除く
            SyncBranchMetadataOnDisconnect(sourceProp, targetView.NodeId);
        }

        // 分岐ノードへの接続が増えた際、接続先ごとのメタ情報(重み/true-false)のエントリを追加する
        // aSourceProp : 接続元ノードのSerializedProperty / aTargetNodeId : 新たに接続された先のノードID
        private void SyncBranchMetadataOnConnect(SerializedProperty aSourceProp, string aTargetNodeId)
        {
            if (aSourceProp.managedReferenceValue is VFXSequenceRandomBranchNode)
            {
                SerializedProperty weightsProp = aSourceProp.FindPropertyRelative("mWeights");
                int newIndex = weightsProp.arraySize;
                weightsProp.InsertArrayElementAtIndex(newIndex);
                SerializedProperty newElement = weightsProp.GetArrayElementAtIndex(newIndex);
                newElement.FindPropertyRelative("mTargetNodeId").stringValue = aTargetNodeId;
                newElement.FindPropertyRelative("mWeight").floatValue = 1f;
            }
            else if (aSourceProp.managedReferenceValue is VFXSequenceConditionalBranchNode)
            {
                SerializedProperty branchesProp = aSourceProp.FindPropertyRelative("mBranches");
                int newIndex = branchesProp.arraySize;
                branchesProp.InsertArrayElementAtIndex(newIndex);
                SerializedProperty newElement = branchesProp.GetArrayElementAtIndex(newIndex);
                newElement.FindPropertyRelative("mTargetNodeId").stringValue = aTargetNodeId;
                newElement.FindPropertyRelative("mFireOnTrue").boolValue = true; // 既定はtrue側
            }
        }

        // 分岐ノードの接続が切れた際、対応する接続先ごとのメタ情報のエントリを取り除く
        // aSourceProp : 接続元ノードのSerializedProperty / aTargetNodeId : 切断された先のノードID
        private void SyncBranchMetadataOnDisconnect(SerializedProperty aSourceProp, string aTargetNodeId)
        {
            string listPropName = aSourceProp.managedReferenceValue switch
            {
                VFXSequenceRandomBranchNode => "mWeights",
                VFXSequenceConditionalBranchNode => "mBranches",
                _ => null,
            };
            if (listPropName == null)
            {
                return;
            }

            SerializedProperty listProp = aSourceProp.FindPropertyRelative(listPropName);
            for (int i = 0; i < listProp.arraySize; i++)
            {
                SerializedProperty element = listProp.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative("mTargetNodeId").stringValue == aTargetNodeId)
                {
                    listProp.DeleteArrayElementAtIndex(i);
                    break;
                }
            }
        }

        private void RemoveNode(VFXSequenceNodeView aNodeView)
        {
            mNodeViews.Remove(aNodeView.NodeId);

            SerializedProperty nodesProp = mSerializedObject.FindProperty("mNodes");
            for (int i = 0; i < nodesProp.arraySize; i++)
            {
                SerializedProperty element = nodesProp.GetArrayElementAtIndex(i);
                if (element.managedReferenceValue is VFXSequenceNodeBase node && node.NodeId == aNodeView.NodeId)
                {
                    nodesProp.DeleteArrayElementAtIndex(i);
                    break;
                }
            }

            // 他ノードからこのノードへの接続参照を除去する(StopVFX/StopNode等の参照はダングリング時に無視される仕様のためそのままにする)
            for (int i = 0; i < nodesProp.arraySize; i++)
            {
                SerializedProperty element = nodesProp.GetArrayElementAtIndex(i);
                SerializedProperty nextIdsProp = element.FindPropertyRelative("mNextNodeIds");
                for (int j = nextIdsProp.arraySize - 1; j >= 0; j--)
                {
                    if (nextIdsProp.GetArrayElementAtIndex(j).stringValue == aNodeView.NodeId)
                    {
                        nextIdsProp.DeleteArrayElementAtIndex(j);
                    }
                }

                // 分岐ノードの接続先メタ情報にも同じ参照が残っていれば除去する(通常はDisconnectEdge経由で既に消えているはずの二重防御)
                SyncBranchMetadataOnDisconnect(element, aNodeView.NodeId);
            }
        }

        private void UpdateNodePosition(VFXSequenceNodeView aNodeView)
        {
            SerializedProperty nodeProp = GetNodeProperty(aNodeView.NodeId);
            if (nodeProp == null)
            {
                return;
            }

            SerializedProperty positionProp = nodeProp.FindPropertyRelative("mPosition");
            positionProp.vector2Value = aNodeView.GetPosition().position;
        }

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
            if (selection.Count == 1 && selection[0] is VFXSequenceNodeView nodeView)
            {
                OnNodeSelectionChanged?.Invoke(GetNodeProperty(nodeView.NodeId));
            }
            else
            {
                OnNodeSelectionChanged?.Invoke(null);
            }
        }
    }
}
