/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAITreeNodeView.cs
 * @author hqrse
 * @date 2026/08/25
 * @brief 判断ツリーのノード1つ分のグラフ表示
 * =====================================*/

using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace PPCore
{
    // 判断ツリーのノード 1 つをグラフ上へ表示するビュー
    // 上側に親から繋がる入力ポート、右側にノード種別ごとの出力ポートを生やす
    // 実データ（PPUnitAINode）への反映はグラフビュー側が行い、ここは見た目だけを持つ
    internal sealed class PPUnitAITreeNodeView : Node
    {
        // 表示対象のノード
        public PPUnitAINode Node { get; }
        // 親から繋がる入力ポート
        public Port InputPort { get; }
        // 接続口ごとの出力ポート。添字が PPUnitAINode の接続口番号と対応する
        public IReadOnlyList<Port> OutputPorts => mOutputPorts;

        private readonly List<Port> mOutputPorts = new();

        // aNode : 表示対象のノード
        public PPUnitAITreeNodeView(PPUnitAINode aNode)
        {
            Node = aNode;
            title = aNode.NodeName;
            viewDataKey = aNode.NodeId;

            // 空白領域へのドロップでノード追加メニューを出すため、既定の Port ではなく自前のポートを使う
            InputPort = PPUnitAITreePort.Create(Orientation.Horizontal, Direction.Input, Port.Capacity.Single);
            InputPort.portName = "";
            inputContainer.Add(InputPort);

            var ports = aNode.Ports;
            for (int i = 0; i < ports.Count; i++)
            {
                var capacity = ports[i].IsMultiple ? Port.Capacity.Multi : Port.Capacity.Single;
                var port = PPUnitAITreePort.Create(Orientation.Horizontal, Direction.Output, capacity);
                port.portName = ports[i].Name;
                outputContainer.Add(port);
                mOutputPorts.Add(port);
            }

            ApplyTitleColor(aNode);
            RefreshInterruptStyle();
            RefreshExpandedState();
            RefreshPorts();

            style.left = aNode.GraphPosition.x;
            style.top = aNode.GraphPosition.y;
        }

        // ノード種別に応じたタイトル色を当てる
        // 種別はノードを作り直さない限り変わらないため、生成時に 1 回だけ決める
        // aNode : 表示対象のノード
        private void ApplyTitleColor(PPUnitAINode aNode)
        {
            Color color = aNode switch
            {
                PPUnitAISelectorNode => new Color(0.20f, 0.32f, 0.42f),
                PPUnitAILotteryNode => new Color(0.33f, 0.20f, 0.42f),
                PPUnitAIConditionNode => new Color(0.34f, 0.30f, 0.16f),
                PPUnitAISearchNode => new Color(0.16f, 0.40f, 0.22f),
                PPUnitAIActionNode => new Color(0.45f, 0.16f, 0.16f),
                _ => new Color(0.24f, 0.24f, 0.24f),
            };
            titleContainer.style.backgroundColor = color;
        }

        // 割り込み指定の縁取りを現在の設定へ合わせる
        // 待機コミット中の挙動を左右する設定なので、ひと目で分かるように枠で示す
        // インスペクタで切り替えた直後にも呼ぶため、OFF に戻された場合は枠を消す
        private void RefreshInterruptStyle()
        {
            float width = Node.IsInterrupt ? 2f : 0f;
            style.borderTopWidth = width;
            style.borderBottomWidth = width;
            style.borderLeftWidth = width;
            style.borderRightWidth = width;

            if (!Node.IsInterrupt) return;

            var borderColor = new Color(0.95f, 0.65f, 0.20f);
            style.borderTopColor = borderColor;
            style.borderBottomColor = borderColor;
            style.borderLeftColor = borderColor;
            style.borderRightColor = borderColor;
        }

        // 表示名と縁取りをノードの現在値へ更新する
        // インスペクタでノード名や割り込み指定を編集した直後に呼ぶ
        // aIsRoot : ルートなら true
        public void RefreshView(bool aIsRoot)
        {
            title = aIsRoot ? $"★ {Node.NodeName}" : Node.NodeName;
            RefreshInterruptStyle();
        }
    }
}
