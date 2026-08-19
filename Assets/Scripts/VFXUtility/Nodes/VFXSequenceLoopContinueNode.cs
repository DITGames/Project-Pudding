/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequenceLoopContinueNode.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief 到達時に対象ループノードの周回カウントを見て、次周回への再発火または完了後への進行を行う制御ノード(VFXは持たない)
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;

namespace VFXUtility
{
    [Serializable]
    public class VFXSequenceLoopContinueNode : VFXSequenceNodeBase
    {
        [Label("対象ループノードID")]
        [SerializeField] private string mTargetLoopNodeId;

        public string TargetLoopNodeId => mTargetLoopNodeId;
    }
}
