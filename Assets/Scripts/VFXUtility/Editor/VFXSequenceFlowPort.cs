/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequenceFlowPort.cs
 * @author hqrse
 * @date 2026/08/18
 * @brief ノード間フロー接続用のPort。空白領域へドロップされた際にノード追加メニューを表示できるようにする
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace VFXUtility.Editor
{
    // 静的な Port.Create() が返すPortは既定のEdgeConnectorしか持たず、差し替える公開APIも無いため、
    // Shader Graph(UnityEditor.ShaderGraph.Drawing.ShaderPort)およびVFX Graph(UnityEditor.VFX.UI.VFXFlowAnchor)と
    // 同じ方式でPortを直接継承し、privateコンストラクタ経由でm_EdgeConnectorへ自前のリスナー(このクラス自身)を設定する
    internal class VFXSequenceFlowPort : Port, IEdgeConnectorListener
    {
        // aOrientation : ポートの向き / aDirection : 入力か出力か
        public static VFXSequenceFlowPort Create(Orientation aOrientation, Direction aDirection)
        {
            var port = new VFXSequenceFlowPort(aOrientation, aDirection, Capacity.Multi, typeof(bool));
            port.m_EdgeConnector = new EdgeConnector<Edge>(port);
            port.AddManipulator(port.m_EdgeConnector);
            return port;
        }

        private VFXSequenceFlowPort(Orientation aOrientation, Direction aDirection, Capacity aCapacity, Type aType)
            : base(aOrientation, aDirection, aCapacity, aType)
        {
        }

        // 既存ポート上でドロップされた際に呼ばれ、接続を確定させる
        // IEdgeConnectorListenerを差し替えるとUnity標準のDefaultEdgeConnectorListener.OnDropが呼ばれなくなるため、
        // 標準が行っていた「既存接続の削除 → graphViewChanged通知 → 要素追加 → 両端ポートの接続」を同等に再現する必要がある
        // (ここを空実装にすると通常のピン同士のドラッグ接続が一切成立しなくなる)
        // aGraphView : 対象のGraphView / aEdge : 確定させるエッジ
        void IEdgeConnectorListener.OnDrop(GraphView aGraphView, Edge aEdge)
        {
            // 単一接続しか持てないポートに繋ぎ直した場合は、先に既存の接続を削除する
            // (本実装は入出力とも Capacity.Multi のため実質的に何も削除されないが、標準の挙動に合わせておく)
            var edgesToDelete = new List<GraphElement>();
            if (aEdge.input.capacity == Capacity.Single)
            {
                foreach (Edge connected in aEdge.input.connections)
                {
                    if (connected != aEdge)
                    {
                        edgesToDelete.Add(connected);
                    }
                }
            }
            if (aEdge.output.capacity == Capacity.Single)
            {
                foreach (Edge connected in aEdge.output.connections)
                {
                    if (connected != aEdge)
                    {
                        edgesToDelete.Add(connected);
                    }
                }
            }
            if (edgesToDelete.Count > 0)
            {
                aGraphView.DeleteElements(edgesToDelete);
            }

            // graphViewChangedを通すことでVFXSequencerGraphView側のアセット反映処理(ConnectEdge)が走る
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
        // aEdge : ドラッグ中だった未確定のエッジ / aPosition : ドロップ位置(ワールド座標)
        void IEdgeConnectorListener.OnDropOutsidePort(Edge aEdge, Vector2 aPosition)
        {
            VFXSequencerGraphView graphView = GetFirstAncestorOfType<VFXSequencerGraphView>();
            if (graphView == null)
            {
                return;
            }

            graphView.ShowNodeCreationMenuForDrop(this, aPosition);
        }
    }
}
