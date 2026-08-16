/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequenceStep.cs
 * @author hqrse
 * @date 2026/08/16
 * @brief VFXSequenceDefinitionが管理する1ステップ分の再生情報
 * =====================================*/

using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace VFXUtility
{
    [Serializable]
    public class VFXSequenceStep
    {
        [Label("対象VFX ID")]
        [SerializeField] private string mVfxId;

        [Label("直前のステップからの遅延(秒)")]
        [SerializeField] private float mDelaySeconds;

        [Label("再生後に適用するパラメータ名", true)]
        [SerializeField] private List<string> mParamNamesToApply = new();

        public string VfxId => mVfxId;
        public float DelaySeconds => mDelaySeconds;
        public IReadOnlyList<string> ParamNamesToApply => mParamNamesToApply;
    }
}
