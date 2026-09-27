/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitCreatorTools.cs
 * @author hqrse
 * @date 2026/09/09
 * @brief ユニット下書きの共通サービスへ接続するMCPアダプター
 * =====================================*/

using System;
using System.Linq;
using System.Reflection;
using MCPBridge.Editor.Server;
using MCPBridge.Editor.Tools;
using Newtonsoft.Json.Linq;
using PPCore;
using UnityEditor;
using UnityEngine;

namespace MCPBridgeBindings.Editor
{
    public sealed class PPUnitCreatorDraftTool : IMCPTool
    {
        public string Name => "pp_unit_creator_draft";
        public string Description => "ユニット下書きのcreate/get/update/evaluate/validate/open/compare/ranking。createにsource（既存PPUnitDefinitionの{guid,localFileId}）を渡すと、既存ユニットを編集する下書き（mode=edit）を作る。"
            + "編集モードではunitId・folderは変更不可で、pp_unit_creator_generateが元アセットへの保存（書き戻し）になる。updateはexpectedRevision必須。patchで共通入力、stats、skills、compareTarget、portraitを編集。"
            + "compareはtarget（省略時は下書きの比較対象）と下書きをlevels（省略時は1/プレビューLv/最大Lv）で評価し、下書き・比較対象・差分（下書き−比較対象）の5能力を返す。"
            + "rankingはstatとlevel（省略時は最大Lv）でAssets配下の全PPUnitDefinitionと下書きを降順に並べる（同値は同順位）。生成はpp_unit_creator_generateを使用。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["operation"] = new JObject { ["type"] = "string", ["enum"] = new JArray("create", "get", "update", "evaluate", "validate", "open", "compare", "ranking") },
                ["draftId"] = new JObject { ["type"] = "string" },
                ["source"] = PPUnitCreatorAdapter.ReferenceSchema("create用。編集する既存PPUnitDefinition。指定すると編集モードの下書きを作る"),
                ["expectedRevision"] = new JObject { ["type"] = "integer" },
                ["levels"] = new JObject { ["type"] = "array", ["description"] = "evaluate/compare用。省略時は1/プレビューLv/最大Lv", ["items"] = new JObject { ["type"] = "integer", ["minimum"] = 1 } },
                ["target"] = PPUnitCreatorAdapter.ReferenceSchema("compare用の比較対象PPUnitDefinition。省略時は下書きの比較対象"),
                ["stat"] = new JObject { ["type"] = new JArray("integer", "string"), ["description"] = "ranking用。indexまたはキー名: " + PPUnitCreatorAdapter.StatLegend },
                ["level"] = new JObject { ["type"] = "integer", ["minimum"] = 1, ["description"] = "ranking用の評価レベル。省略時は下書きの最大Lv" },
                ["patch"] = PPUnitCreatorAdapter.PatchSchema,
            },
            ["required"] = new JArray("operation"),
            ["additionalProperties"] = false,
        };

        public JToken Invoke(JObject aArguments) => MCPMainThreadDispatcher.RunOnMainThread(() =>
        {
            var operation = aArguments.Value<string>("operation");
            if (aArguments["patch"] != null && !(aArguments["patch"] is JObject)) throw new ArgumentException("patchはオブジェクトで指定してください。");
            if (operation == "create")
            {
                var source = PPUnitCreatorAdapter.Resolve<PPUnitDefinition>(aArguments["source"]);
                var created = source != null ? PPUnitCreationDraft.CreateFrom(source) : PPUnitCreationDraft.Create();
                try
                {
                    if (aArguments["patch"] is JObject patch) PPUnitCreatorAdapter.Apply(created, patch);
                    var description = PPUnitCreatorAdapter.Describe(created);
                    PPUnitDraftStore.Save(created);
                    return description;
                }
                catch { PPUnitCreatorAdapter.DestroyDraft(created); throw; }
            }
            var draft = PPUnitDraftStore.Load(aArguments.Value<string>("draftId") ?? "");
            switch (operation)
            {
                case "get": return PPUnitCreatorAdapter.Describe(draft);
                case "update":
                    if (aArguments["expectedRevision"] == null) throw new ArgumentException("expectedRevisionが必要です。");
                    draft.RequireRevision(aArguments.Value<int>("expectedRevision"));
                    var copy = draft.CreateWorkingCopy();
                    try
                    {
                        PPUnitCreatorAdapter.Apply(copy, aArguments["patch"] as JObject ?? throw new ArgumentException("patchが必要です。"));
                        var description = PPUnitCreatorAdapter.Describe(copy);
                        copy.Commit();
                        description["revision"] = copy.Revision;
                        return description;
                    }
                    catch { PPUnitCreatorAdapter.DestroyDraft(copy); throw; }
                case "evaluate":
                    var levels = Levels(aArguments, draft);
                    return new JObject
                    {
                        ["revision"] = draft.Revision,
                        ["values"] = new JArray(levels.Select(aLevel => new JObject { ["level"] = aLevel, ["stats"] = JObject.FromObject(draft.Unit.EvaluateStats(aLevel)) })),
                        ["validation"] = PPUnitCreatorAdapter.Validation(draft),
                    };
                case "validate": return PPUnitCreatorAdapter.Validation(draft);
                case "compare":
                    // 明示した比較対象を優先し、省略時は下書きに保存済みの比較対象を使う
                    var target = PPUnitCreatorAdapter.Resolve<PPUnitDefinition>(aArguments["target"]);
                    if (target == null) target = draft.CompareTarget;
                    if (target == null) throw new ArgumentException("比較対象がありません。targetを指定するか、下書きに比較対象を設定してください。");
                    return new JObject
                    {
                        ["draftId"] = draft.DraftId, ["revision"] = draft.Revision, ["statKeys"] = new JArray(PPUnitStatComparer.StatKeys),
                        ["target"] = new JObject { ["reference"] = PPUnitCreatorAdapter.Reference(target), ["unitId"] = target.UnitId, ["displayName"] = target.DisplayName },
                        ["values"] = new JArray(Levels(aArguments, draft).Select(aLevel => PPUnitStatComparer.Compare(draft, target, aLevel)).Select(aComparison => new JObject
                        {
                            ["level"] = aComparison.Level, ["draft"] = PPUnitCreatorAdapter.StatValues(aComparison.Draft),
                            ["target"] = PPUnitCreatorAdapter.StatValues(aComparison.Target), ["diff"] = PPUnitCreatorAdapter.StatValues(aComparison.Diff),
                        })),
                    };
                case "ranking":
                    var stat = PPUnitCreatorAdapter.StatIndex(aArguments["stat"]);
                    var level = aArguments["level"] != null ? aArguments.Value<int>("level") : draft.MaxLevel;
                    if (level < 1 || level > draft.MaxLevel) throw new ArgumentException("levelは下書きのレベル範囲内で指定してください。");
                    var ranking = PPUnitStatComparer.Rank(draft, PPUnitStatComparer.FindUnits(), stat, level);
                    return new JObject
                    {
                        ["draftId"] = draft.DraftId, ["revision"] = draft.Revision, ["level"] = level,
                        ["stat"] = new JObject { ["index"] = stat, ["key"] = PPUnitStatComparer.StatKeys[stat], ["name"] = PPUnitCreationDraft.StatNames[stat] },
                        ["draftRank"] = ranking.First(aRow => aRow.IsDraft).Rank, ["count"] = ranking.Count,
                        ["ranking"] = new JArray(ranking.Select(aRow => new JObject
                        {
                            ["rank"] = aRow.Rank, ["unitId"] = aRow.Unit.UnitId, ["displayName"] = aRow.Unit.DisplayName, ["value"] = aRow.Value,
                            ["isDraft"] = aRow.IsDraft, ["reference"] = aRow.IsDraft ? JValue.CreateNull() : PPUnitCreatorAdapter.Reference(aRow.Unit),
                        })),
                    };
                case "open": PPUnitCreatorWindow.OpenDraft(draft.DraftId); return PPUnitCreatorAdapter.Describe(draft);
                default: throw new ArgumentException("未知の操作です: " + operation);
            }
        });

        // 評価レベルの一覧。省略時は初期・プレビュー・最大の3点
        private static int[] Levels(JObject aArguments, PPUnitCreationDraft aDraft)
        {
            var levels = aArguments["levels"]?.ToObject<int[]>() ?? new[] { 1, aDraft.PreviewLevel, aDraft.MaxLevel };
            if (levels.Length > 1000 || levels.Any(aLevel => aLevel < 1 || aLevel > aDraft.MaxLevel)) throw new ArgumentException("levelsは下書きのレベル範囲内、1000件以下で指定してください。");
            return levels;
        }
    }

    public sealed class PPUnitCreatorGenerateTool : IMCPTool
    {
        public string Name => "pp_unit_creator_generate";
        public string Description => "検証済み下書きからユニットと関連アセットを生成する。編集モード（mode=edit）の下書きでは元のユニット・ビジュアル定義へ書き戻して保存する（GUID・パスは変わらない。読み込み後に元アセットが変わっていれば拒否）。"
            + "永続化操作。expectedRevisionとrequestId必須。同じ要求の再送は記録済み結果を返す。編集モードは新しいrequestIdなら何度でも保存できる。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["draftId"] = new JObject { ["type"] = "string" },
                ["expectedRevision"] = new JObject { ["type"] = "integer" },
                ["requestId"] = new JObject { ["type"] = "string", ["minLength"] = 1 },
            },
            ["required"] = new JArray("draftId", "expectedRevision", "requestId"),
            ["additionalProperties"] = false,
        };

        public JToken Invoke(JObject aArguments) => MCPMainThreadDispatcher.RunOnMainThread(() =>
        {
            if (aArguments["expectedRevision"] == null) throw new ArgumentException("expectedRevisionが必要です。");
            var draft = PPUnitDraftStore.Load(aArguments.Value<string>("draftId") ?? "");
            var paths = PPUnitCreationService.Create(draft, aArguments.Value<int>("expectedRevision"), aArguments.Value<string>("requestId"));
            if (draft.IsEditing)
                return new JObject
                {
                    ["draftId"] = draft.DraftId, ["mode"] = "edit", ["unitId"] = draft.Unit.UnitId, ["savedRevision"] = draft.SavedRevision, ["revision"] = draft.Revision,
                    ["assets"] = new JArray(paths.Select(aPath => new JObject { ["path"] = aPath, ["guid"] = AssetDatabase.AssetPathToGUID(aPath) })),
                    ["unitCatalogRegistered"] = draft.UnitCatalog != null,
                    ["visualCatalogRegistered"] = draft.VisualCatalog != null && draft.SourceVisual != null,
                    ["skillCatalogRegistered"] = draft.SkillCatalog != null,
                };
            return new JObject
            {
                ["draftId"] = draft.DraftId, ["unitId"] = draft.CreatedUnitId, ["revision"] = draft.CreatedRevision,
                ["assets"] = new JArray(paths.Select((aPath, aIndex) => new JObject { ["path"] = aPath, ["guid"] = draft.CreatedGuids[aIndex] })),
                ["unitCatalogRegistered"] = draft.UnitCatalog != null,
                ["visualCatalogRegistered"] = draft.VisualCatalog != null,
                ["skillCatalogRegistered"] = draft.SkillCatalog != null,
            };
        });
    }

    // ユニット作成ウィンドウの表示状態の読み書きと入力イベントの送信を行う操作ツール。AIによる動作確認やスクリーンショット撮影の前準備に使う
    // ディスクへの保存は行わない（ウィンドウが元から行う下書きの保存を除く）
    public sealed class PPUnitCreatorWindowTool : IMCPTool
    {
        private static readonly string[] RectKeys =
        {
            "tabToolbar", "parameterViewToolbar", "bodyScroll", "footerScroll", "previewSlider", "previewSliderTrack",
            "compareSlider", "compareSliderTrack", "rankingSlider", "rankingSliderTrack", "rankingStatPopup",
        };

        public string Name => "pp_unit_creator_window";
        public string Description => "開いているユニット作成ウィンドウを操作する（AIによる動作確認・スクリーンショット撮影用）。operationはstate/set/event。"
            + "stateは下書きID・更新番号・タブ・評価レベル・系列・位置と、操作対象の矩形（ウィンドウ内容の座標。OnGUIのEvent.mousePositionと同じ座標系。〜Trackはスライダーの溝）を返す。"
            + "setは表示状態だけを変える（下書きの内容・更新番号は変えない）。eventはscrollWheel/mouseDown/mouseDrag/mouseUp/repaintを順にSendEventで送り、各イベント後の更新番号を返す。"
            + "eventの座標は既定でstateの矩形と同じ座標で指定し、ドックのタブ帯などホストビューまでのずれ（viewOffset）はツールが実測して足す。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["operation"] = new JObject { ["type"] = "string", ["enum"] = new JArray("state", "set", "event") },
                ["draftId"] = new JObject { ["type"] = "string", ["description"] = "set用。その下書きを開く" },
                ["tab"] = new JObject { ["type"] = "integer", ["description"] = "set用。0=ビジュアル,1=パラメータ,2=スキル,3=ディテイル" },
                ["parameterView"] = new JObject { ["type"] = "integer", ["description"] = "set用。0=編集,1=比較,2=ランキング" },
                ["compareLevel"] = new JObject { ["type"] = "integer", ["minimum"] = 1, ["description"] = "set用。描画時に1〜最大Lvへ丸める" },
                ["rankingLevel"] = new JObject { ["type"] = "integer", ["minimum"] = 1, ["description"] = "set用。描画時に1〜最大Lvへ丸める" },
                ["rankingStat"] = new JObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 4, ["description"] = "set用。" + PPUnitCreatorAdapter.StatLegend },
                ["extraCollapsed"] = new JObject { ["type"] = "boolean", ["description"] = "set用。追加パラメータを閉じるか" },
                ["series"] = new JObject { ["type"] = "array", ["maxItems"] = 3, ["items"] = new JObject { ["type"] = "boolean" }, ["description"] = "set用。編集ビューの系列（初期・指定・最大）の表示" },
                ["size"] = new JObject
                {
                    ["type"] = "object", ["description"] = "set用。ウィンドウの大きさ（論理ピクセル）",
                    ["properties"] = new JObject { ["width"] = new JObject { ["type"] = "number" }, ["height"] = new JObject { ["type"] = "number" } },
                    ["required"] = new JArray("width", "height"),
                },
                ["focus"] = new JObject { ["type"] = "boolean", ["description"] = "set用。trueでウィンドウにフォーカスする" },
                ["events"] = new JObject
                {
                    ["type"] = "array", ["maxItems"] = 500, ["description"] = "event用。座標はcoordinatesで指定した座標系（既定はstateの矩形と同じウィンドウ内容の座標）",
                    ["items"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["type"] = new JObject { ["type"] = "string", ["enum"] = new JArray("scrollWheel", "mouseDown", "mouseDrag", "mouseUp", "repaint") },
                            ["x"] = new JObject { ["type"] = "number" }, ["y"] = new JObject { ["type"] = "number" },
                            ["deltaX"] = new JObject { ["type"] = "number" }, ["deltaY"] = new JObject { ["type"] = "number" },
                            ["button"] = new JObject { ["type"] = "integer", ["minimum"] = 0 },
                        },
                        ["required"] = new JArray("type"),
                    },
                },
                ["coordinates"] = new JObject
                {
                    ["type"] = "string", ["enum"] = new JArray("window", "view"),
                    ["description"] = "event用。window（既定）はstateの矩形と同じウィンドウ内容の座標で、送信時にviewOffset（ホストビュー左上からウィンドウ内容左上までのずれ。ドックのタブ帯を含む）を足す。viewは足さずにそのまま送る",
                },
            },
            ["required"] = new JArray("operation"),
            ["additionalProperties"] = false,
        };

        public JToken Invoke(JObject aArguments) => MCPMainThreadDispatcher.RunOnMainThread(() =>
        {
            var window = Resources.FindObjectsOfTypeAll<PPUnitCreatorWindow>().FirstOrDefault()
                ?? throw new InvalidOperationException("ユニット作成ウィンドウが開いていません。Window > Project-Pudding > ユニット作成 で開いてください。");
            var operation = aArguments.Value<string>("operation");
            switch (operation)
            {
                case "state":
                    return State(window);
                case "set":
                    if (aArguments["draftId"] != null)
                    {
                        var id = aArguments.Value<string>("draftId");
                        PPUnitDraftStore.Load(id);
                        PPUnitCreatorWindow.OpenDraft(id);
                    }
                    PPUnitCreatorWindow.PPWindowAccess.Set(window, Optional<int>(aArguments, "tab"), Optional<int>(aArguments, "parameterView"),
                        Optional<int>(aArguments, "compareLevel"), Optional<int>(aArguments, "rankingLevel"), Optional<int>(aArguments, "rankingStat"),
                        Optional<bool>(aArguments, "extraCollapsed"), aArguments["series"]?.ToObject<bool[]>());
                    if (aArguments["size"] is JObject size)
                        window.position = new Rect(window.position.x, window.position.y, size.Value<float>("width"), size.Value<float>("height"));
                    if (aArguments.Value<bool?>("focus") == true) window.Focus();
                    var refreshed = Refresh(window);
                    var state = State(window);
                    state["repaintedImmediately"] = refreshed;
                    return state;
                case "event":
                    var events = aArguments["events"] as JArray ?? throw new ArgumentException("eventsが必要です。");
                    if (events.Count > 500) throw new ArgumentException("eventsは500件以下です。");
                    var before = Revision(window);
                    var measured = ViewOffset(window);
                    var offset = aArguments.Value<string>("coordinates") == "view" ? Vector2.zero : measured.offset;
                    var results = new JArray();
                    foreach (var entry in events)
                    {
                        var type = entry.Value<string>("type");
                        if (type == "repaint") Refresh(window);
                        else
                        {
                            window.SendEvent(new Event
                            {
                                type = type switch
                                {
                                    "scrollWheel" => EventType.ScrollWheel,
                                    "mouseDown" => EventType.MouseDown,
                                    "mouseDrag" => EventType.MouseDrag,
                                    "mouseUp" => EventType.MouseUp,
                                    _ => throw new ArgumentException("未知のイベントです: " + type),
                                },
                                mousePosition = new Vector2(entry["x"]?.Value<float>() ?? 0, entry["y"]?.Value<float>() ?? 0) + offset,
                                delta = new Vector2(entry["deltaX"]?.Value<float>() ?? 0, entry["deltaY"]?.Value<float>() ?? 0),
                                button = entry["button"]?.Value<int>() ?? 0,
                                clickCount = type == "mouseDown" ? 1 : 0,
                            });
                        }
                        results.Add(new JObject
                        {
                            ["type"] = type, ["revision"] = Revision(window), ["hotControl"] = GUIUtility.hotControl,
                            ["previewPending"] = PPUnitCreatorWindow.PPWindowAccess.PreviewPending(window),
                        });
                    }
                    var repainted = Refresh(window);
                    return new JObject
                    {
                        ["repaintedImmediately"] = repainted, ["viewOffset"] = OffsetData(measured), ["appliedOffset"] = new JObject { ["x"] = offset.x, ["y"] = offset.y }, ["revisionBefore"] = before, ["revisionAfter"] = Revision(window),
                        ["revisions"] = new JArray(results.Select(aResult => aResult["revision"])), ["events"] = results, ["state"] = State(window),
                    };
                default: throw new ArgumentException("未知の操作です: " + operation);
            }
        });

        private static T? Optional<T>(JObject aArguments, string aName) where T : struct
            => aArguments[aName] == null || aArguments[aName].Type == JTokenType.Null ? null : aArguments[aName].ToObject<T>();

        // 矩形を記録し直すため、Repaint を予約ではなくその場で処理させる。RepaintImmediately は非公開APIのためリフレクションで呼び、
        // 見つからなければ予約だけ行う（戻り値 false。矩形は次の描画まで古いまま）
        private static bool Refresh(PPUnitCreatorWindow aWindow)
        {
            aWindow.Repaint();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var method = typeof(EditorWindow).GetMethod("RepaintImmediately", flags, null, Type.EmptyTypes, null);
            if (method != null) { method.Invoke(aWindow, null); return true; }
            var parent = typeof(EditorWindow).GetField("m_Parent", flags)?.GetValue(aWindow);
            method = parent?.GetType().GetMethod("RepaintImmediately", flags, null, Type.EmptyTypes, null);
            if (method == null) return false;
            method.Invoke(parent, null);
            return true;
        }

        // SendEvent のマウス座標はウィンドウを載せたホストビューの座標として解釈され、OnGUI にはウィンドウ内容の座標へ換算されて届く
        // その差（ドックのタブ帯・枠の分）を次の順で求める
        // 1. estimated: Repaint 時に記録したウィンドウ内容の左上（GUIToScreenPoint(0,0)）− ホストビューの screenPosition
        // 2. measured: 推定値をもとにウィンドウ内容の中ほどへ MouseMove を1回送り、OnGUI に届いた座標との差を取る（ドッキング・フロートどちらでも実際の換算と一致する）
        // MouseMove はホバー表示の更新にしか使われず、下書きの内容・更新番号は変えない
        private static (Vector2 offset, string source) ViewOffset(PPUnitCreatorWindow aWindow)
        {
            if (PPUnitCreatorWindow.PPWindowAccess.ScreenOrigin(aWindow) == null) Refresh(aWindow);
            var estimate = Vector2.zero;
            var source = "none";
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var parent = typeof(EditorWindow).GetField("m_Parent", flags)?.GetValue(aWindow);
            var origin = PPUnitCreatorWindow.PPWindowAccess.ScreenOrigin(aWindow);
            if (origin != null && parent?.GetType().GetProperty("screenPosition", flags)?.GetValue(parent) is Rect view)
            {
                estimate = origin.Value - view.position;
                source = "estimated";
            }
            var probe = estimate + new Vector2(aWindow.position.width / 2, Mathf.Min(60, aWindow.position.height / 2));
            PPUnitCreatorWindow.PPWindowAccess.ClearLastMousePosition(aWindow);
            aWindow.SendEvent(new Event { type = EventType.MouseMove, mousePosition = probe });
            var received = PPUnitCreatorWindow.PPWindowAccess.LastMousePosition(aWindow);
            return received != null ? (probe - received.Value, "measured") : (estimate, source);
        }

        private static JObject OffsetData((Vector2 offset, string source) aOffset)
            => new() { ["x"] = aOffset.offset.x, ["y"] = aOffset.offset.y, ["source"] = aOffset.source };

        private static int? Revision(PPUnitCreatorWindow aWindow)
        {
            var id = PPUnitCreatorWindow.PPWindowAccess.DraftId(aWindow);
            return string.IsNullOrEmpty(id) ? null : PPUnitDraftStore.Load(id).Revision;
        }

        private static JObject State(PPUnitCreatorWindow aWindow)
        {
            var id = PPUnitCreatorWindow.PPWindowAccess.DraftId(aWindow);
            if (string.IsNullOrEmpty(id)) throw new InvalidOperationException("ウィンドウがまだ描画されていません。setのfocusかrepaintの後に再実行してください。");
            var draft = PPUnitDraftStore.Load(id);
            var tab = PPUnitCreatorWindow.PPWindowAccess.Tab(aWindow);
            var view = PPUnitCreatorWindow.PPWindowAccess.ParameterView(aWindow);
            var stat = Mathf.Clamp(PPUnitCreatorWindow.PPWindowAccess.RankingStat(aWindow), 0, PPUnitStatComparer.StatCount - 1);
            var scroll = PPUnitCreatorWindow.PPWindowAccess.BodyScroll(aWindow);
            var rects = PPUnitCreatorWindow.PPWindowAccess.Rects(aWindow);
            var rectData = new JObject();
            foreach (var key in RectKeys)
                rectData[key] = rects.TryGetValue(key, out var rect) ? RectData(rect) : JValue.CreateNull();
            return new JObject
            {
                ["draftId"] = id, ["revision"] = draft.Revision, ["mode"] = draft.IsEditing ? "edit" : "create",
                ["tab"] = new JObject { ["index"] = tab, ["name"] = PPUnitCreatorWindow.PPWindowAccess.TabNames[tab] },
                ["parameterView"] = new JObject { ["index"] = view, ["name"] = PPUnitCreatorWindow.PPWindowAccess.ParameterViews[view] },
                ["previewLevel"] = draft.PreviewLevel, ["maxLevel"] = draft.MaxLevel,
                ["compareLevel"] = Mathf.Clamp(PPUnitCreatorWindow.PPWindowAccess.CompareLevel(aWindow), 1, draft.MaxLevel),
                ["rankingLevel"] = Mathf.Clamp(PPUnitCreatorWindow.PPWindowAccess.RankingLevel(aWindow), 1, draft.MaxLevel),
                ["rankingStat"] = new JObject { ["index"] = stat, ["key"] = PPUnitStatComparer.StatKeys[stat], ["name"] = PPUnitCreationDraft.StatNames[stat] },
                ["extraCollapsed"] = PPUnitCreatorWindow.PPWindowAccess.ExtraCollapsed(aWindow),
                ["series"] = new JArray(PPUnitCreatorWindow.PPWindowAccess.Series(aWindow)),
                ["previewPending"] = PPUnitCreatorWindow.PPWindowAccess.PreviewPending(aWindow),
                ["position"] = RectData(aWindow.position), ["viewOffset"] = OffsetData(ViewOffset(aWindow)), ["bodyScroll"] = new JObject { ["x"] = scroll.x, ["y"] = scroll.y },
                ["rects"] = rectData,
            };
        }

        private static JObject RectData(Rect aRect) => new() { ["x"] = aRect.x, ["y"] = aRect.y, ["width"] = aRect.width, ["height"] = aRect.height };
    }

    public static class PPUnitCreatorAdapter
    {
        // 詳細データのスキーマは設定対象と共に返し、推測によるパス指定を避ける
        public static JObject PatchSchema => new()
        {
            ["type"] = "object", ["additionalProperties"] = false,
            ["properties"] = new JObject
            {
                ["unitId"] = new JObject { ["type"] = "string" }, ["displayName"] = new JObject { ["type"] = "string" },
                ["folder"] = new JObject { ["type"] = "string" }, ["maxLevel"] = new JObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 100000 },
                ["previewLevel"] = new JObject { ["type"] = "integer" }, ["newAI"] = new JObject { ["type"] = "boolean" },
                ["typeAttribute"] = new JObject { ["type"] = "string", ["enum"] = new JArray(Enum.GetNames(typeof(PPTypeAttribute))) },
                ["icon"] = ReferenceSchema(), ["portrait"] = ReferenceSchema("立ち絵のSprite"), ["aiProfile"] = ReferenceSchema(),
                ["unitCatalog"] = ReferenceSchema(), ["visualCatalog"] = ReferenceSchema(), ["skillCatalog"] = ReferenceSchema(),
                ["compareTarget"] = ReferenceSchema("比較ビューとcompareの既定の比較対象PPUnitDefinition"),
                ["scales"] = new JObject { ["type"] = "array", ["minItems"] = 4, ["maxItems"] = 5, ["description"] = "棒グラフの基準値。" + StatLegend + "の順。4要素ならきようさは変更しない", ["items"] = new JObject { ["type"] = "number", ["exclusiveMinimum"] = 0 } },
                ["extra"] = new JObject { ["type"] = "object", ["description"] = "AttackCost, ActionCount, SkillGaugeMax, CoinGaugeMax" },
                ["stats"] = new JObject { ["type"] = "array", ["description"] = "各要素: index(0=HP,1=攻撃,2=防御,3=速度), initial, final, template(線形/早熟/晩成/S字), curve({keys:[{time,value,inTangent,outTangent,inWeight,outWeight,weightedMode}],preWrapMode,postWrapMode})。templateとcurveは同時指定不可。" },
                ["skills"] = new JObject { ["type"] = "array", ["description"] = "一覧を置換。各要素は{reference:{guid,localFileId}}または{new:{mSkillId:...,mDisplayName:...,mSkillEffects:[{type:完全型名,properties:{...}}]}}。get結果のpropertiesはそのままnewへ渡せる。" },
            },
        };

        internal static JObject ReferenceSchema(string aDescription = null)
        {
            var schema = new JObject
            {
                ["type"] = new JArray("object", "null"),
                ["properties"] = new JObject { ["guid"] = new JObject { ["type"] = "string" }, ["localFileId"] = new JObject { ["type"] = "integer" } },
                ["required"] = new JArray("guid", "localFileId"),
            };
            if (aDescription != null) schema["description"] = aDescription;
            return schema;
        }

        // 能力のindexとキー名の対応（0=hp(HP) …）
        public static string StatLegend => string.Join(", ", PPUnitStatComparer.StatKeys.Select((aKey, aIndex) => aIndex + "=" + aKey + "(" + PPUnitCreationDraft.StatNames[aIndex] + ")"));

        // 能力はindex（0〜4）またはキー名で受け付ける
        public static int StatIndex(JToken aToken)
        {
            if (aToken == null || aToken.Type == JTokenType.Null) throw new ArgumentException("statが必要です: " + StatLegend);
            var index = aToken.Type == JTokenType.Integer ? aToken.Value<int>() : Array.IndexOf(PPUnitStatComparer.StatKeys, aToken.Value<string>());
            if (index < 0 || index >= PPUnitStatComparer.StatCount) throw new ArgumentException("不正なstatです: " + StatLegend);
            return index;
        }

        public static JObject StatValues(float[] aValues)
        {
            var result = new JObject();
            for (var i = 0; i < aValues.Length; i++) result[PPUnitStatComparer.StatKeys[i]] = aValues[i];
            return result;
        }

        public static JObject Validation(PPUnitCreationDraft aDraft)
        {
            var result = PPUnitCreationValidator.Validate(aDraft);
            return new JObject { ["revision"] = aDraft.Revision, ["errors"] = new JArray(result.Errors), ["warnings"] = new JArray(result.Warnings), ["paths"] = new JArray(result.Paths) };
        }

        // GUIDとlocalFileIdの組でサブアセットを厳密に解決する
        public static T Resolve<T>(JToken aToken) where T : UnityEngine.Object
        {
            if (aToken == null || aToken.Type == JTokenType.Null) return null;
            var guid = aToken.Value<string>("guid");
            if (string.IsNullOrEmpty(guid) || aToken["localFileId"] == null) throw new ArgumentException("参照にはguidとlocalFileIdが必要です。");
            var id = aToken.Value<long>("localFileId");
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var result = AssetDatabase.LoadAllAssetsAtPath(path).OfType<T>().FirstOrDefault(aObject =>
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(aObject, out string objectGuid, out long localId) && objectGuid == guid && localId == id);
            if (result == null) throw new ArgumentException("指定した型の参照を解決できません: " + guid + "/" + id);
            return result;
        }

        public static JToken Reference(UnityEngine.Object aObject)
        {
            if (aObject == null) return JValue.CreateNull();
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(aObject, out string guid, out long id)) return JValue.CreateNull();
            return new JObject { ["guid"] = guid, ["localFileId"] = id };
        }

        public static JObject Describe(PPUnitCreationDraft aDraft) => new()
        {
            ["draftId"] = aDraft.DraftId, ["revision"] = aDraft.Revision,
            ["mode"] = aDraft.IsEditing ? "edit" : "create",
            ["source"] = aDraft.IsEditing ? new JObject { ["reference"] = Reference(aDraft.Source), ["path"] = AssetDatabase.GUIDToAssetPath(aDraft.SourceGuid), ["visual"] = Reference(aDraft.SourceVisual) } : JValue.CreateNull(),
            ["plannedPaths"] = aDraft.IsEditing ? new JArray(PPUnitCreationValidator.EditPlan(aDraft).AllPaths) : new JArray(PPUnitCreationValidator.Validate(aDraft).Paths),
            ["lastSave"] = aDraft.SavedRevision >= 0 ? new JObject { ["requestId"] = aDraft.SavedRequestId, ["revision"] = aDraft.SavedRevision, ["paths"] = new JArray(aDraft.SavedPaths) } : JValue.CreateNull(),
            ["unitId"] = aDraft.Unit.UnitId, ["displayName"] = aDraft.Unit.DisplayName,
            ["folder"] = aDraft.Folder, ["maxLevel"] = aDraft.MaxLevel, ["previewLevel"] = aDraft.PreviewLevel,
            ["typeAttribute"] = aDraft.Unit.TypeAttribute.ToString(), ["icon"] = Reference(aDraft.Icon), ["portrait"] = Reference(aDraft.Portrait),
            ["newAI"] = aDraft.NewAI, ["aiProfile"] = Reference(aDraft.Unit.AIProfile),
            ["unitCatalog"] = Reference(aDraft.UnitCatalog), ["visualCatalog"] = Reference(aDraft.VisualCatalog), ["skillCatalog"] = Reference(aDraft.SkillCatalog),
            ["compareTarget"] = Reference(aDraft.CompareTarget),
            ["scales"] = new JArray(aDraft.Scales), ["extra"] = JObject.FromObject(aDraft.Unit.ExpandStatBlock),
            ["stats"] = new JArray(Enumerable.Range(0, 4).Select(aIndex => new JObject
            {
                ["index"] = aIndex, ["initial"] = aDraft.GetInitial(aIndex), ["final"] = aDraft.GetFinal(aIndex), ["curve"] = CurveData(aDraft.GetCurve(aIndex)),
            })),
            ["skills"] = new JArray(aDraft.Unit.Skills.Select(aSkill => aSkill is PPSkillDefinition skill && aDraft.NewSkills.Contains(skill)
                ? new JObject { ["new"] = ReadProperties(new SerializedObject(skill)) }
                : new JObject { ["reference"] = Reference(aSkill) })),
            ["createdPaths"] = new JArray(aDraft.CreatedPaths),
        };

        public static void Apply(PPUnitCreationDraft aDraft, JObject aPatch)
        {
            var allowed = (JObject)PatchSchema["properties"];
            foreach (var field in aPatch.Properties())
            {
                if (allowed[field.Name] == null) throw new ArgumentException("未知の変更項目: " + field.Name);
                var expected = allowed[field.Name]["type"];
                var actual = field.Value.Type switch
                {
                    JTokenType.Integer => "integer", JTokenType.Float => "number", JTokenType.String => "string",
                    JTokenType.Array => "array", JTokenType.Object => "object", JTokenType.Boolean => "boolean", JTokenType.Null => "null", _ => "invalid",
                };
                if (expected is JArray accepted ? !accepted.Values<string>().Contains(actual) : expected.Value<string>() != actual)
                    throw new ArgumentException("項目の型が不正です: " + field.Name);
            }
            // 編集モードではユニットIDと保存先を元アセットのまま固定する
            if (aDraft.IsEditing && aPatch["unitId"] != null && aPatch.Value<string>("unitId") != aDraft.Unit.UnitId) throw new ArgumentException("編集モードではunitIdを変更できません。");
            if (aDraft.IsEditing && aPatch["folder"] != null && aPatch.Value<string>("folder") != aDraft.Folder) throw new ArgumentException("編集モードではfolderを変更できません。");
            if (aPatch["maxLevel"] != null) aDraft.SetMaxLevel(aPatch.Value<int>("maxLevel"));
            if (aPatch["previewLevel"] != null) aDraft.PreviewLevel = aPatch.Value<int>("previewLevel");
            if (aPatch["folder"] != null) aDraft.Folder = aPatch.Value<string>("folder") ?? "";
            if (aPatch["newAI"] != null) aDraft.NewAI = aPatch.Value<bool>("newAI");
            if (aPatch["icon"] != null) aDraft.Icon = Resolve<Sprite>(aPatch["icon"]);
            if (aPatch["portrait"] != null) aDraft.Portrait = Resolve<Sprite>(aPatch["portrait"]);
            if (aPatch["unitCatalog"] != null) aDraft.UnitCatalog = Resolve<PPUnitCatalog>(aPatch["unitCatalog"]);
            if (aPatch["visualCatalog"] != null) aDraft.VisualCatalog = Resolve<PPUnitVisualCatalog>(aPatch["visualCatalog"]);
            if (aPatch["skillCatalog"] != null) aDraft.SkillCatalog = Resolve<PPSkillCatalog>(aPatch["skillCatalog"]);
            if (aPatch["compareTarget"] != null) aDraft.CompareTarget = Resolve<PPUnitDefinition>(aPatch["compareTarget"]);
            var so = new SerializedObject(aDraft.Unit);
            if (aPatch["unitId"] != null) so.FindProperty("mUnitId").stringValue = aPatch.Value<string>("unitId");
            if (aPatch["displayName"] != null) so.FindProperty("mDisplayName").stringValue = aPatch.Value<string>("displayName");
            if (aPatch["typeAttribute"] != null)
            {
                if (!Enum.TryParse<PPTypeAttribute>(aPatch.Value<string>("typeAttribute"), out var type) || !Enum.IsDefined(typeof(PPTypeAttribute), type)) throw new ArgumentException("不正な属性です。");
                so.FindProperty("mTypeAttribute").intValue = (int)type;
            }
            if (aPatch["aiProfile"] != null) so.FindProperty("mAIProfile").objectReferenceValue = Resolve<PPUnitAIProfileDefinition>(aPatch["aiProfile"]);
            if (aPatch["extra"] is JObject extra) ApplyValue(so.FindProperty("mExpandStatBlock"), extra);
            so.ApplyModifiedPropertiesWithoutUndo();
            if (aPatch["scales"] is JArray scales)
            {
                if (scales.Count < 4 || scales.Count > PPUnitCreationDraft.ChartAxisCount) throw new ArgumentException("scalesは4〜5要素です。");
                for (var i = 0; i < scales.Count; i++) aDraft.Scales[i] = scales[i].Value<float>();
            }
            if (aPatch["stats"] is JArray stats)
            {
                foreach (var entry in stats)
                {
                    if (entry["index"] == null) throw new ArgumentException("statsにはindexが必要です。");
                    var index = entry.Value<int>("index");
                    if (index < 0 || index > 3) throw new ArgumentException("stats.indexは0〜3です。");
                    var initial = entry["initial"]?.Value<float>() ?? aDraft.GetInitial(index);
                    var final = entry["final"]?.Value<float>() ?? (aDraft.MaxLevel == 1 ? initial : aDraft.GetFinal(index));
                    if (!PPUnitCreationValidator.IsFinite(initial) || !PPUnitCreationValidator.IsFinite(final) || initial < 0 || final < initial || (initial == 0 && final != 0) || (aDraft.MaxLevel == 1 && initial != final)) throw new ArgumentException("初期値と終端値の組が不正です。");
                    aDraft.SetStat(index, initial, final);
                    if (entry["template"] != null && entry["curve"] != null) throw new ArgumentException("templateとcurveは同時指定できません。");
                    if (entry["template"] != null)
                    {
                        var kind = Array.IndexOf(PPUnitGrowthTemplate.Names, entry.Value<string>("template"));
                        if (kind < 0) throw new ArgumentException("不明な成長テンプレートです。");
                        aDraft.SetCurve(index, PPUnitGrowthTemplate.Create(kind, aDraft.MaxLevel, initial > 0 ? final / initial : 1));
                    }
                    if (entry["curve"] != null) aDraft.SetCurve(index, ReadCurve(entry["curve"]));
                }
            }
            if (aPatch["skills"] is JArray skills)
            {
                if (skills.Count > 256) throw new ArgumentException("スキルは256件以下です。");
                aDraft.Unit.Skills.Clear();
                foreach (var skill in aDraft.NewSkills) UnityEngine.Object.DestroyImmediate(skill);
                aDraft.NewSkills.Clear();
                foreach (var entry in skills)
                {
                    if (entry["reference"] != null && entry["new"] != null) throw new ArgumentException("スキルはreferenceまたはnewを指定してください。");
                    if (entry["new"] is JObject properties)
                    {
                        var skill = aDraft.AddNewSkill();
                        var skillSo = new SerializedObject(skill);
                        foreach (var property in properties.Properties())
                        {
                            if (property.Name == "m_Script") throw new ArgumentException("スクリプトの変更はできません。");
                            ApplyValue(skillSo.FindProperty(property.Name) ?? throw new ArgumentException("不明なスキル項目: " + property.Name), property.Value);
                        }
                        skillSo.ApplyModifiedPropertiesWithoutUndo();
                    }
                    else aDraft.Unit.Skills.Add(Resolve<PPSkillDefinition>(entry["reference"] ?? throw new ArgumentException("スキル参照が必要です。")));
                }
            }
        }

        // スキル効果も既存SerializedPropertyへ設定し、ランタイム生成は行わない
        private static void ApplyValue(SerializedProperty aProperty, JToken aValue)
        {
            if (aProperty.isArray && aProperty.propertyType != SerializedPropertyType.String)
            {
                var array = aValue as JArray ?? throw new ArgumentException("配列が必要です: " + aProperty.propertyPath);
                if (array.Count > 1024) throw new ArgumentException("配列が大きすぎます。");
                aProperty.arraySize = array.Count;
                for (var i = 0; i < array.Count; i++) ApplyValue(aProperty.GetArrayElementAtIndex(i), array[i]);
            }
            else if (aProperty.propertyType == SerializedPropertyType.ManagedReference)
            {
                if (aValue.Type == JTokenType.Null) { aProperty.managedReferenceValue = null; return; }
                var typeName = aValue.Value<string>("type");
                var fieldType = aProperty.managedReferenceFieldTypename.Split(' ');
                var baseType = Type.GetType(fieldType[1] + ", " + fieldType[0]);
                var type = baseType == null ? null : TypeCache.GetTypesDerivedFrom(baseType).FirstOrDefault(aType => aType.FullName == typeName && !aType.IsAbstract && aType.IsSerializable && aType.Namespace == "PPCore");
                if (type == null) throw new ArgumentException("不明なスキル効果型: " + typeName);
                aProperty.managedReferenceValue = Activator.CreateInstance(type);
                foreach (var child in ((JObject)aValue["properties"]).Properties())
                    ApplyValue(aProperty.FindPropertyRelative(child.Name) ?? throw new ArgumentException("不明な効果項目: " + child.Name), child.Value);
            }
            else if (aProperty.propertyType == SerializedPropertyType.Generic)
            {
                foreach (var child in ((JObject)aValue).Properties())
                    ApplyValue(aProperty.FindPropertyRelative(child.Name) ?? throw new ArgumentException("不明な項目: " + child.Name), child.Value);
            }
            else if (aProperty.propertyType == SerializedPropertyType.ObjectReference)
            {
                var value = Resolve<UnityEngine.Object>(aValue);
                if (value != null && aProperty.type.StartsWith("PPtr<", StringComparison.Ordinal))
                {
                    var expectedName = aProperty.type.Substring(5).TrimEnd('>').TrimStart('$');
                    var actualType = value.GetType();
                    while (actualType != null && actualType.Name != expectedName) actualType = actualType.BaseType;
                    if (actualType == null) throw new ArgumentException("参照の型が一致しません: " + aProperty.propertyPath);
                }
                // SerializedPropertyに期待型の検査を委ね、型不一致で無視された場合もエラーにする
                aProperty.objectReferenceValue = value;
                if (aProperty.objectReferenceValue != value) throw new ArgumentException("参照の型が一致しません。");
            }
            else MCPArgumentConverter.ApplyToSerializedProperty(aProperty, aValue);
        }

        private static JObject ReadProperties(SerializedObject aObject)
        {
            var result = new JObject();
            var property = aObject.GetIterator();
            if (property.NextVisible(true)) do
            {
                if (property.name != "m_Script") result[property.name] = ReadValue(property);
            } while (property.NextVisible(false));
            return result;
        }

        private static JToken ReadValue(SerializedProperty aProperty)
        {
            if (aProperty.isArray && aProperty.propertyType != SerializedPropertyType.String)
                return new JArray(Enumerable.Range(0, aProperty.arraySize).Select(aIndex => ReadValue(aProperty.GetArrayElementAtIndex(aIndex))));
            switch (aProperty.propertyType)
            {
                case SerializedPropertyType.String: return aProperty.stringValue;
                case SerializedPropertyType.Float: return aProperty.floatValue;
                case SerializedPropertyType.Integer: return aProperty.intValue;
                case SerializedPropertyType.Boolean: return aProperty.boolValue;
                case SerializedPropertyType.Enum: return aProperty.intValue;
                case SerializedPropertyType.ObjectReference: return Reference(aProperty.objectReferenceValue);
                case SerializedPropertyType.ManagedReference:
                case SerializedPropertyType.Generic:
                    if (aProperty.propertyType == SerializedPropertyType.ManagedReference && aProperty.managedReferenceValue == null) return JValue.CreateNull();
                    var properties = new JObject();
                    var child = aProperty.Copy();
                    var end = child.GetEndProperty();
                    if (child.NextVisible(true)) do
                    {
                        if (SerializedProperty.EqualContents(child, end)) break;
                        properties[child.name] = ReadValue(child);
                    } while (child.NextVisible(false));
                    return aProperty.propertyType == SerializedPropertyType.ManagedReference
                        ? new JObject { ["type"] = aProperty.managedReferenceValue.GetType().FullName, ["properties"] = properties } : properties;
                default: throw new ArgumentException("取得非対応のスキル項目: " + aProperty.propertyPath + " / " + aProperty.propertyType);
            }
        }

        private static JObject CurveData(AnimationCurve aCurve) => new()
        {
            ["preWrapMode"] = (int)aCurve.preWrapMode, ["postWrapMode"] = (int)aCurve.postWrapMode,
            ["keys"] = new JArray(aCurve.keys.Select(aKey => new JObject
            {
                ["time"] = aKey.time, ["value"] = aKey.value, ["inTangent"] = aKey.inTangent, ["outTangent"] = aKey.outTangent,
                ["inWeight"] = aKey.inWeight, ["outWeight"] = aKey.outWeight, ["weightedMode"] = (int)aKey.weightedMode,
            })),
        };

        private static AnimationCurve ReadCurve(JToken aData)
        {
            var keys = aData["keys"] as JArray ?? throw new ArgumentException("curve.keysが必要です。");
            if (keys.Count > 1024) throw new ArgumentException("曲線キーは1024件以下です。");
            return new AnimationCurve(keys.Select(aKey => new Keyframe(aKey.Value<float>("time"), aKey.Value<float>("value"), aKey["inTangent"]?.Value<float>() ?? 0, aKey["outTangent"]?.Value<float>() ?? 0)
            {
                inWeight = aKey["inWeight"]?.Value<float>() ?? 0, outWeight = aKey["outWeight"]?.Value<float>() ?? 0,
                weightedMode = (WeightedMode)(aKey["weightedMode"]?.Value<int>() ?? 0),
            }).ToArray())
            {
                preWrapMode = (WrapMode)(aData["preWrapMode"]?.Value<int>() ?? 0), postWrapMode = (WrapMode)(aData["postWrapMode"]?.Value<int>() ?? 0),
            };
        }

        public static void DestroyDraft(PPUnitCreationDraft aDraft)
        {
            foreach (var skill in aDraft.NewSkills) if (skill != null) UnityEngine.Object.DestroyImmediate(skill);
            if (aDraft.Unit != null) UnityEngine.Object.DestroyImmediate(aDraft.Unit);
            UnityEngine.Object.DestroyImmediate(aDraft);
        }
    }
}
