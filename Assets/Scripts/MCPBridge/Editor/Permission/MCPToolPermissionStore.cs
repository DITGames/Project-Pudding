/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file MCPToolPermissionStore.cs
 * @author hqrse
 * @date 2026/09/10
 * @brief ツールごとの許可状態の永続化
 * UserSettings/配下(Unity公式.gitignoreテンプレートでバージョン管理対象外の per-user・per-project
 * 設定置き場。Library/と異なりバージョンアップや再インポートでも消えにくい)に保存する。
 * 初回起動時は、ディスクへの永続化を伴う等リスクの高いツールだけを無効化した状態を初期値とする
 * =====================================*/

using System.Collections.Generic;
using System.IO;
using System.Linq;
using MCPBridge.Editor.Tools;
using Newtonsoft.Json;

namespace MCPBridge.Editor.Permission
{
    public static class MCPToolPermissionStore
    {
        private const string StoreDirectory = "UserSettings/MCPBridge";
        private const string StorePath = StoreDirectory + "/permissions.json";

        // 初回起動時に既定で無効化しておくツール。ディスクへの永続化を伴い、
        // ウィンドウ上で明示的に許可して初めて使えるようにすべきもの。
        // execute_menu_itemは拒否リストによる保護、compile_and_checkは診断用途のため
        // 初期状態から許可する対象としてここには含めない(SPEC.md/PLAN.mdの合意事項)
        private static readonly string[] sInitiallyDisabledToolNames =
        {
            "save_scene", "edit_asset", "create_terrain",
            "create_scene", "load_scene",
            "set_asset_import_settings", "manage_asset_file",
            "edit_shader",
            "set_material_property",
            "set_vfx_property",
            "pp_unit_creator_generate",
        };

        public static HashSet<string> Load()
        {
            if (!File.Exists(StorePath))
            {
                var defaultAllowed = CreateDefault();
                SaveIfAnyToolAllowed(defaultAllowed);
                return defaultAllowed;
            }

            var json = File.ReadAllText(StorePath);
            var data = JsonConvert.DeserializeObject<MCPToolPermissionStoreData>(json);

            // permissions.jsonが手動編集等で壊れ、AllowedToolNamesを読めない状態になっている場合は
            // ここで初期状態へ自己修復する
            if (data?.AllowedToolNames == null)
            {
                var defaultAllowed = CreateDefault();
                SaveIfAnyToolAllowed(defaultAllowed);
                return defaultAllowed;
            }

            return new HashSet<string>(data.AllowedToolNames);
        }

        // 許可ツールが1件も無い初期状態は永続化しない。
        // ツールが収集できていない状態(MCPトランスポートを持たないAssetImportWorker等、
        // MCPToolRegistryが走査を行わないプロセス)で保存すると、許可ツールが空のpermissions.jsonが
        // 本体のEditorプロセスにも残り、以降すべてのtools/callが拒否されてしまう
        private static void SaveIfAnyToolAllowed(HashSet<string> aAllowedToolNames)
        {
            if (aAllowedToolNames.Count > 0)
            {
                Save(aAllowedToolNames);
            }
        }

        public static void Save(HashSet<string> aAllowedToolNames)
        {
            Directory.CreateDirectory(StoreDirectory);
            var data = new MCPToolPermissionStoreData { AllowedToolNames = aAllowedToolNames.ToList() };
            File.WriteAllText(StorePath, JsonConvert.SerializeObject(data, Formatting.Indented));
        }

        private static HashSet<string> CreateDefault()
        {
            return MCPToolRegistry.AllToolNames.Except(sInitiallyDisabledToolNames).ToHashSet();
        }

        private sealed class MCPToolPermissionStoreData
        {
            public List<string> AllowedToolNames;
        }
    }
}
