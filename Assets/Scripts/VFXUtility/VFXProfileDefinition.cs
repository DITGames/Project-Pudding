/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXProfileDefinition.cs
 * @author hqrse
 * @date 2026/08/16
 * @brief VFXエントリとパラメータ定義を再利用可能な形でまとめて保持するアセット
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace VFXUtility
{
    [CreateAssetMenu(fileName = "VFXProfileDefinition", menuName = "VFXUtility/VFXProfileDefinition")]
    public class VFXProfileDefinition : ScriptableObject
    {
        [Label("登録VFX一覧", true)]
        [SerializeField] private List<VFXEntry> mVfxEntries = new();

        [Label("パラメータ一覧", true)]
        [SerializeField] private List<VFXParameterEntry> mParameters = new();

        public IReadOnlyList<VFXEntry> VfxEntries => mVfxEntries;
        public IReadOnlyList<VFXParameterEntry> Parameters => mParameters;

        public VFXEntry FindEntry(string aVfxId)
        {
            return mVfxEntries.Find(e => e.VfxId == aVfxId);
        }

        public VFXParameterEntry FindParameter(string aVfxId, string aParamName)
        {
            return mParameters.Find(p => p.VfxId == aVfxId && p.ParamName == aParamName);
        }
    }
}
