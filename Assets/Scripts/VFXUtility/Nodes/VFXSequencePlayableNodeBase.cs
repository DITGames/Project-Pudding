/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequencePlayableNodeBase.cs
 * @author hqrse
 * @date 2026/08/18
 * @brief VFXを実際に再生するノード(通常ノード・イベントノード)の共通基底クラス
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;
using UnityEngine.VFX;

namespace VFXUtility
{
    [Serializable]
    public abstract class VFXSequencePlayableNodeBase : VFXSequenceNodeBase
    {
        [Label("VFXアセット")]
        [SerializeField] private VisualEffectAsset mVisualEffectAsset;

        [Label("パラメータ", true)]
        [SerializeField] private List<VFXSequenceNodeParameter> mParameters = new();

        public VisualEffectAsset VisualEffectAsset => mVisualEffectAsset;
        public IReadOnlyList<VFXSequenceNodeParameter> Parameters => mParameters;
    }
}
