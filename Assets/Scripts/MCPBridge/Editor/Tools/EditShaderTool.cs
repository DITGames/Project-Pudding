/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file EditShaderTool.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief .shader(ShaderLabテキスト)ファイルを新規作成・上書きするMCPツール
 * .shadergraphはノードグラフを内部でシリアライズした複雑なフォーマットであり、
 * テキスト操作での編集は破損リスクが高いため明示的に対象外とする。
 * assetPathはAssets/配下に閉じ込める(プロジェクト外への書き込みを防ぐ)。
 * ファイルへの永続化を伴うため明示モードでのみ許可する
 * =====================================*/

using System;
using System.IO;
using System.Text;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MCPBridge.Editor.Tools
{
    public sealed class EditShaderTool : IMCPTool
    {
        public string Name => "edit_shader";

        public string Description =>
            ".shader(ShaderLabテキスト)ファイルを新規作成または上書きします(.shadergraphは対象外。ディスクへの永続化を伴います)。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["assetPath"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "例: Assets/Shaders/MyShader.shader",
                },
                ["source"] = new JObject { ["type"] = "string", ["description"] = "ShaderLabのソーステキスト全体" },
            },
            ["required"] = new JArray("assetPath", "source"),
        };

        public JToken Invoke(JObject aArguments)
        {
            return MCPMainThreadDispatcher.RunOnMainThread(() =>
            {
                var assetPath = aArguments.Value<string>("assetPath");
                if (assetPath.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase))
                {
                    throw new MCPToolException(-32007, ".shadergraphは対象外です。.shader(ShaderLabテキスト)のみ編集できます。");
                }
                if (!assetPath.EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
                {
                    throw new MCPToolException(-32007, $".shader拡張子のパスを指定してください: {assetPath}");
                }
                if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal) || assetPath.Contains(".."))
                {
                    throw new MCPToolException(-32007,
                        $"assetPathは\"Assets/\"配下の相対パスで指定してください(\"..\"は使用できません): {assetPath}");
                }

                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(fullPath, aArguments.Value<string>("source"), new UTF8Encoding(false));

                AssetDatabase.ImportAsset(assetPath);
                return new JObject { ["assetPath"] = assetPath };
            });
        }
    }
}
