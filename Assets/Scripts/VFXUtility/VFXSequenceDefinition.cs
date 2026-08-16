/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequenceDefinition.cs
 * @author hqrse
 * @date 2026/08/16
 * @brief 複数VFXを時間差で再生する一連の演出をデータとして定義するアセット
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace VFXUtility
{
    [CreateAssetMenu(fileName = "VFXSequenceDefinition", menuName = "VFXUtility/VFXSequenceDefinition")]
    public class VFXSequenceDefinition : ScriptableObject
    {
        [Label("シーケンスステップ", true)]
        [SerializeField] private List<VFXSequenceStep> mSteps = new();

        public IReadOnlyList<VFXSequenceStep> Steps => mSteps;
    }
}
