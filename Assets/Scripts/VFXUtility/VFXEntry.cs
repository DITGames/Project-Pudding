/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXEntry.cs
 * @author hqrse
 * @date 2026/08/17
 * @brief VFXParameterComponentが管理する1件のVFX登録情報
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;
using UnityEngine.VFX;

namespace VFXUtility
{
    [Serializable]
    public class VFXEntry
    {
        [Label("VFX ID")]
        [SerializeField] private string mVfxId;

        [Label("VFXアセット")]
        [SerializeField] private VisualEffectAsset mVisualEffectAsset;

        public string VfxId => mVfxId;
        public VisualEffectAsset VisualEffectAsset => mVisualEffectAsset;
    }
}
