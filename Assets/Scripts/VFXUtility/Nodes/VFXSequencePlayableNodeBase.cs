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

        [Label("位置オフセット")]
        [SerializeField] private Vector3 mPositionOffset;

        [Label("回転オフセット")]
        [SerializeField] private Vector3 mRotationOffset;

        [Label("スケール倍率")]
        [SerializeField] private float mScaleOffset = 1f;

        public VisualEffectAsset VisualEffectAsset => mVisualEffectAsset;
        public IReadOnlyList<VFXSequenceNodeParameter> Parameters => mParameters;
        public Vector3 PositionOffset => mPositionOffset;
        public Vector3 RotationOffset => mRotationOffset;
        public float ScaleOffset => mScaleOffset;
    }
}
