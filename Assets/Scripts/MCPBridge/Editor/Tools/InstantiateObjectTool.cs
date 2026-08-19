/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file InstantiateObjectTool.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief プレハブをシーンへ生成するMCPツール
 * メモリ上のシーン状態のみを変更する(save_sceneを呼ばない限りファイルへは反映されないため、
 * ツール利用モードでは常時許可側に分類する)
 * =====================================*/

using System;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPBridge.Editor.Tools
{
    public sealed class InstantiateObjectTool : IMCPTool
    {
        public string Name => "instantiate_object";

        public string Description => "prefabPathで指定したプレハブをシーンへ生成します。parentPathを指定すると子として配置します。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["prefabPath"] = new JObject { ["type"] = "string" },
                ["parentPath"] = new JObject { ["type"] = "string" },
            },
            ["required"] = new JArray("prefabPath"),
        };

        public JToken Invoke(JObject aArguments)
        {
            return MCPMainThreadDispatcher.RunOnMainThread(() =>
            {
                var prefabPath = aArguments.Value<string>("prefabPath");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException($"プレハブが見つかりません: {prefabPath}");
                }

                Transform parent = null;
                var parentPath = aArguments.Value<string>("parentPath");
                if (!string.IsNullOrEmpty(parentPath))
                {
                    parent = MCPSceneObjectResolver.ResolveGameObject(parentPath).transform;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                if (parent != null)
                {
                    instance.transform.SetParent(parent, false);
                }

                // 同名プレハブを複数回生成すると階層パスで個体を区別できなくなるため、
                // 兄弟内で一意な名前になるようリネームする(MCPSceneObjectResolverはパス解決に名前を使うため)
                GameObjectUtility.EnsureUniqueNameForSibling(instance);

                return new JObject { ["objectPath"] = MCPSceneObjectResolver.GetHierarchyPath(instance.transform) };
            });
        }
    }
}
