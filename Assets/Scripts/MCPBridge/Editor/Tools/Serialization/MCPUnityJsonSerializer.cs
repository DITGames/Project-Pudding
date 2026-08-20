/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPUnityJsonSerializer.cs
 * @author hqrse
 * @date 2026/08/20
 * @brief Unity固有型のコンバーターを登録した共有JsonSerializer
 * 読み書きの双方をこの1インスタンス経由に統一することで、
 * get_fieldの出力とset_fieldの入力の表現が食い違わないようにする
 * =====================================*/

using System.Collections.Generic;
using Newtonsoft.Json;

namespace MCPBridge.Editor.Tools.Serialization
{
    internal static class MCPUnityJsonSerializer
    {
        public static readonly JsonSerializer Instance = JsonSerializer.Create(new JsonSerializerSettings
        {
            // 専用コンバーターを持たない型は計算プロパティを辿らずフィールドのみを見る
            ContractResolver = new MCPFieldOnlyContractResolver(),
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Converters = new List<JsonConverter>
            {
                new MCPVector2Converter(),
                new MCPVector3Converter(),
                new MCPVector4Converter(),
                new MCPVector2IntConverter(),
                new MCPVector3IntConverter(),
                new MCPQuaternionConverter(),
                new MCPColorConverter(),
                new MCPRectConverter(),
                new MCPBoundsConverter(),
                new MCPMatrix4x4Converter(),
                new MCPUnityObjectConverter(),
            },
        });
    }
}
