/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAITreeGraphView.cs
 * @author hqrse
 * @date 2026/08/25
 * @brief 判断ツリーをノードグラフとして表示・編集するGraphView
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace PPCore
{
    // 判断ツリー（PPUnitAIProfileDefinition）をノードグラフとして編集する GraphView
    //
    // ノードの追加・削除・接続・移動は全て SerializedObject を通してアセットへ反映する
    // Undo とダーティ管理を Unity 側に任せられるため、独自の履歴を持たずに済む
    //
    // 優先度リストの子の並び順は、グラフ上の縦位置（上にあるものほど優先）から決める
    // 接続線の順序は見た目に出ないため、位置で表現したほうが読み違えない
    internal sealed class PPUnitAITreeGraphView : GraphView
    {
        private readonly SerializedObject mSerializedObject;
        private readonly PPUnitAIProfileDefinition mProfile;
        private readonly Dictionary<string, PPUnitAITreeNodeView> mNodeViews = new();

        // 選択中ノードが変わった際に、対応するノードを渡して通知する(未選択時は null)
        public event Action<PPUnitAINode> OnNodeSelectionChanged;

        // グラフ構造が変わった際に通知する。ルート未設定などの警告表示を更新するのに使う
        public event Action OnGraphStructureChanged;

        // aSerializedObject : 編集対象のプロファイルの SerializedObject
        // aProfile : 表示対象の判断ツリー
        public PPUnitAITreeGraphView(SerializedObject aSerializedObject, PPUnitAIProfileDefinition aProfile)
        {
            mSerializedObject = aSerializedObject;
            mProfile = aProfile;

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            graphViewChanged = OnGraphViewChanged;

            Rebuild();
        }

        // 右クリックメニューにノード追加の項目を並べる
        // 追加できる型は PPTypeMenuName を持つ PPUnitAINode 派生から集める
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            Vector2 graphPosition = contentViewContainer.WorldToLocal(evt.mousePosition);

            foreach (var (type, displayName) in EnumerateNodeTypes())
            {
                evt.menu.AppendAction($"ノードを追加/{displayName}", _ => AddNode(type, graphPosition));
            }

            if (selection.OfType<PPUnitAITreeNodeView>().FirstOrDefault() is { } selected)
            {
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("このノードをルートにする", _ => SetRoot(selected.Node));
            }

            base.BuildContextualMenu(evt);
        }

        // 接続可能なポートを返す。入力と出力、かつ別ノード同士のみ繋げる
        public override List<Port> GetCompatiblePorts(Port aStartPort, NodeAdapter aNodeAdapter)
            => ports.ToList()
                .Where(p => p != aStartPort && p.node != aStartPort.node && p.direction != aStartPort.direction)
                .ToList();

        // 指定ノードの表示を現在の値へ更新する
        // インスペクタでノード名や割り込み指定を編集した直後に呼び、グラフ側の表示を追従させる
        // aNodeId : 対象ノードの ID
        public void RefreshNodeView(string aNodeId)
        {
            if (!mNodeViews.TryGetValue(aNodeId ?? "", out var view)) return;

            view.RefreshView(view.Node.NodeId == mProfile.RootNodeId);
        }

        // ルートノードが画面中央へ来るように表示位置を合わせる
        // アセットを開いた直後はレイアウトが未確定のため、1 フレーム後に実行する
        public void FrameRootNode()
        {
            schedule.Execute(() =>
            {
                if (!mNodeViews.TryGetValue(mProfile.RootNodeId ?? "", out var rootView)) return;

                var rect = rootView.GetPosition();
                // 生成直後はノードの大きさが未確定なため、その場合は左上を基準にして寄せる
                Vector2 center = rect.size == Vector2.zero ? rect.position : rect.center;
                Vector3 offset = new Vector3(layout.width * 0.5f - center.x, layout.height * 0.5f - center.y, 0f);
                UpdateViewTransform(offset, Vector3.one);
            }).ExecuteLater(50);
        }

        // アセットの現在の内容からグラフを組み立て直す
        public void Rebuild()
        {
            foreach (var view in mNodeViews.Values)
            {
                RemoveElement(view);
            }
            mNodeViews.Clear();
            foreach (var edge in edges.ToList())
            {
                RemoveElement(edge);
            }

            foreach (var node in mProfile.Nodes)
            {
                if (node == null) continue;

                CreateNodeView(node);
            }
            ConnectAllEdges();
            OnGraphStructureChanged?.Invoke();
        }

        // ノード 1 つ分のビューを作ってグラフへ載せる
        // aNode : 表示するノード
        private void CreateNodeView(PPUnitAINode aNode)
        {
            var view = new PPUnitAITreeNodeView(aNode);
            view.RefreshView(aNode.NodeId == mProfile.RootNodeId);
            mNodeViews[aNode.NodeId] = view;
            AddElement(view);
        }

        // 全ノードの接続口を辿って接続線を張る
        private void ConnectAllEdges()
        {
            foreach (var view in mNodeViews.Values)
            {
                var ports = view.Node.Ports;
                for (int i = 0; i < ports.Count && i < view.OutputPorts.Count; i++)
                {
                    foreach (var childId in ports[i].ChildIds)
                    {
                        if (!mNodeViews.TryGetValue(childId ?? "", out var childView)) continue;

                        var edge = view.OutputPorts[i].ConnectTo(childView.InputPort);
                        AddElement(edge);
                    }
                }
            }
        }

        // グラフ上の操作をアセットへ反映する
        // 接続・切断・削除・移動をまとめて受け取るため、Undo の記録もここで 1 回だけ行う
        // aChange : GraphView が通知する変更内容
        // return : 受け取った変更内容をそのまま返す
        private GraphViewChange OnGraphViewChanged(GraphViewChange aChange)
        {
            bool isStructureChanged = false;
            Undo.RecordObject(mProfile, "判断ツリーの編集");

            if (aChange.elementsToRemove != null)
            {
                foreach (var element in aChange.elementsToRemove)
                {
                    if (element is Edge edge) DisconnectEdge(edge);
                    else if (element is PPUnitAITreeNodeView nodeView) RemoveNode(nodeView.Node);
                    isStructureChanged = true;
                }
            }

            if (aChange.edgesToCreate != null)
            {
                foreach (var edge in aChange.edgesToCreate)
                {
                    ConnectEdge(edge);
                    isStructureChanged = true;
                }
            }

            if (aChange.movedElements != null)
            {
                foreach (var element in aChange.movedElements.OfType<PPUnitAITreeNodeView>())
                {
                    element.Node.SetGraphPosition(element.GetPosition().position);
                }
                // 縦位置が優先度になるため、移動しただけでも並び順を取り直す
                ReorderAllSelectors();
            }

            if (isStructureChanged)
            {
                ReorderAllSelectors();
            }

            ApplyChanges();
            if (isStructureChanged) OnGraphStructureChanged?.Invoke();
            return aChange;
        }

        // 接続線 1 本を実データへ反映する
        // aEdge : 張られた接続線
        private void ConnectEdge(Edge aEdge)
        {
            if (aEdge.output?.node is not PPUnitAITreeNodeView parent) return;
            if (aEdge.input?.node is not PPUnitAITreeNodeView child) return;

            int portIndex = parent.OutputPorts.ToList().IndexOf(aEdge.output);
            if (portIndex < 0) return;

            parent.Node.ConnectChild(portIndex, child.Node.NodeId);
        }

        // 接続線 1 本の切断を実データへ反映する
        // aEdge : 外された接続線
        private void DisconnectEdge(Edge aEdge)
        {
            if (aEdge.output?.node is not PPUnitAITreeNodeView parent) return;
            if (aEdge.input?.node is not PPUnitAITreeNodeView child) return;

            int portIndex = parent.OutputPorts.ToList().IndexOf(aEdge.output);
            if (portIndex < 0) return;

            parent.Node.DisconnectChild(portIndex, child.Node.NodeId);
        }

        // ポートから空白領域へドロップされた際に、ノード追加メニューを出す
        // 選ばれた型のノードをその場に作り、ドラッグ元のポートと繋ぐ
        // aSourcePort : ドラッグ元のポート
        // aPosition : ドロップ位置(ワールド座標)
        public void ShowNodeCreationMenuForDrop(Port aSourcePort, Vector2 aPosition)
        {
            bool isFromOutput = aSourcePort.direction == Direction.Output;
            Vector2 graphPosition = contentViewContainer.WorldToLocal(aPosition);

            var menu = new GenericMenu();
            foreach (var (type, displayName) in EnumerateNodeTypes())
            {
                menu.AddItem(new GUIContent(displayName), false,
                    () => AddNodeAndConnect(type, graphPosition, aSourcePort, isFromOutput));
            }
            menu.ShowAsContext();
        }

        // 新規ノードを生成し、ドラッグ元ポートと接続する
        // aNodeType : 追加するノードの型
        // aPosition : グラフ上の配置
        // aSourcePort : ドラッグ元のポート
        // aIsFromOutput : true ならドラッグ元が出力ポート(新規ノードが子になる)、false なら入力ポート(新規ノードが親になる)
        private void AddNodeAndConnect(Type aNodeType, Vector2 aPosition, Port aSourcePort, bool aIsFromOutput)
        {
            var newNode = AddNode(aNodeType, aPosition);
            if (newNode == null || !mNodeViews.TryGetValue(newNode.NodeId, out var newNodeView)) return;

            // 新規ノードが親になる場合、繋ぐ先は最初の接続口にする
            Port newNodePort = aIsFromOutput
                ? newNodeView.InputPort
                : newNodeView.OutputPorts.FirstOrDefault();
            if (newNodePort == null) return;

            Port outputPort = aIsFromOutput ? aSourcePort : newNodePort;
            Port inputPort = aIsFromOutput ? newNodePort : aSourcePort;

            var edge = outputPort.ConnectTo(inputPort);
            AddElement(edge);
            ConnectEdge(edge);
            ReorderAllSelectors();
            ApplyChanges();
            OnGraphStructureChanged?.Invoke();
        }

        // ノードを 1 つ追加する
        // aType : 追加するノードの型
        // aPosition : グラフ上の配置
        // return : 追加されたノード
        private PPUnitAINode AddNode(Type aType, Vector2 aPosition)
        {
            Undo.RecordObject(mProfile, "ノードの追加");

            var node = (PPUnitAINode)Activator.CreateInstance(aType);
            node.EnsureNodeId();
            node.SetGraphPosition(aPosition);

            var nodesProperty = mSerializedObject.FindProperty("mNodes");
            nodesProperty.arraySize++;
            nodesProperty.GetArrayElementAtIndex(nodesProperty.arraySize - 1).managedReferenceValue = node;
            mSerializedObject.ApplyModifiedProperties();
            mProfile.InvalidateNodeMap();

            // 最初の 1 つは自動的にルートにしておく。ルート未設定のまま気付かないのを防ぐ
            if (string.IsNullOrEmpty(mProfile.RootNodeId))
            {
                SetRootProperty(node.NodeId);
            }

            CreateNodeView(node);
            OnGraphStructureChanged?.Invoke();
            return node;
        }

        // ノードを 1 つ削除し、他のノードから張られていた接続も外す
        // aNode : 削除するノード
        private void RemoveNode(PPUnitAINode aNode)
        {
            foreach (var other in mProfile.Nodes)
            {
                if (other == null || ReferenceEquals(other, aNode)) continue;

                var ports = other.Ports;
                for (int i = 0; i < ports.Count; i++)
                {
                    other.DisconnectChild(i, aNode.NodeId);
                }
            }

            var nodesProperty = mSerializedObject.FindProperty("mNodes");
            for (int i = 0; i < nodesProperty.arraySize; i++)
            {
                if (nodesProperty.GetArrayElementAtIndex(i).managedReferenceValue is not PPUnitAINode node) continue;
                if (!ReferenceEquals(node, aNode)) continue;

                nodesProperty.DeleteArrayElementAtIndex(i);
                break;
            }
            mSerializedObject.ApplyModifiedProperties();
            mProfile.InvalidateNodeMap();

            mNodeViews.Remove(aNode.NodeId);
            if (mProfile.RootNodeId == aNode.NodeId)
            {
                SetRootProperty("");
            }
        }

        // 指定ノードをルートにする
        // aNode : ルートにするノード
        private void SetRoot(PPUnitAINode aNode)
        {
            Undo.RecordObject(mProfile, "ルートノードの変更");
            SetRootProperty(aNode.NodeId);

            foreach (var view in mNodeViews.Values)
            {
                view.RefreshView(view.Node.NodeId == mProfile.RootNodeId);
            }
            OnGraphStructureChanged?.Invoke();
        }

        // ルートノード ID を書き込む
        // aNodeId : 設定するノード ID
        private void SetRootProperty(string aNodeId)
        {
            mSerializedObject.FindProperty("mRootNodeId").stringValue = aNodeId;
            mSerializedObject.ApplyModifiedProperties();
        }

        // 全ての優先度リストについて、子の並び順をグラフ上の縦位置へ揃える
        // 接続線の順序は見た目に出ないため、上にあるものほど優先という規則で読めるようにする
        private void ReorderAllSelectors()
        {
            foreach (var view in mNodeViews.Values)
            {
                var ports = view.Node.Ports;
                for (int i = 0; i < ports.Count; i++)
                {
                    if (!ports[i].IsMultiple) continue;

                    var ordered = ports[i].ChildIds
                        .Where(id => mNodeViews.ContainsKey(id ?? ""))
                        .OrderBy(id => mNodeViews[id].GetPosition().position.y)
                        .ToList();
                    view.Node.ReorderChildren(i, ordered);
                }
            }
        }

        // 変更をアセットへ書き戻し、保存対象として記録する
        private void ApplyChanges()
        {
            mSerializedObject.ApplyModifiedProperties();
            mProfile.InvalidateNodeMap();
            EditorUtility.SetDirty(mProfile);
        }

        // 選択が変わった際に通知する。インスペクタ側の表示切り替えに使う
        public override void AddToSelection(ISelectable aSelectable)
        {
            base.AddToSelection(aSelectable);
            NotifySelection();
        }

        public override void RemoveFromSelection(ISelectable aSelectable)
        {
            base.RemoveFromSelection(aSelectable);
            NotifySelection();
        }

        public override void ClearSelection()
        {
            base.ClearSelection();
            NotifySelection();
        }

        // 現在の選択内容を通知する
        private void NotifySelection()
            => OnNodeSelectionChanged?.Invoke(selection.OfType<PPUnitAITreeNodeView>().FirstOrDefault()?.Node);

        // 追加できるノード型を、表示名付きで列挙する
        // PPTypeMenuName が付いていない型はメニューに出さない（付け忘れの検出も兼ねる）
        // return : ノード型と表示名の組
        private static IEnumerable<(Type Type, string DisplayName)> EnumerateNodeTypes()
        {
            foreach (var type in TypeCache.GetTypesDerivedFrom<PPUnitAINode>())
            {
                if (type.IsAbstract) continue;

                var attribute = type.GetCustomAttribute<PPTypeMenuNameAttribute>();
                if (attribute == null) continue;

                yield return (type, attribute.Path);
            }
        }
    }
}
