/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file CompileAndCheckTool.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief AssetDatabase.Refresh()でスクリプトの再コンパイルを走らせ、結果を返すMCPツール
 * コンパイルは複数フレームにまたがる非同期処理のため、ScreenshotTool同様
 * RunOnMainThreadの内側で完了を同期的に待つとEditorApplication.updateが戻らずフリーズしうる。
 * そのためメインスレッドでは購読とトリガーのみ行い(Enqueueで投げっぱなし)、
 * 完了待ち自体はHTTPハンドラスレッド側でManualResetEventSlimを使って行う。
 * 同時に複数のtools/callが来てもCompilationPipelineの購読/解除が競合しないようlockで直列化する。
 * 診断用途のツールのためMCPModeStore.sPersistentToolNamesには登録せずDebugモードでも許可する
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;

namespace MCPBridge.Editor.Tools
{
    public sealed class CompileAndCheckTool : IMCPTool
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
        private static readonly object sGate = new();

        public string Name => "compile_and_check";

        public string Description =>
            "AssetDatabase.Refresh()でスクリプトの再コンパイルを走らせ、完了を待ってアセンブリごとのエラー・警告一覧を返します。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject(),
        };

        public JToken Invoke(JObject aArguments)
        {
            // execute_plan(PlanExecutor)はメインスレッド上のEditorApplication.updateから
            // 直接MCPToolRegistry.Call()を呼ぶため、この経路でコンパイル完了を同期待機すると
            // CompilationPipelineのコールバックを処理するEditorApplication.update自体が
            // 戻らずEditorごとフリーズする。そのためメインスレッドからの呼び出しは明示的に拒否する
            if (MCPMainThreadDispatcher.IsMainThread)
            {
                throw new MCPToolException(-32009,
                    "compile_and_checkはexecute_plan(ToolCallステップ)からは呼び出せません。MCPクライアントからtools/callで直接呼び出してください。");
            }

            lock (sGate)
            {
                var messages = new List<(string Assembly, CompilerMessage Message)>();
                using var done = new ManualResetEventSlim(false);

                void OnAssemblyFinished(string aAssembly, CompilerMessage[] aMessages)
                {
                    foreach (var m in aMessages)
                    {
                        messages.Add((aAssembly, m));
                    }
                }

                Action<object> onAllFinished = null;
                onAllFinished = _ => done.Set();

                MCPMainThreadDispatcher.Enqueue(() =>
                {
                    CompilationPipeline.assemblyCompilationFinished += OnAssemblyFinished;
                    CompilationPipeline.compilationFinished += onAllFinished;
                    AssetDatabase.Refresh();
                    if (!EditorApplication.isCompiling)
                    {
                        // 変更なし等でコンパイルがトリガーされなかった場合は即完了扱いにする
                        onAllFinished(null);
                    }
                });

                // 購読解除はEnqueue(投げっぱなし)ではなくRunOnMainThreadで同期的に行う。
                // 投げっぱなしのまま先にdoneをDispose(lockブロック終端 or throw)してしまうと、
                // 解除が反映される前にコンパイルが完了してonAllFinishedが破棄済みdoneに対して
                // Set()を呼ぶ競合(タイミング依存でまれに例外)が起こり得るため
                if (!done.Wait(Timeout))
                {
                    MCPMainThreadDispatcher.RunOnMainThread(() =>
                    {
                        CompilationPipeline.assemblyCompilationFinished -= OnAssemblyFinished;
                        CompilationPipeline.compilationFinished -= onAllFinished;
                        return null;
                    });
                    throw new MCPToolException(-32005, "コンパイル完了待機がタイムアウトしました。");
                }

                MCPMainThreadDispatcher.RunOnMainThread(() =>
                {
                    CompilationPipeline.assemblyCompilationFinished -= OnAssemblyFinished;
                    CompilationPipeline.compilationFinished -= onAllFinished;
                    return null;
                });

                var success = messages.All(m => m.Message.type != CompilerMessageType.Error);
                var byAssembly = messages.GroupBy(m => m.Assembly).Select(g => new JObject
                {
                    ["assembly"] = g.Key,
                    ["messages"] = new JArray(g.Select(x => new JObject
                    {
                        ["type"] = x.Message.type.ToString(),
                        ["message"] = x.Message.message,
                        ["file"] = x.Message.file,
                        ["line"] = x.Message.line,
                    })),
                });

                return new JObject { ["success"] = success, ["assemblies"] = new JArray(byAssembly) };
            }
        }
    }
}
