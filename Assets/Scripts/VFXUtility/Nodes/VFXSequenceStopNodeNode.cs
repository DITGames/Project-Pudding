/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequenceStopNodeNode.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief 到達時に指定ブランチ(ルートノードの直接の接続先)から始まる全フローを(全セッション横断で)停止する制御ノード(VFXは持たない)
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace VFXUtility
{
    [Serializable]
    public class VFXSequenceStopNodeNode : VFXSequenceNodeBase
    {
        [Label("対象ノードID(ルートノードの直接の接続先)")]
        [SerializeField] private string mTargetBranchNodeId;

        public string TargetBranchNodeId => mTargetBranchNodeId;
    }
}
