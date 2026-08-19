/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPToolMode.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief ツール利用モード1件分のデータ(モード名+許可するツール名の一覧)
 * 制御粒度はツール単位とする(ツール内の個別メソッド名等の制限は対象外)
 * =====================================*/

using System;
using System.Collections.Generic;

namespace MCPBridge.Editor.Mode
{
    [Serializable]
    public sealed class MCPToolMode
    {
        public string Name;
        public List<string> AllowedToolNames = new();
    }
}
