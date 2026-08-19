/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequenceConditionalBranchNode.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief 到達時に公開名のbool値を判定し、該当する側の接続先を全て発火する分岐ノード(VFXは持たない)
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;

namespace VFXUtility
{
    // 分岐ノードの接続先1件分のtrue/false設定。接続先IDはGraphView側の接続操作と同期して自動的に増減する
    [Serializable]
    public class VFXSequenceBranchCondition
    {
        [SerializeField] private string mTargetNodeId;

        [Label("true側で発火")]
        [SerializeField] private bool mFireOnTrue = true;

        public string TargetNodeId { get => mTargetNodeId; set => mTargetNodeId = value; }
        public bool FireOnTrue { get => mFireOnTrue; set => mFireOnTrue = value; }
    }

    [Serializable]
    public class VFXSequenceConditionalBranchNode : VFXSequenceNodeBase
    {
        [Label("条件用の公開名(bool)")]
        [SerializeField] private string mConditionExposedName;

        [Label("既定値(未設定時)")]
        [SerializeField] private bool mDefaultValue;

        [Label("接続先ごとのtrue/false", true)]
        [SerializeField] private List<VFXSequenceBranchCondition> mBranches = new();

        public string ConditionExposedName => mConditionExposedName;
        public bool DefaultValue => mDefaultValue;
        public List<VFXSequenceBranchCondition> Branches => mBranches;
    }
}
