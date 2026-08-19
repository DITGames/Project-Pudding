/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequenceRandomBranchNode.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief 到達時に接続先ごとの重みで抽選し、選ばれた1つのみを発火する分岐ノード(VFXは持たない)
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;

namespace VFXUtility
{
    // 分岐ノードの接続先1件分の重み設定。接続先IDはGraphView側の接続操作と同期して自動的に増減する
    [Serializable]
    public class VFXSequenceBranchWeight
    {
        [SerializeField] private string mTargetNodeId;

        [Label("重み")]
        [SerializeField] private float mWeight = 1f;

        public string TargetNodeId { get => mTargetNodeId; set => mTargetNodeId = value; }
        public float Weight { get => mWeight; set => mWeight = value; }
    }

    [Serializable]
    public class VFXSequenceRandomBranchNode : VFXSequenceNodeBase
    {
        [Label("接続先ごとの重み", true)]
        [SerializeField] private List<VFXSequenceBranchWeight> mWeights = new();

        public List<VFXSequenceBranchWeight> Weights => mWeights;
    }
}
