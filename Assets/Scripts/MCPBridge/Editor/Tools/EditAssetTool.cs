/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file EditAssetTool.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief ScriptableObject等のアセットファイルの値を書き換えるMCPツール
 * SerializedObject/SerializedProperty経由で書き込むことで、Undo・ダーティフラグ管理を
 * Unity標準の仕組みに委ね、生のリフレクションより安全にシリアライズ整合性を保つ。
 * アセットファイルを直接上書きするツールであり、ツール利用モードでは明示モードでのみ許可する
 * =====================================*/

using System;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MCPBridge.Editor.Tools
{
    public sealed class EditAssetTool : IMCPTool
    {
        public string Name => "edit_asset";

        public string Description => "assetPathで指定したアセットのpropertyPathへ値を書き込み、保存します(ディスクへの永続化を伴う操作)。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["assetPath"] = new JObject { ["type"] = "string" },
                ["propertyPath"] = new JObject { ["type"] = "string" },
                ["value"] = new JObject(),
            },
            ["required"] = new JArray("assetPath", "propertyPath", "value"),
        };

        public JToken Invoke(JObject aArguments)
        {
            return MCPMainThreadDispatcher.RunOnMainThread(() =>
            {
                var assetPath = aArguments.Value<string>("assetPath");
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (asset == null)
                {
                    throw new InvalidOperationException($"アセットが見つかりません: {assetPath}");
                }

                var serializedObject = new SerializedObject(asset);
                var property = serializedObject.FindProperty(aArguments.Value<string>("propertyPath"));
                if (property == null)
                {
                    throw new InvalidOperationException($"プロパティが見つかりません: {aArguments.Value<string>("propertyPath")}");
                }

                MCPArgumentConverter.ApplyToSerializedProperty(property, aArguments["value"]);
                serializedObject.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                return null;
            });
        }
    }
}
