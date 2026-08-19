/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file CallMethodTool.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief GameObjectのコンポーネント上の任意publicメソッドをリフレクションで呼び出す汎用MCPツール
 * 特定のゲームロジック検証手順は本ツールの対象外とし、「任意のpublicメソッドを呼べる」
 * 汎用機構のみを提供する。実際に何を呼ぶかはMCPクライアント側が組み立てる
 * =====================================*/

using System;
using System.Linq;
using System.Reflection;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;

namespace MCPBridge.Editor.Tools
{
    public sealed class CallMethodTool : IMCPTool
    {
        public string Name => "call_method";

        public string Description =>
            "objectPath+componentTypeで解決した対象のpublicメソッドを、指定した引数で呼び出します。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["objectPath"] = new JObject { ["type"] = "string" },
                ["componentType"] = new JObject { ["type"] = "string" },
                ["method"] = new JObject { ["type"] = "string" },
                ["args"] = new JObject { ["type"] = "array" },
            },
            ["required"] = new JArray("objectPath", "componentType", "method"),
        };

        public JToken Invoke(JObject aArguments)
        {
            return MCPMainThreadDispatcher.RunOnMainThread(() =>
            {
                var target = MCPSceneObjectResolver.ResolveComponentOrGameObject(
                    aArguments.Value<string>("objectPath"),
                    aArguments.Value<string>("componentType"));

                var methodName = aArguments.Value<string>("method");
                var argTokens = aArguments["args"] as JArray;
                var argCount = argTokens?.Count ?? 0;

                // GetMethod(string, BindingFlags)はオーバーロードが複数あるとAmbiguousMatchExceptionを
                // 投げるため、引数の数で一致するオーバーロードをGetMethods()から選択する
                var method = target.GetType()
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == argCount);
                if (method == null)
                {
                    throw new InvalidOperationException($"メソッドが見つかりません(または引数の数が一致しません): {methodName}");
                }

                var args = MCPArgumentConverter.Convert(argTokens, method);
                var result = method.Invoke(target, args);
                return MCPArgumentConverter.ToJToken(result);
            });
        }
    }
}
