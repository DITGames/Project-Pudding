/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequenceEventNode.cs
 * @author hqrse
 * @date 2026/08/18
 * @brief 自動開始せず、PlayEvent(イベント名)が呼ばれるまで待機する入り口ノード
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace VFXUtility
{
    [Serializable]
    public class VFXSequenceEventNode : VFXSequencePlayableNodeBase
    {
        [Label("イベント名")]
        [SerializeField] private string mEventName;

        public string EventName => mEventName;
    }
}
