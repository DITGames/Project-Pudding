/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file AnimSequenceEntryNodeView.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief ノードグラフ上の1アニメーションキー(エントリ)を表すGraphView用ノードビュー
 * =====================================*/

using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace AnimSequencer2D.Editor
{
    internal class AnimSequenceEntryNodeView : Node
    {
        // ノードの識別に使うアニメーションキー(AnimSequenceEntry.Keyと同じ値。一意性は呼び出し元が保証する)
        public string Key { get; private set; }
        public Port InputPort { get; }
        public Port OutputPort { get; }

        // ノード右クリックメニューから「複製」が選ばれた際に呼ぶ(複製元のKeyを渡す)
        public event Action<string> OnDuplicateRequested;

        // aKey : 対象エントリのキー / aPosition : グラフ上の座標
        public AnimSequenceEntryNodeView(string aKey, Vector2 aPosition)
        {
            Key = aKey;
            title = aKey; // ノード上での直接編集はさせない(リネームはInspectorのみ)

            // 入力: 複数の他エントリから同じ遷移先へ接続できるようCapacity.Multi
            InputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            InputPort.portName = string.Empty;
            inputContainer.Add(InputPort);

            // 出力: 1エントリのTransitionTargetKeyは1つのみのためCapacity.Single
            OutputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            OutputPort.portName = string.Empty;
            outputContainer.Add(OutputPort);

            SetPosition(new Rect(aPosition, new Vector2(160, 80)));

            RefreshExpandedState();
            RefreshPorts();
        }

        // Key変更(Inspector経由のリネーム)をノードタイトルへ反映する
        // aNewKey : リネーム後のキー
        public void RefreshTitle(string aNewKey)
        {
            Key = aNewKey;
            title = aNewKey;
        }

        // プレビュー再生中ハイライトの表示切替。AnimSequenceEntryGraphView.SetPlayingKeyから呼ぶ
        public void SetPlaying(bool aIsPlaying) => EnableInClassList("anim-seq-node--playing", aIsPlaying);

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("複製", _ => OnDuplicateRequested?.Invoke(Key));
            base.BuildContextualMenu(evt);
        }
    }
}
