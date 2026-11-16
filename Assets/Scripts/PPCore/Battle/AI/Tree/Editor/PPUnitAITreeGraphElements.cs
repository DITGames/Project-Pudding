/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitAITreeGraphElements.cs
 * @author hqrse
 * @date 2026/09/02
 * @brief ツリーエディタのグラフ要素（エッジ・ポート・ハイライト）
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace PPCore
{
    // 強調用の色を保持するエッジ
    // 既定の Edge はポートの色から毎回線の色を計算し直すため、InputColor / OutputColor を差し替える
    internal sealed class PPUnitAITreeEdge : Edge
    {
        /* 定数 */

        // 強調時の線の太さ
        private const int HighlightedEdgeWidth = 4;

        /* 状態 */

        // 強調時に使う色。null なら既定の計算に任せる
        private Color? mHighlightColor;

        /* 操作 */

        // 強調色を設定する。null を渡すと解除して既定の色・太さへ戻る
        // aColor : 強調色。解除する場合は null
        public void SetHighlightColor(Color? aColor)
        {
            mHighlightColor = aColor;
            UpdateEdgeControl();
            MarkDirtyRepaint();
        }

        // 既定の更新のあとに強調色・太さで上書きする
        // 既定の実装がポートの色から毎回計算し直すため、そのまま書き込むだけでは次の更新で消えてしまう
        public override bool UpdateEdgeControl()
        {
            bool result = base.UpdateEdgeControl();
            if (mHighlightColor.HasValue && edgeControl != null)
            {
                edgeControl.inputColor = mHighlightColor.Value;
                edgeControl.outputColor = mHighlightColor.Value;
                edgeControl.edgeWidth = HighlightedEdgeWidth;
            }
            return result;
        }
    }

    // 自前のリスナーを持つポート。Port.Create() では EdgeConnector を差し替えられないため、
    // Port を直接継承して m_EdgeConnector へこのクラス自身を設定する
    internal class PPUnitAITreePort : Port, IEdgeConnectorListener
    {
        /* static 関数 */

        public static PPUnitAITreePort Create(Orientation aOrientation, Direction aDirection, Capacity aCapacity)
        {
            var port = new PPUnitAITreePort(aOrientation, aDirection, aCapacity, typeof(bool));
            port.m_EdgeConnector = new EdgeConnector<PPUnitAITreeEdge>(port);
            port.AddManipulator(port.m_EdgeConnector);
            return port;
        }

        /* 生成 */

        private PPUnitAITreePort(Orientation aOrientation, Direction aDirection, Capacity aCapacity, Type aType)
            : base(aOrientation, aDirection, aCapacity, aType)
        {
        }

        /* イベントハンドラ */

        // 既存ポート上でドロップされた際に接続を確定させる
        // 標準の OnDrop が呼ばれなくなるため、既存接続の削除・通知・要素追加・両端の接続を同等に再現する
        void IEdgeConnectorListener.OnDrop(GraphView aGraphView, Edge aEdge)
        {
            // 1 本しか繋げないポートへ繋ぎ直した場合は、先に既存の接続を削除する
            // 条件分岐やターゲット検索の枝は 1 本しか持てないため、この処理が実際に効く
            var edgesToDelete = new List<GraphElement>();
            if (aEdge.input.capacity == Capacity.Single)
            {
                foreach (Edge connected in aEdge.input.connections)
                {
                    if (connected != aEdge) edgesToDelete.Add(connected);
                }
            }
            if (aEdge.output.capacity == Capacity.Single)
            {
                foreach (Edge connected in aEdge.output.connections)
                {
                    if (connected != aEdge) edgesToDelete.Add(connected);
                }
            }
            if (edgesToDelete.Count > 0)
            {
                aGraphView.DeleteElements(edgesToDelete);
            }

            // graphViewChanged を通すことで、グラフビュー側のアセット反映処理が走る
            var change = new GraphViewChange { edgesToCreate = new List<Edge> { aEdge } };
            List<Edge> edgesToCreate = change.edgesToCreate;
            if (aGraphView.graphViewChanged != null)
            {
                edgesToCreate = aGraphView.graphViewChanged(change).edgesToCreate;
            }

            foreach (Edge created in edgesToCreate)
            {
                aGraphView.AddElement(created);
                aEdge.input.Connect(created);
                aEdge.output.Connect(created);
            }
        }

        // ポートの無い空白領域でドロップされた際に呼ばれる。ノード追加メニューを表示して自動接続する
        void IEdgeConnectorListener.OnDropOutsidePort(Edge aEdge, Vector2 aPosition)
        {
            var graphView = GetFirstAncestorOfType<PPUnitAITreeGraphView>();
            if (graphView == null) return;

            graphView.ShowNodeCreationMenuForDrop(this, aPosition);
        }
    }

    // 思考経路をグラフ上で強調表示する際の種類
    // 通過しただけのノードと、実際に行動が確定したノードを見分けられるようにする
    public enum PPUnitAITreeHighlight
    {
        // 強調しない
        None,
        // 経路として通過した
        Passed,
        // ここで行動が確定した
        Decided,
    }

    // デバッグウィンドウで選ばれた思考記録を、ツリーウィンドウへ伝える中継点
    public static class PPUnitAITreeHighlightHub
    {
        // 現在選ばれている思考記録。選択されていなければ null
        public static PPUnitAIThinkEntry Selected { get; private set; }

        // 選択が変わったときに発火する(選ばれた思考記録。解除時は null)
        public static event Action<PPUnitAIThinkEntry> OnSelectionChanged;

        // 選択を差し替えて通知する
        // aEntry : 選ばれた思考記録。解除する場合は null
        public static void Select(PPUnitAIThinkEntry aEntry)
        {
            Selected = aEntry;
            OnSelectionChanged?.Invoke(aEntry);
        }
    }
}
