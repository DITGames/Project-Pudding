/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequenceLoopNode.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief ループの開始点となるノード(VFXは持たない)。本体接続先(mNextNodeIds)は対応するループ継続ノードから繰り返し発火される
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace VFXUtility
{
    [Serializable]
    public class VFXSequenceLoopNode : VFXSequenceNodeBase
    {
        [Label("ループ回数(0=無限)")]
        [SerializeField] private int mLoopCount;

        public int LoopCount => mLoopCount;
    }
}
