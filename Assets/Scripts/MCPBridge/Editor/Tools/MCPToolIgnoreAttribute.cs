/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPToolIgnoreAttribute.cs
 * @author hqrse
 * @date 2026/08/20
 * @brief IMCPTool実装を自動登録の対象から除外する属性
 * テスト用の実装や、条件付きでのみ登録したいツールに付ける
 * =====================================*/

using System;

namespace MCPBridge.Editor.Tools
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class MCPToolIgnoreAttribute : Attribute
    {
    }
}
