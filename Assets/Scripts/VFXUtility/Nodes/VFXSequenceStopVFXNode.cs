/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequenceStopVFXNode.cs
 * @author hqrse
 * @date 2026/08/18
 * @brief 到達時に指定ノードが再生中のVFXを(全セッション横断で)停止する制御ノード(VFXは持たない)
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;

namespace VFXUtility
{
    [Serializable]
    public class VFXSequenceStopVFXNode : VFXSequenceNodeBase
    {
        [Label("対象ノードID")]
        [SerializeField] private string mTargetNodeId;

        public string TargetNodeId => mTargetNodeId;
    }
}
