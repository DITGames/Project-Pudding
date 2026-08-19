/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequenceOverrideSet.cs
 * @author hqrse
 * @date 2026/08/18
 * @brief 公開名と値の組をまとめて保持し、シーケンスへ一括適用するためのアセット
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;

namespace VFXUtility
{
    [Serializable]
    public class VFXSequenceOverrideEntry : VFXParameterValueBase
    {
        [Label("有効")]
        [SerializeField] private bool mEnabled = true;

        [Label("公開名")]
        [SerializeField] private string mExposedName;

        // オフにすると、このエントリは適用時にスキップされる(一部の値だけ変えるバリエーションを作れる)
        public bool Enabled => mEnabled;
        public string ExposedName => mExposedName;

        // エディタからの収集時に公開名を設定する
        // aExposedName : 設定する公開名
        public void SetExposedName(string aExposedName)
        {
            mExposedName = aExposedName;
        }
    }

    [CreateAssetMenu(fileName = "VFXOverrideSet", menuName = "VFXUtility/VFXSequenceOverrideSet")]
    public class VFXSequenceOverrideSet : ScriptableObject
    {
        [Label("対象シーケンス定義")]
        [SerializeField] private VFXSequenceDefinition mTargetDefinition;

        [Label("上書きパラメータ", true)]
        [SerializeField] private List<VFXSequenceOverrideEntry> mEntries = new();

        public VFXSequenceDefinition TargetDefinition => mTargetDefinition;
        public IReadOnlyList<VFXSequenceOverrideEntry> Entries => mEntries;

#if UNITY_EDITOR
        // 対象定義から収集した結果でエントリ一覧を差し替える(エディタ専用)
        // aEntries : 差し替えるエントリ一覧
        public void EditorSetEntries(List<VFXSequenceOverrideEntry> aEntries)
        {
            mEntries = aEntries;
        }
#endif
    }
}
