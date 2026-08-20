/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPModeStore.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief モード定義の永続化
 * UserSettings/配下(Unity公式.gitignoreテンプレートでバージョン管理対象外の per-user・per-project
 * 設定置き場。Library/と異なりバージョンアップや再インポートでも消えにくい)に保存する。
 * 初回起動時は、常時許可(Debug)と永続化ツールも含むSceneEditの2種を初期モードとして生成する
 * =====================================*/

using System.Collections.Generic;
using System.IO;
using System.Linq;
using MCPBridge.Editor.Tools;
using Newtonsoft.Json;

namespace MCPBridge.Editor.Mode
{
    public static class MCPModeStore
    {
        private const string StoreDirectory = "UserSettings/MCPBridge";
        private const string StorePath = StoreDirectory + "/modes.json";

        private const string DebugModeName = "Debug";
        private const string SceneEditModeName = "SceneEdit";

        // ディスクへの永続化を伴い、明示モードでのみ許可すべきツール。
        // execute_menu_itemは拒否リストによる保護、compile_and_checkは診断用途のため
        // Debugモードでも常時許可する対象としてここには含めない(SPEC.md/PLAN.mdの合意事項)
        private static readonly string[] sPersistentToolNames =
        {
            "save_scene", "edit_asset", "create_terrain",
            "create_scene", "load_scene",
            "set_asset_import_settings", "manage_asset_file",
            "edit_shader",
            "set_material_property",
            "set_vfx_property",
        };

        public static (List<MCPToolMode> Modes, string CurrentModeName) Load()
        {
            if (!File.Exists(StorePath))
            {
                var (defaultModes, defaultCurrentName) = CreateDefault();
                SaveIfAnyToolAllowed(defaultModes, defaultCurrentName);
                return (defaultModes, defaultCurrentName);
            }

            var json = File.ReadAllText(StorePath);
            var data = JsonConvert.DeserializeObject<MCPModeStoreData>(json);

            // modes.jsonが手動編集等で壊れ、モードが1件も無い状態になっている場合は
            // MCPModeRegistry側でmodes[0]アクセスが例外になるため、ここで初期モードへ自己修復する
            if (data?.Modes == null || data.Modes.Count == 0)
            {
                var (defaultModes, defaultCurrentName) = CreateDefault();
                SaveIfAnyToolAllowed(defaultModes, defaultCurrentName);
                return (defaultModes, defaultCurrentName);
            }

            return (data.Modes, data.CurrentModeName);
        }

        // 許可ツールが1件も無い初期モードは永続化しない。
        // ツールが収集できていない状態(MCPトランスポートを持たないAssetImportWorker等、
        // MCPToolRegistryが走査を行わないプロセス)で保存すると、許可ツールが空のmodes.jsonが
        // 本体のEditorプロセスにも残り、以降すべてのtools/callが拒否されてしまう
        private static void SaveIfAnyToolAllowed(List<MCPToolMode> aModes, string aCurrentModeName)
        {
            if (aModes.Any(m => m.AllowedToolNames.Count > 0))
            {
                Save(aModes, aCurrentModeName);
            }
        }

        public static void Save(List<MCPToolMode> aModes, string aCurrentModeName)
        {
            Directory.CreateDirectory(StoreDirectory);
            var data = new MCPModeStoreData { Modes = aModes, CurrentModeName = aCurrentModeName };
            File.WriteAllText(StorePath, JsonConvert.SerializeObject(data, Formatting.Indented));
        }

        private static (List<MCPToolMode>, string) CreateDefault()
        {
            var allToolNames = MCPToolRegistry.AllToolNames.ToList();

            var debugMode = new MCPToolMode
            {
                Name = DebugModeName,
                AllowedToolNames = allToolNames.Except(sPersistentToolNames).ToList(),
            };
            var sceneEditMode = new MCPToolMode
            {
                Name = SceneEditModeName,
                AllowedToolNames = allToolNames.ToList(),
            };
            return (new List<MCPToolMode> { debugMode, sceneEditMode }, debugMode.Name);
        }

        private sealed class MCPModeStoreData
        {
            public List<MCPToolMode> Modes;
            public string CurrentModeName;
        }
    }
}
