/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequenceNodeBase.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief VFXSequenceDefinitionが保持するノードグラフの全ノード共通の基底クラス
 * =====================================*/

using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace VFXUtility
{
    [Serializable]
    public abstract class VFXSequenceNodeBase
    {
        [Label("ノードID")]
        [SerializeField] private string mNodeId = Guid.NewGuid().ToString("N");

        [Label("表示名(空なら種別名を表示)")]
        [SerializeField] private string mDisplayName;

        [Label("グラフ上の座標")]
        [SerializeField] private Vector2 mPosition;

        [Label("Delay(秒)")]
        [SerializeField] private float mDelaySeconds;

        [Label("通知イベント名(空なら通知しない)")]
        [SerializeField] private string mNotifyEventName;

        [Label("接続先ノードID", true)]
        [SerializeField] private List<string> mNextNodeIds = new();

        public string NodeId => mNodeId;

        // グラフ上のノードタイトルに使う表示名。空の場合はノード種別名(通常ノード等)を表示する
        public string DisplayName => mDisplayName;

        public Vector2 Position { get => mPosition; set => mPosition = value; }
        public float DelaySeconds => mDelaySeconds;
        public IReadOnlyList<string> NextNodeIds => mNextNodeIds;

        // Delay経過後にこのノードが発火した際、外部へ通知するイベント名。空の場合は通知しない
        // イベントノードが持つ受信用の「イベント名」とは別物で、こちらは外部へ知らせる送信用の名前
        public string NotifyEventName => mNotifyEventName;

        // 後続ノードへの接続を追加する(既に接続済みなら何もしない)
        // aTargetNodeId : 接続先ノードのID
        public void AddNextNode(string aTargetNodeId)
        {
            if (!mNextNodeIds.Contains(aTargetNodeId))
            {
                mNextNodeIds.Add(aTargetNodeId);
            }
        }

        // 後続ノードへの接続を削除する
        // aTargetNodeId : 削除する接続先ノードのID
        public void RemoveNextNode(string aTargetNodeId)
        {
            mNextNodeIds.Remove(aTargetNodeId);
        }
    }
}
