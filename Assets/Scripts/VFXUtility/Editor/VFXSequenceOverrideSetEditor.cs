/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequenceOverrideSetEditor.cs
 * @author hqrse
 * @date 2026/08/18
 * @brief オーバーライドセットに、対象シーケンス定義から公開パラメータを収集するボタンを追加するインスペクタ
 * =====================================*/

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VFXUtility.Editor
{
    [CustomEditor(typeof(VFXSequenceOverrideSet))]
    internal class VFXSequenceOverrideSetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var overrideSet = (VFXSequenceOverrideSet)target;

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(overrideSet.TargetDefinition == null))
            {
                if (GUILayout.Button("対象定義から公開パラメータを収集"))
                {
                    CollectExposedParameters(overrideSet);
                }
            }

            if (overrideSet.TargetDefinition == null)
            {
                EditorGUILayout.HelpBox("対象シーケンス定義を設定すると、公開名が付いたパラメータを収集できます。", MessageType.Info);
            }
        }

        // 対象定義内の公開名が付いたパラメータを収集し、既定値で埋めたエントリ一覧を作り直す
        // 既に同じ公開名のエントリがある場合は、ユーザーが編集済みの値と有効フラグを引き継ぐ
        // aOverrideSet : 収集先のオーバーライドセット
        private void CollectExposedParameters(VFXSequenceOverrideSet aOverrideSet)
        {
            var existingEntries = new Dictionary<string, VFXSequenceOverrideEntry>();
            foreach (VFXSequenceOverrideEntry entry in aOverrideSet.Entries)
            {
                if (!string.IsNullOrEmpty(entry.ExposedName))
                {
                    existingEntries[entry.ExposedName] = entry;
                }
            }

            var collected = new List<VFXSequenceOverrideEntry>();
            var seenExposedNames = new HashSet<string>();

            foreach (VFXSequenceNodeParameter param in aOverrideSet.TargetDefinition.EnumerateAllParameters())
            {
                if (string.IsNullOrEmpty(param.ExposedName) || !seenExposedNames.Add(param.ExposedName))
                {
                    continue; // 公開名なしは対象外。同じ公開名は1件のみ扱う
                }

                if (existingEntries.TryGetValue(param.ExposedName, out VFXSequenceOverrideEntry existing))
                {
                    collected.Add(existing); // 編集済みの値を保持する
                    continue;
                }

                var newEntry = new VFXSequenceOverrideEntry();
                newEntry.SetExposedName(param.ExposedName);
                newEntry.CopyValueFrom(param); // 既定値で埋める
                collected.Add(newEntry);
            }

            Undo.RecordObject(aOverrideSet, "公開パラメータを収集");
            aOverrideSet.EditorSetEntries(collected);
            EditorUtility.SetDirty(aOverrideSet);
            serializedObject.Update();
        }
    }
}
