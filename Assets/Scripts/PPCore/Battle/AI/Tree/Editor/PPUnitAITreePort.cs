/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAITreePort.cs
 * @author hqrse
 * @date 2026/08/25
 * @brief 判断ツリー用のPort。空白領域へドロップされた際にノード追加メニューを表示できるようにする
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace PPCore
{
    // 静的な Port.Create() が返す Port は既定の EdgeConnector しか持たず、差し替える公開 API も無いため、
    // VFXSequenceFlowPort と同じ方式で Port を直接継承し、
    // private コンストラクタ経由で m_EdgeConnector へ自前のリスナー(このクラス自身)を設定する
    internal class PPUnitAITreePort : Port, IEdgeConnectorListener
    {
        // aOrientation : ポートの向き
        // aDirection : 入力か出力か
        // aCapacity : 繋げる本数
        // return : 生成されたポート
        public static PPUnitAITreePort Create(Orientation aOrientation, Direction aDirection, Capacity aCapacity)
        {
            var port = new PPUnitAITreePort(aOrientation, aDirection, aCapacity, typeof(bool));
            port.m_EdgeConnector = new EdgeConnector<PPUnitAITreeEdge>(port);
            port.AddManipulator(port.m_EdgeConnector);
            return port;
        }

        private PPUnitAITreePort(Orientation aOrientation, Direction aDirection, Capacity aCapacity, Type aType)
            : base(aOrientation, aDirection, aCapacity, aType)
        {
        }

        // 既存ポート上でドロップされた際に呼ばれ、接続を確定させる
        // IEdgeConnectorListener を差し替えると Unity 標準の DefaultEdgeConnectorListener.OnDrop が呼ばれなくなるため、
        // 標準が行っていた「既存接続の削除 → graphViewChanged 通知 → 要素追加 → 両端ポートの接続」を同等に再現する
        // (ここを空実装にすると通常のピン同士のドラッグ接続が一切成立しなくなる)
        // aGraphView : 対象の GraphView
        // aEdge : 確定させるエッジ
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
        // aEdge : ドラッグ中だった未確定のエッジ
        // aPosition : ドロップ位置(ワールド座標)
        void IEdgeConnectorListener.OnDropOutsidePort(Edge aEdge, Vector2 aPosition)
        {
            var graphView = GetFirstAncestorOfType<PPUnitAITreeGraphView>();
            if (graphView == null) return;

            graphView.ShowNodeCreationMenuForDrop(this, aPosition);
        }
    }
}
