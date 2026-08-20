/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file CreateSceneTool.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief 新規シーンを作成しディスクへ保存するMCPツール
 * シーンファイルの新規作成(ディスクへの永続化)を伴うため、
 * ツール利用モードではsave_scene等と同様に明示モードでのみ許可する
 * =====================================*/

using System;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace MCPBridge.Editor.Tools
{
    public sealed class CreateSceneTool : IMCPTool
    {
        public string Name => "create_scene";

        public string Description =>
            "新規の空シーンを作成し、指定パスへ保存します(ディスクへの永続化を伴う操作)。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["scenePath"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "保存先(例: Assets/Scenes/NewScene.unity)",
                },
            },
            ["required"] = new JArray("scenePath"),
        };

        public JToken Invoke(JObject aArguments)
        {
            return MCPMainThreadDispatcher.RunOnMainThread(() =>
            {
                var scenePath = aArguments.Value<string>("scenePath");
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(scenePath) != null)
                {
                    throw new InvalidOperationException($"既にシーンが存在します: {scenePath}");
                }

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (!EditorSceneManager.SaveScene(scene, scenePath))
                {
                    throw new InvalidOperationException($"シーンの保存に失敗しました: {scenePath}");
                }

                return new JObject { ["scenePath"] = scene.path };
            });
        }
    }
}
