/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPToolRegistry.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief 全MCPツールの登録・解決を行う
 * tools/list(現在のモードで許可されたツールの列挙)・tools/call(名前解決してInvoke)の
 * 両ハンドラから利用する。モードによる許可チェックもここで一元的に行う。
 * IMCPTool実装はTypeCacheで自動収集するため、導入先はクラスを置くだけでツールを追加できる
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MCPBridge.Editor.Logging;
using MCPBridge.Editor.Mode;
using MCPBridge.Editor.Server;
using MCPBridge.Editor.Window;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MCPBridge.Editor.Tools
{
    public static class MCPToolRegistry
    {
        private const string LogTag = "MCPBridge";

        private static readonly Dictionary<string, IMCPTool> sTools = new();
        private static bool sInitialized;

        public static IEnumerable<string> AllToolNames
        {
            get
            {
                EnsureInitialized();
                return sTools.Keys;
            }
        }

        // 現在のモードで許可されているツールのみを返す(tools/list用)
        public static IEnumerable<IMCPTool> ListAllowedTools()
        {
            EnsureInitialized();
            return sTools.Values.Where(t => MCPModeRegistry.IsAllowed(t.Name));
        }

        // tools/callのディスパッチ。未知のツール名、またはモードで許可されていないツールはエラーにする
        public static JToken Call(string aName, JObject aArguments)
        {
            EnsureInitialized();

            if (string.IsNullOrEmpty(aName) || !sTools.TryGetValue(aName, out var tool))
            {
                throw new MCPToolException(-32601, $"Unknown tool: {aName}");
            }
            if (!MCPModeRegistry.IsAllowed(aName))
            {
                throw new MCPToolException(-32001,
                    $"Tool '{aName}' is not allowed in current mode '{MCPModeRegistry.CurrentMode.Name}'.");
            }

            try
            {
                var result = tool.Invoke(aArguments);
                // Call()自体がHTTPハンドラスレッド・メインスレッド(execute_plan経由)の
                // どちらからも呼ばれ得るため、記録はEnqueueで必ずメインスレッドへ委譲する
                MCPMainThreadDispatcher.Enqueue(() => MCPToolCallLog.RecordSuccess(aName));
                return result;
            }
            catch (Exception e)
            {
                MCPMainThreadDispatcher.Enqueue(() => MCPToolCallLog.RecordError(aName, e.Message));
                throw;
            }
        }

        // 静的コンストラクタで走査すると[InitializeOnLoad]の実行順序に依存してしまうため、
        // 各入口からの遅延初期化にしている。MCPModeStore.CreateDefault()がAllToolNamesを
        // 参照する経路があり、走査中に再入する可能性があるためフラグは走査前に立てる
        private static void EnsureInitialized()
        {
            if (sInitialized)
            {
                return;
            }

            // TypeCache等のEditor APIはメインスレッド前提だが、tools/listはHTTPハンドラスレッドから
            // MCPProtocolHandler.HandleToolsList()経由で直接呼ばれる(ツール本体と違いディスパッチを
            // 挟んでいない)ため、ここで必ずメインスレッドへ寄せる。
            // 既にメインスレッド上ならRunOnMainThreadは即時実行するので自己デッドロックはしない。
            // sInitialized/sToolsの変更を全てこのラムダ内に閉じることで、RunOnMainThreadが
            // タイムアウトした場合でも呼び出し元スレッドとメインスレッドがDictionaryを
            // 同時に触る状況を作らない
            MCPMainThreadDispatcher.RunOnMainThread(() =>
            {
                // メインスレッドへ渡るまでの間に別経路の走査が完了している場合があるため再判定する
                // (タイムアウト後に積み残った処理が後から走るケースでの二重走査を防ぐ)
                if (sInitialized)
                {
                    return null;
                }

                sInitialized = true;

                try
                {
                    DiscoverTools();
                }
                catch (Exception e)
                {
                    // 走査に失敗したまま初期化済み扱いで固定されると、ドメインリロードまで
                    // 全ツールがUnknown tool扱いになり復旧手段が無くなる。次の入口で再走査できるよう
                    // フラグを戻す(再入防止は走査中だけ効けばよい)。
                    // 途中まで登録された分は名前重複の誤検出を招くため破棄する
                    sInitialized = false;
                    sTools.Clear();
                    MCPLog.Error(LogTag, $"MCPツールの走査に失敗しました: {e.Message}");
                }

                return null;
            });
        }

        private static void DiscoverTools()
        {
            // AssetImportWorkerは別プロセスでMCPトランスポートを持たず、ドメインリロード中の
            // 型メタデータ参照でMonoごとクラッシュしうるため走査しない
            if (IsRunningInAssetImportWorker())
            {
                return;
            }

            var skipped = 0;
            foreach (var type in TypeCache.GetTypesDerivedFrom<IMCPTool>())
            {
                if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters)
                {
                    continue;
                }
                if (type.IsDefined(typeof(MCPToolIgnoreAttribute), false))
                {
                    continue;
                }
                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    MCPLog.Warning(LogTag, $"引数なしコンストラクタが無いため登録をスキップします: {type.FullName}");
                    skipped++;
                    continue;
                }

                try
                {
                    var tool = (IMCPTool)Activator.CreateInstance(type);
                    if (string.IsNullOrEmpty(tool.Name))
                    {
                        MCPLog.Error(LogTag, $"ツール名が空のため登録をスキップします: {type.FullName}");
                        skipped++;
                        continue;
                    }
                    if (sTools.TryGetValue(tool.Name, out var existing))
                    {
                        MCPLog.Error(LogTag,
                            $"ツール名が重複しています: {tool.Name} ({existing.GetType().FullName} を保持し {type.FullName} を破棄)");
                        skipped++;
                        continue;
                    }

                    sTools[tool.Name] = tool;
                }
                catch (Exception e)
                {
                    // 1つの型の失敗で全体の登録を止めない
                    MCPLog.Error(LogTag, $"ツールの生成に失敗しました: {type.FullName} / {e.Message}");
                    skipped++;
                }
            }

            MCPLog.Log(LogTag, $"MCPツールを{sTools.Count}件登録しました(スキップ {skipped}件)");
        }

        // AssetDatabase.IsAssetImportWorkerProcessはUnityバージョンによって可視性が変わるため、
        // リフレクションで参照し、取得できない場合はワーカーではないとみなす
        private static bool IsRunningInAssetImportWorker()
        {
            try
            {
                var method = typeof(AssetDatabase).GetMethod(
                    "IsAssetImportWorkerProcess",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                if (method != null && method.GetParameters().Length == 0)
                {
                    return method.Invoke(null, null) is true;
                }
            }
            catch (Exception)
            {
                // 判定できない場合は通常プロセスとして扱う
            }

            return false;
        }
    }
}
