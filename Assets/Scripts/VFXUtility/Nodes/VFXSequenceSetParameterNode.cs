/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequenceSetParameterNode.cs
 * @author hqrse
 * @date 2026/08/18
 * @brief 到達時に指定ノードが再生中のVFXインスタンスへパラメータのみを適用する制御ノード(自身はVFXを持たない)
 * 新規にVFXを再生し直さず、既存の再生中インスタンスへパラメータ変更だけを反映したい場合に使う
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;

namespace VFXUtility
{
    [Serializable]
    public class VFXSequenceSetParameterNode : VFXSequenceNodeBase
    {
        [Label("対象ノードID")]
        [SerializeField] private string mTargetNodeId;

        [Label("パラメータ", true)]
        [SerializeField] private List<VFXSequenceNodeParameter> mParameters = new();

        public string TargetNodeId => mTargetNodeId;
        public IReadOnlyList<VFXSequenceNodeParameter> Parameters => mParameters;
    }
}
