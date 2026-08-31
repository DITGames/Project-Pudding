/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAIProfileNamePostprocessor.cs
 * @author hqrse
 * @date 2026/08/31
 * @brief 判断ツリーのオブジェクト名をファイル名へ揃える
 * =====================================*/

using System.IO;
using UnityEditor;

namespace PPCore
{
    // 判断ツリーのオブジェクト名を、取り込みのたびにファイル名へ揃える
    //
    // ScriptableObject は内部に持つ名前とファイル名が別物で、ずれると保存のたびに
    // 「Main Object Name '◯◯' does not match filename '△△'」の警告が出続ける
    //
    // ずれる経路は 1 つではない
    //   ・同じ名前で作ろうとしてファイル名だけ連番になった（PUAP_Verify と PUAP_Verify1）
    //   ・複製した
    //   ・Unity の外でファイル名を変えた
    // 作る側で塞いでも取りこぼすため、取り込みの時点で揃える
    public sealed class PPUnitAIProfileNamePostprocessor : AssetPostprocessor
    {
        // 取り込まれたアセットのうち、判断ツリーの名前がファイル名と食い違っていれば直す
        // aImportedAssets : 取り込まれたアセットのパス
        // aDeletedAssets : 削除されたアセットのパス
        // aMovedAssets : 移動後のパス
        // aMovedFromAssetPaths : 移動前のパス
        private static void OnPostprocessAllAssets(string[] aImportedAssets, string[] aDeletedAssets,
            string[] aMovedAssets, string[] aMovedFromAssetPaths)
        {
            FixNames(aImportedAssets);
            // 移動やリネームの直後も食い違いが起きるため、そちらも見る
            FixNames(aMovedAssets);
        }

        // 指定されたパスの判断ツリーについて、名前をファイル名へ揃える
        // aPaths : 調べるアセットのパス
        private static void FixNames(string[] aPaths)
        {
            foreach (string path in aPaths)
            {
                if (!path.EndsWith(".asset")) continue;

                var profile = AssetDatabase.LoadAssetAtPath<PPUnitAIProfileDefinition>(path);
                if (profile == null) continue;

                string fileName = Path.GetFileNameWithoutExtension(path);
                if (profile.name == fileName) continue;

                // 揃えた結果また取り込みが走るが、次はここで弾かれるため繰り返しにはならない
                profile.name = fileName;
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);
            }
        }
    }
}
