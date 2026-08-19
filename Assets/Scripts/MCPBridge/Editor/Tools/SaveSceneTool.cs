/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file SaveSceneTool.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief 現在のシーンをディスクへ保存するMCPツール
 * 唯一シーンファイルを直接上書きするツールであり、ツール利用モードでは明示モードでのみ許可する
 * (Debugモードには含めず、SceneEditモードでのみ許可対象とする)
 * =====================================*/

using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace MCPBridge.Editor.Tools
{
    public sealed class SaveSceneTool : IMCPTool
    {
        public string Name => "save_scene";

        public string Description => "現在アクティブなシーンをディスクへ保存します(ディスクへの永続化を伴う操作)。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject(),
        };

        public JToken Invoke(JObject aArguments)
        {
            return MCPMainThreadDispatcher.RunOnMainThread(() =>
            {
                var scene = SceneManager.GetActiveScene();
                var saved = EditorSceneManager.SaveScene(scene);
                return new JObject { ["saved"] = saved, ["path"] = scene.path };
            });
        }
    }
}
