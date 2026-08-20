/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ManageAssetFileTool.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief アセットファイルの複製/移動/リネームを行うMCPツール
 * AssetDatabase.MoveAssetは移動・リネームの両方を兼ねる(パスの末尾ファイル名だけ
 * 変えて呼べばリネームになる)。ディスクへの永続化を伴うため明示モードでのみ許可する
 * =====================================*/

using System;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MCPBridge.Editor.Tools
{
    public sealed class ManageAssetFileTool : IMCPTool
    {
        public string Name => "manage_asset_file";

        public string Description =>
            "アセットファイルの複製(copy)または移動/リネーム(move)を行います(ディスクへの永続化を伴います)。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["action"] = new JObject { ["type"] = "string", ["enum"] = new JArray("copy", "move") },
                ["sourcePath"] = new JObject { ["type"] = "string" },
                ["destinationPath"] = new JObject { ["type"] = "string" },
            },
            ["required"] = new JArray("action", "sourcePath", "destinationPath"),
        };

        public JToken Invoke(JObject aArguments)
        {
            return MCPMainThreadDispatcher.RunOnMainThread(() =>
            {
                var action = aArguments.Value<string>("action");
                var sourcePath = aArguments.Value<string>("sourcePath");
                var destinationPath = aArguments.Value<string>("destinationPath");

                switch (action)
                {
                    case "copy":
                        if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
                        {
                            throw new InvalidOperationException($"アセットの複製に失敗しました: {sourcePath} -> {destinationPath}");
                        }
                        break;
                    case "move":
                        var error = AssetDatabase.MoveAsset(sourcePath, destinationPath);
                        if (!string.IsNullOrEmpty(error))
                        {
                            throw new InvalidOperationException($"アセットの移動に失敗しました: {error}");
                        }
                        break;
                    default:
                        throw new ArgumentException($"未対応のactionです: {action}");
                }

                return new JObject { ["action"] = action, ["destinationPath"] = destinationPath };
            });
        }
    }
}
