/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file RecentAssetOpenTracker.cs
 * @author hqrse
 * @date 2026/07/10
 * @brief アセットが開かれたタイミングを検知し、履歴に記録するクラス
 * =====================================*/
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace RecentAssetsWindow.Editor
{
    public static class RecentAssetOpenTracker
    {
        // Unityがアセットを「開く」動作(ダブルクリック等)を行うたびに呼び出される。
        // falseを返すことでUnity標準のオープン処理はそのまま継続させ、記録のみを行う
        [OnOpenAsset(0)]
        private static bool OnOpenAsset(int aInstanceID, int aLine)
        {
            // GetAssetPath(int)はUnity6でEntityId版に置き換えられ非推奨警告(CS0618)が出るため、
            // EntityId版を明示的に呼び出す
            var path = AssetDatabase.GetAssetPath((EntityId)aInstanceID);
            if (!string.IsNullOrEmpty(path))
            {
                RecentAssetsHistory.Add(AssetDatabase.AssetPathToGUID(path));
            }
            return false;
        }
    }
}
