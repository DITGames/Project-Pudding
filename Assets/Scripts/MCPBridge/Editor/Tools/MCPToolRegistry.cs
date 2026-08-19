/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPToolRegistry.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief 全MCPツールの登録・解決を行う
 * tools/list(現在のモードで許可されたツールの列挙)・tools/call(名前解決してInvoke)の
 * 両ハンドラから利用する。モードによる許可チェックもここで一元的に行う
 * =====================================*/

using System.Collections.Generic;
using System.Linq;
using MCPBridge.Editor.Mode;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;

namespace MCPBridge.Editor.Tools
{
    public static class MCPToolRegistry
    {
        private static readonly Dictionary<string, IMCPTool> sTools = new();

        static MCPToolRegistry()
        {
            Register(new FindObjectTool());
            Register(new CallMethodTool());
            Register(new GetFieldTool());
            Register(new SetFieldTool());
            Register(new PlayModeControlTool());
            Register(new SimulateInputTool());
            Register(new GetInputStateTool());
            Register(new ScreenshotTool());
            Register(new GetLogsTool());
            Register(new ExecutePlanTool());
            Register(new GetExecutionStatusTool());
            Register(new InstantiateObjectTool());
            Register(new DestroyObjectTool());
            Register(new SaveSceneTool());
            Register(new EditAssetTool());
            Register(new CreateTerrainTool());
        }

        public static IEnumerable<string> AllToolNames => sTools.Keys;

        // 現在のモードで許可されているツールのみを返す(tools/list用)
        public static IEnumerable<IMCPTool> ListAllowedTools()
        {
            return sTools.Values.Where(t => MCPModeRegistry.IsAllowed(t.Name));
        }

        // tools/callのディスパッチ。未知のツール名、またはモードで許可されていないツールはエラーにする
        public static JToken Call(string aName, JObject aArguments)
        {
            if (string.IsNullOrEmpty(aName) || !sTools.TryGetValue(aName, out var tool))
            {
                throw new MCPToolException(-32601, $"Unknown tool: {aName}");
            }
            if (!MCPModeRegistry.IsAllowed(aName))
            {
                throw new MCPToolException(-32001,
                    $"Tool '{aName}' is not allowed in current mode '{MCPModeRegistry.CurrentMode.Name}'.");
            }
            return tool.Invoke(aArguments);
        }

        private static void Register(IMCPTool aTool)
        {
            sTools[aTool.Name] = aTool;
        }
    }
}
