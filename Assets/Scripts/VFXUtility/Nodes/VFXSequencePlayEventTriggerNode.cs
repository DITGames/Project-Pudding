/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequencePlayEventTriggerNode.cs
 * @author hqrse
 * @date 2026/08/18
 * @brief 到達時に同一グラフ内の一致するイベントノードを発火させる制御ノード(VFXは持たない)
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace VFXUtility
{
    [Serializable]
    public class VFXSequencePlayEventTriggerNode : VFXSequenceNodeBase
    {
        [Label("発火先イベント名")]
        [SerializeField] private string mEventName;

        public string EventName => mEventName;
    }
}
