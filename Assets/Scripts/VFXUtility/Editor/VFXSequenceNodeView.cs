/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequenceNodeView.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief ノードグラフ上の1ノードを表すGraphView用ノードビュー
 * =====================================*/

using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace VFXUtility.Editor
{
    internal class VFXSequenceNodeView : Node
    {
        public string NodeId { get; }
        public Port InputPort { get; }
        public Port OutputPort { get; }

        // 表示名が未設定の場合に使うノード種別名(表示名編集時のフォールバック計算に使う)
        public string DefaultTitle { get; }

        // aNode : このビューが表示するノードデータ
        public VFXSequenceNodeView(VFXSequenceNodeBase aNode)
        {
            NodeId = aNode.NodeId;
            DefaultTitle = VFXSequenceNodeTypeMenuUtility.GetDisplayName(aNode);
            title = string.IsNullOrEmpty(aNode.DisplayName) ? DefaultTitle : aNode.DisplayName;

            // ルートノードは唯一の開始点のため、他ノードからの接続を受け付けない
            // (入力ポート自体を持たせないことで、GraphView上でドラッグ接続が物理的にできなくなる)
            if (aNode is not VFXSequenceRootNode)
            {
                InputPort = VFXSequenceFlowPort.Create(Orientation.Horizontal, Direction.Input);
                InputPort.portName = string.Empty;
                inputContainer.Add(InputPort);
            }

            OutputPort = VFXSequenceFlowPort.Create(Orientation.Horizontal, Direction.Output);
            OutputPort.portName = string.Empty;
            outputContainer.Add(OutputPort);

            SetPosition(new Rect(aNode.Position, new Vector2(180, 100)));

            RefreshExpandedState();
            RefreshPorts();
        }
    }
}
