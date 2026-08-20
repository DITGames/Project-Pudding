/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file GetSceneHierarchyTool.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief 現在アクティブなシーンのGameObject階層をツリー構造で取得するMCPツール
 * FindObjectToolが単一オブジェクトの検索であるのに対し、本ツールは全ルートオブジェクトを
 * 一括で取得する。読み取り専用のためDebugモードでも許可する
 * =====================================*/

using System.Linq;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MCPBridge.Editor.Tools
{
    public sealed class GetSceneHierarchyTool : IMCPTool
    {
        public string Name => "get_scene_hierarchy";

        public string Description =>
            "現在アクティブなシーンの全ルートGameObjectから、名前・アクティブ状態・コンポーネント一覧を含む階層ツリーを取得します。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject(),
        };

        public JToken Invoke(JObject aArguments)
        {
            return MCPMainThreadDispatcher.RunOnMainThread(() =>
            {
                var roots = SceneManager.GetActiveScene().GetRootGameObjects();
                return new JArray(roots.Select(r => BuildNode(r.transform)));
            });
        }

        // Transform配下を再帰的にたどり、名前・アクティブ状態・コンポーネント一覧・子ノードを持つツリーを組み立てる
        private static JObject BuildNode(Transform aTransform)
        {
            return new JObject
            {
                ["name"] = aTransform.name,
                ["activeSelf"] = aTransform.gameObject.activeSelf,
                ["components"] = new JArray(aTransform.GetComponents<Component>().Select(c => c.GetType().FullName)),
                ["children"] = new JArray(
                    Enumerable.Range(0, aTransform.childCount).Select(i => BuildNode(aTransform.GetChild(i)))),
            };
        }
    }
}
