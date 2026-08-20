/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPBridgeWindow.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief 接続状態・TODO進行状況・実行ログを表示する可視化パネル
 * TODOの手動編集・実行の中断/再開など、実行中の計画そのものへの介入は行わない(SPEC anti-goal)。
 * 一方でツール利用モードの切替・新規作成はSPECで明示的に許可された操作面のため、
 * このウィンドウ上のUIから行えるようにする。
 * UI Toolkit(UIElements)ベースで実装しており、各セクションは「カード構築(Build〜)」と
 * 「差分更新(Refresh〜)」のペアで構成する(IMGUI時代のRepaint()全体再描画をやめ、
 * イベント発火のたびに該当セクションだけを更新する)。
 * 各カードの主要コンテンツ領域はドラッグで縦幅を変更でき(AddResizeHandle)、
 * カード先頭のハンドルでカード自体の並び順も入れ替えられる(AddReorderHandle)。
 * どちらもEditorPrefsへ永続化し、Editor再起動をまたいで復元する。
 * ウィンドウ全体はScrollViewで包み、カード合計高がウィンドウ高を超えてもスクロールできる。
 * カード外最上部のタブでライト/ダークを切り替えられる(Unity Editor自体のスキルとは独立。
 * USSのカスタムプロパティ(--mcp-*)で配色を差し替える。SetTheme/BuildThemeTabBar参照)
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Linq;
using MCPBridge.Editor.Execution;
using MCPBridge.Editor.Mode;
using MCPBridge.Editor.Server;
using MCPBridge.Editor.Tools;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MCPBridge.Editor.Window
{
    public class MCPBridgeWindow : EditorWindow
    {
        private const string UssPath = "Assets/Scripts/MCPBridge/Editor/Window/MCPBridgeWindow.uss";

        private const float DefaultCompactContentHeight = 70f;
        private const float DefaultListContentHeight = 120f;
        private const float MinContentHeight = 32f;

        // 接続状態・実行進行・モード・システムログの変化はイベント購読で該当セクションだけ更新する。
        // それとは別に「最終リクエスト受信時刻」表示だけはイベントを持たないため、
        // 全体を再描画するのではなく低頻度(1秒間隔)のポーリングで接続状態セクションのみ更新する
        private const double PeriodicRefreshIntervalSeconds = 1.0;
        private double mNextPeriodicRefreshTime;

        private VisualElement mConnectionBadge;

        private PopupField<string> mModePopup;
        private Label mModeCountLabel;
        private VisualElement mModeToolListContainer;

        private ProgressBar mTodoProgressBar;
        private Label mTodoStatusLabel;
        private VisualElement mTodoListContainer;

        private ListView mExecutionLogList;
        private ListView mToolCallLogList;
        private ListView mSystemLogList;

        private Button mLightThemeTab;
        private Button mDarkThemeTab;

        [MenuItem("Window/MCP Bridge")]
        public static void Open()
        {
            var window = GetWindow<MCPBridgeWindow>();
            window.titleContent = new GUIContent("MCP Bridge");
            window.minSize = new Vector2(360, 420);
            window.Show();
        }

        private void OnEnable()
        {
            MCPHttpServer.OnConnectionStateChanged += RefreshConnectionSection;
            PlanExecutionState.OnChanged += RefreshTodoSection;
            PlanExecutionState.OnChanged += RefreshExecutionLogSection;
            MCPModeRegistry.OnModeChanged += RefreshModeSection;
            MCPToolCallLog.OnChanged += RefreshToolCallLogSection;
            MCPSystemEventLog.OnChanged += RefreshSystemLogSection;
            EditorApplication.update += PeriodicRefresh;
        }

        private void OnDisable()
        {
            MCPHttpServer.OnConnectionStateChanged -= RefreshConnectionSection;
            PlanExecutionState.OnChanged -= RefreshTodoSection;
            PlanExecutionState.OnChanged -= RefreshExecutionLogSection;
            MCPModeRegistry.OnModeChanged -= RefreshModeSection;
            MCPToolCallLog.OnChanged -= RefreshToolCallLogSection;
            MCPSystemEventLog.OnChanged -= RefreshSystemLogSection;
            EditorApplication.update -= PeriodicRefresh;
        }

        private void PeriodicRefresh()
        {
            if (EditorApplication.timeSinceStartup < mNextPeriodicRefreshTime)
            {
                return;
            }
            mNextPeriodicRefreshTime = EditorApplication.timeSinceStartup + PeriodicRefreshIntervalSeconds;
            RefreshConnectionSection();
        }

        public void CreateGUI()
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }
            rootVisualElement.AddToClassList("mcp-root");

            // ライト/ダークはUnity Editor自体のスキルとは独立したこのウィンドウ専用のテーマ。
            // カード外の最上部にタブを置き、選択はEditorPrefsで永続化する
            rootVisualElement.Add(BuildThemeTabBar());

            // カード合計高がウィンドウの表示領域を超えてもスクロールできるよう、
            // 全カードをScrollViewの中に配置する
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.AddToClassList("mcp-root-scroll");
            rootVisualElement.Add(scrollView);

            // カードの並び順はEditorPrefsに保存し、次回起動時も復元する。
            // 保存された順序に無いID(将来カードが追加された場合)は末尾に補う
            var sections = new (string Id, VisualElement Card)[]
            {
                ("Connection", BuildConnectionSection()),
                ("Mode", BuildModeSection()),
                ("Todo", BuildTodoSection()),
                ("ExecutionLog", BuildExecutionLogSection()),
                ("ToolCallLog", BuildToolCallLogSection()),
                ("SystemLog", BuildSystemLogSection()),
            };
            foreach (var (id, card) in ApplySavedOrder(sections))
            {
                card.name = id;
                scrollView.Add(card);
                AddReorderHandle(scrollView, card);
            }

            RefreshConnectionSection();
            RefreshModeSection();
            RefreshTodoSection();
            RefreshExecutionLogSection();
            RefreshToolCallLogSection();
            RefreshSystemLogSection();

            var savedTheme = EditorPrefs.GetString(ThemePrefsKey, EditorGUIUtility.isProSkin ? "Dark" : "Light");
            SetTheme(savedTheme);
        }

        // ===== テーマ切替(ライト/ダーク) =====

        private const string ThemePrefsKey = "MCPBridge.Window.Theme";

        private VisualElement BuildThemeTabBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("mcp-theme-tab-bar");

            mLightThemeTab = new Button(() => SetTheme("Light")) { text = "ライト" };
            mLightThemeTab.AddToClassList("mcp-theme-tab");
            bar.Add(mLightThemeTab);

            mDarkThemeTab = new Button(() => SetTheme("Dark")) { text = "ダーク" };
            mDarkThemeTab.AddToClassList("mcp-theme-tab");
            bar.Add(mDarkThemeTab);

            return bar;
        }

        private void SetTheme(string aTheme)
        {
            var isDark = aTheme == "Dark";
            rootVisualElement.RemoveFromClassList("mcp-theme-light");
            rootVisualElement.RemoveFromClassList("mcp-theme-dark");
            rootVisualElement.AddToClassList(isDark ? "mcp-theme-dark" : "mcp-theme-light");

            mLightThemeTab.EnableInClassList("mcp-theme-tab--active", !isDark);
            mDarkThemeTab.EnableInClassList("mcp-theme-tab--active", isDark);

            EditorPrefs.SetString(ThemePrefsKey, isDark ? "Dark" : "Light");
        }

        // カード共通の見出しラベルを追加する
        private static VisualElement CreateCard(string aTitle)
        {
            var card = new VisualElement();
            card.AddToClassList("mcp-card");
            var title = new Label(aTitle);
            title.AddToClassList("mcp-card__title");
            card.Add(title);
            return card;
        }

        // ログ系カード用。見出しの右上に「クリア」ボタンを添えたタイトル行を作る
        private static VisualElement CreateLogCard(string aTitle, Action aOnClear)
        {
            var card = new VisualElement();
            card.AddToClassList("mcp-card");

            var titleRow = new VisualElement();
            titleRow.AddToClassList("mcp-card__title-row");

            var title = new Label(aTitle);
            title.AddToClassList("mcp-card__title");
            titleRow.Add(title);

            var clearButton = new Button(aOnClear) { text = "クリア" };
            clearButton.AddToClassList("mcp-card__clear-button");
            titleRow.Add(clearButton);

            card.Add(titleRow);
            return card;
        }

        // カードごとの縦幅をEditorPrefsへ永続化する際のキー接頭辞(Editor再起動をまたいで保持する)
        private const string HeightPrefsKeyPrefix = "MCPBridge.Window.Height.";

        // カードの並び順をEditorPrefsへ永続化する際のキー(カンマ区切りのID列)
        private const string OrderPrefsKey = "MCPBridge.Window.CardOrder";

        // EditorPrefsに保存された並び順をaSectionsへ適用する。保存が無い場合はaSectionsのまま返す。
        // 保存された順序に存在するがaSectionsに無いID(旧バージョンの残骸等)は無視し、
        // 逆にaSectionsにあるが保存順序に無いID(将来追加されたカード)は末尾に補う
        private static IEnumerable<(string Id, VisualElement Card)> ApplySavedOrder(
            IReadOnlyList<(string Id, VisualElement Card)> aSections)
        {
            var savedOrder = EditorPrefs.GetString(OrderPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(savedOrder))
            {
                return aSections;
            }

            var byId = aSections.ToDictionary(s => s.Id);
            var ordered = new List<(string, VisualElement)>();
            foreach (var id in savedOrder.Split(','))
            {
                if (byId.Remove(id, out var section))
                {
                    ordered.Add(section);
                }
            }
            ordered.AddRange(byId.Values);
            return ordered;
        }

        // ドラッグでカード自体の並び順を入れ替えられるハンドルをaCardの先頭(タイトルの上)へ追加する。
        // ドラッグ中はポインタのY座標と各カードの中心Yを比較して並べ替え、ドロップ時に
        // 現在の並び順をEditorPrefsへ保存する
        private static void AddReorderHandle(ScrollView aScrollView, VisualElement aCard)
        {
            var handle = new VisualElement();
            handle.AddToClassList("mcp-reorder-handle");
            aCard.Insert(0, handle);

            var dragging = false;

            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                dragging = true;
                handle.CapturePointer(evt.pointerId);
            });
            handle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!dragging)
                {
                    return;
                }

                var container = aScrollView.contentContainer;
                var pointerY = container.WorldToLocal(evt.position).y;

                // 自分以外のカードのうち、中心Yがポインタより上にあるものの数 = 挿入先インデックス
                var targetIndex = 0;
                foreach (var sibling in container.Children())
                {
                    if (sibling == aCard)
                    {
                        continue;
                    }
                    var siblingMidY = sibling.layout.y + sibling.layout.height * 0.5f;
                    if (pointerY > siblingMidY)
                    {
                        targetIndex++;
                    }
                }

                if (container.IndexOf(aCard) != targetIndex)
                {
                    container.Remove(aCard);
                    container.Insert(Mathf.Clamp(targetIndex, 0, container.childCount), aCard);
                }
            });
            handle.RegisterCallback<PointerUpEvent>(evt =>
            {
                dragging = false;
                handle.ReleasePointer(evt.pointerId);

                var order = aScrollView.contentContainer.Children()
                    .Select(c => c.name)
                    .Where(n => !string.IsNullOrEmpty(n));
                EditorPrefs.SetString(OrderPrefsKey, string.Join(",", order));
            });
        }

        // aTargetの高さをドラッグで変更できるハンドルをaCardの末尾へ追加する。
        // aTarget自体はScrollView/ListViewいずれでもよい(どちらもstyle.heightで高さを制御できる)。
        // aPrefsKeySuffixで指定した高さをEditorPrefsに保存し、次回起動時も復元する
        private static void AddResizeHandle(VisualElement aCard, VisualElement aTarget, float aInitialHeight, string aPrefsKeySuffix)
        {
            var prefsKey = HeightPrefsKeyPrefix + aPrefsKeySuffix;
            var savedHeight = EditorPrefs.GetFloat(prefsKey, aInitialHeight);
            aTarget.style.height = Mathf.Max(MinContentHeight, savedHeight);

            var handle = new VisualElement();
            handle.AddToClassList("mcp-resize-handle");
            aCard.Add(handle);

            var startHeight = 0f;
            var startPointerY = 0f;
            var dragging = false;

            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                dragging = true;
                startHeight = aTarget.resolvedStyle.height;
                startPointerY = evt.position.y;
                handle.CapturePointer(evt.pointerId);
            });
            handle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!dragging)
                {
                    return;
                }
                var newHeight = Mathf.Max(MinContentHeight, startHeight + (evt.position.y - startPointerY));
                aTarget.style.height = newHeight;
            });
            handle.RegisterCallback<PointerUpEvent>(evt =>
            {
                dragging = false;
                handle.ReleasePointer(evt.pointerId);
                EditorPrefs.SetFloat(prefsKey, aTarget.resolvedStyle.height);
            });
        }

        // ===== 接続情報 =====

        private Label mConnectionStatusValue;
        private Label mConnectionServerValue;
        private Label mConnectionClientValue;
        private Label mConnectionLastRequestValue;

        // 「ラベル: 値」の1行を追加する。接続情報のように項目数が多いセクションで
        // 1行に詰め込まず、項目ごとに見やすく縦に並べるための共通部品
        private static Label AddInfoRow(VisualElement aParent, string aFieldLabel)
        {
            var row = new VisualElement();
            row.AddToClassList("mcp-info-row");

            var fieldLabel = new Label(aFieldLabel);
            fieldLabel.AddToClassList("mcp-info-row__label");
            row.Add(fieldLabel);

            var value = new Label();
            value.AddToClassList("mcp-info-row__value");
            row.Add(value);

            aParent.Add(row);
            return value;
        }

        private VisualElement BuildConnectionSection()
        {
            var card = CreateCard("接続情報");

            var content = new ScrollView(ScrollViewMode.Vertical);
            content.AddToClassList("mcp-card__content");
            card.Add(content);

            var statusRow = new VisualElement();
            statusRow.AddToClassList("mcp-info-row");
            var statusFieldLabel = new Label("ステータス");
            statusFieldLabel.AddToClassList("mcp-info-row__label");
            statusRow.Add(statusFieldLabel);
            mConnectionBadge = new VisualElement();
            mConnectionBadge.AddToClassList("mcp-badge");
            statusRow.Add(mConnectionBadge);
            mConnectionStatusValue = new Label();
            mConnectionStatusValue.AddToClassList("mcp-info-row__value");
            statusRow.Add(mConnectionStatusValue);
            content.Add(statusRow);

            mConnectionServerValue = AddInfoRow(content, "サーバー情報");
            mConnectionClientValue = AddInfoRow(content, "クライアント情報");
            mConnectionLastRequestValue = AddInfoRow(content, "最終リクエスト");

            var restartButton = new Button(MCPHttpServer.Restart) { text = "MCPサーバーを再起動" };
            restartButton.AddToClassList("mcp-button");
            content.Add(restartButton);

            AddResizeHandle(card, content, DefaultCompactContentHeight, "Connection");
            return card;
        }

        private void RefreshConnectionSection()
        {
            if (mConnectionBadge == null)
            {
                return;
            }

            mConnectionBadge.RemoveFromClassList("mcp-badge--ok");
            mConnectionBadge.RemoveFromClassList("mcp-badge--waiting");
            mConnectionBadge.RemoveFromClassList("mcp-badge--error");
            mConnectionBadge.RemoveFromClassList("mcp-badge--idle");

            var lastRequest = MCPHttpServer.LastRequestReceivedAt;
            mConnectionLastRequestValue.text = lastRequest.HasValue ? lastRequest.Value.ToString("HH:mm:ss") : "-";
            mConnectionServerValue.text = MCPHttpServer.State == MCPConnectionState.Stopped
                ? "-"
                : $"{MCPProtocolHandler.ServerName} v{MCPProtocolHandler.ServerVersion}(http://localhost:{MCPHttpServer.Port})";

            switch (MCPHttpServer.State)
            {
                case MCPConnectionState.Listening:
                    if (lastRequest.HasValue)
                    {
                        // initializeハンドシェイクを受け取ったクライアント情報を「接続済み」として表示する。
                        // ステートレスなHTTPサーバーのため厳密な「現在接続中」判定はできず、
                        // 直近にリクエストを送ってきたクライアントの情報として扱う
                        mConnectionBadge.AddToClassList("mcp-badge--ok");
                        mConnectionStatusValue.text = "接続済み";
                        mConnectionClientValue.text = !string.IsNullOrEmpty(MCPHttpServer.ConnectedClientName)
                            ? $"{MCPHttpServer.ConnectedClientName} {MCPHttpServer.ConnectedClientVersion}".Trim()
                            : "情報なし";
                    }
                    else
                    {
                        mConnectionBadge.AddToClassList("mcp-badge--waiting");
                        mConnectionStatusValue.text = "接続待ち";
                        mConnectionClientValue.text = "-";
                    }
                    break;
                case MCPConnectionState.Error:
                    mConnectionBadge.AddToClassList("mcp-badge--error");
                    mConnectionStatusValue.text = $"エラー: {MCPHttpServer.LastErrorMessage}";
                    mConnectionClientValue.text = "-";
                    break;
                default:
                    mConnectionBadge.AddToClassList("mcp-badge--idle");
                    mConnectionStatusValue.text = "未接続";
                    mConnectionClientValue.text = "-";
                    break;
            }
        }

        // ===== モード =====

        private VisualElement BuildModeSection()
        {
            var card = CreateCard("モード");

            // モード選択・新規作成ボタンはリサイズ/スクロール対象に含めず、カード上部に固定表示する
            var row = new VisualElement();
            row.AddToClassList("mcp-row");

            var modeNames = MCPModeRegistry.Modes.Select(m => m.Name).ToList();
            mModePopup = new PopupField<string>(modeNames, MCPModeRegistry.CurrentMode.Name);
            mModePopup.AddToClassList("mcp-mode-popup");
            mModePopup.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue != MCPModeRegistry.CurrentMode.Name)
                {
                    MCPModeRegistry.SwitchTo(evt.newValue);
                }
            });
            row.Add(mModePopup);

            var createButton = new Button(() => MCPModeCreateWindow.Open(MCPToolRegistry.AllToolNames))
            {
                text = "新規モード作成",
            };
            createButton.AddToClassList("mcp-button");
            row.Add(createButton);
            card.Add(row);

            var content = new ScrollView(ScrollViewMode.Vertical);
            content.AddToClassList("mcp-card__content");
            card.Add(content);

            mModeCountLabel = new Label();
            mModeCountLabel.AddToClassList("mcp-sub-label");
            content.Add(mModeCountLabel);

            mModeToolListContainer = new VisualElement();
            mModeToolListContainer.AddToClassList("mcp-tool-list");
            content.Add(mModeToolListContainer);

            AddResizeHandle(card, content, DefaultCompactContentHeight, "Mode");
            return card;
        }

        private void RefreshModeSection()
        {
            if (mModePopup == null)
            {
                return;
            }

            var modeNames = MCPModeRegistry.Modes.Select(m => m.Name).ToList();
            mModePopup.choices = modeNames;
            mModePopup.SetValueWithoutNotify(MCPModeRegistry.CurrentMode.Name);
            mModeCountLabel.text =
                $"許可ツール数: {MCPModeRegistry.CurrentMode.AllowedToolNames.Count} / {MCPToolRegistry.AllToolNames.Count()}";

            mModeToolListContainer.Clear();
            foreach (var toolName in MCPModeRegistry.CurrentMode.AllowedToolNames.OrderBy(n => n))
            {
                var item = new Label(toolName);
                item.AddToClassList("mcp-tool-list-item");
                mModeToolListContainer.Add(item);
            }
        }

        // ===== キュー(TODOリスト/実行進行状況) =====

        // ステップの状態(完了/実行中/待機/エラー)に対応するUnity組み込みアイコン名
        private static readonly Dictionary<string, string> sStepIconNames = new()
        {
            ["done"] = "TestPassed",
            ["running"] = "d_PlayButton",
            ["pending"] = "TestNormal",
            ["error"] = "TestFailed",
        };

        private VisualElement BuildTodoSection()
        {
            var card = CreateCard("キュー");

            mTodoStatusLabel = new Label();
            mTodoStatusLabel.AddToClassList("mcp-sub-label");
            card.Add(mTodoStatusLabel);

            mTodoProgressBar = new ProgressBar();
            card.Add(mTodoProgressBar);

            var listScroll = new ScrollView(ScrollViewMode.Vertical);
            listScroll.AddToClassList("mcp-card__content");
            card.Add(listScroll);

            mTodoListContainer = new VisualElement();
            mTodoListContainer.AddToClassList("mcp-todo-list");
            listScroll.Add(mTodoListContainer);

            AddResizeHandle(card, listScroll, DefaultListContentHeight, "Todo");
            return card;
        }

        private void RefreshTodoSection()
        {
            if (mTodoListContainer == null)
            {
                return;
            }

            mTodoStatusLabel.text = $"ステータス: {PlanExecutionState.Status}";

            var steps = PlanExecutionState.Steps;
            mTodoProgressBar.value = steps.Count == 0
                ? 0f
                : (float)PlanExecutionState.CurrentIndex / steps.Count * 100f;
            mTodoProgressBar.title = steps.Count == 0
                ? "実行中のステップはありません。"
                : $"{PlanExecutionState.CurrentIndex} / {steps.Count}";

            mTodoListContainer.Clear();
            for (var i = 0; i < steps.Count; i++)
            {
                var state = i < PlanExecutionState.CurrentIndex ? "done"
                    : i == PlanExecutionState.CurrentIndex ? (PlanExecutionState.HasError ? "error" : "running")
                    : "pending";

                var row = new VisualElement();
                row.AddToClassList("mcp-step-row");
                row.AddToClassList($"mcp-step-row--{state}");

                var icon = new Image { image = EditorGUIUtility.IconContent(sStepIconNames[state]).image };
                icon.AddToClassList("mcp-step-icon");
                row.Add(icon);
                row.Add(new Label($"{steps[i].Id} ({steps[i].Type})"));
                mTodoListContainer.Add(row);
            }

            if (PlanExecutionState.HasError)
            {
                var errorBox = new HelpBox(PlanExecutionState.ErrorMessage, HelpBoxMessageType.Error);
                mTodoListContainer.Add(errorBox);
            }
        }

        // ===== 実行ログ =====

        // 「クリア」ボタンで隠す行数。PlanExecutionState.LogEntriesはget_execution_statusツールが
        // 参照する共有データであり、Window都合で実データを消すとMCPクライアント側の状態把握を
        // 壊しかねないため、実データは消さずこのオフセットより前を表示しないだけにとどめる
        private int mExecutionLogClearedIndex;

        private VisualElement BuildExecutionLogSection()
        {
            var card = CreateLogCard("実行ログ", () =>
            {
                mExecutionLogClearedIndex = PlanExecutionState.LogEntries.Count;
                RefreshExecutionLogSection();
            });

            mExecutionLogList = BuildLogListView();
            card.Add(mExecutionLogList);
            AddResizeHandle(card, mExecutionLogList, DefaultListContentHeight, "ExecutionLog");
            return card;
        }

        private void RefreshExecutionLogSection()
        {
            if (mExecutionLogList == null)
            {
                return;
            }
            // 新しいexecute_planが始まる等でログ本体が短くなった場合は、クリア状態を持ち越さない
            if (PlanExecutionState.LogEntries.Count < mExecutionLogClearedIndex)
            {
                mExecutionLogClearedIndex = 0;
            }
            var visibleEntries = PlanExecutionState.LogEntries.Skip(mExecutionLogClearedIndex).ToList();
            SetLogItemsAndScrollToLatest(mExecutionLogList, visibleEntries);
        }

        // ===== ツールログ =====

        private VisualElement BuildToolCallLogSection()
        {
            var card = CreateLogCard("ツールログ", MCPToolCallLog.Clear);

            mToolCallLogList = BuildLogListView();
            card.Add(mToolCallLogList);
            AddResizeHandle(card, mToolCallLogList, DefaultListContentHeight, "ToolCallLog");
            return card;
        }

        private void RefreshToolCallLogSection()
        {
            if (mToolCallLogList == null)
            {
                return;
            }
            SetLogItemsAndScrollToLatest(mToolCallLogList, MCPToolCallLog.Entries);
        }

        // ===== システムイベントログ =====

        private VisualElement BuildSystemLogSection()
        {
            var card = CreateLogCard("システムログ", MCPSystemEventLog.Clear);

            mSystemLogList = BuildLogListView();
            card.Add(mSystemLogList);
            AddResizeHandle(card, mSystemLogList, DefaultListContentHeight, "SystemLog");
            return card;
        }

        private void RefreshSystemLogSection()
        {
            if (mSystemLogList == null)
            {
                return;
            }
            SetLogItemsAndScrollToLatest(mSystemLogList, MCPSystemEventLog.Entries);
        }

        // ログ更新の共通処理。itemsSourceを差し替えてRebuildした後、更新前に最下部を見ていた場合のみ
        // 最新行へスクロールする(過去ログを読んでいる最中に強制スクロールさせないため)
        private static void SetLogItemsAndScrollToLatest(ListView aListView, IReadOnlyList<string> aEntries)
        {
            var wasAtBottom = IsScrolledToBottom(aListView);

            var items = aEntries.ToList();
            aListView.itemsSource = items;
            aListView.Rebuild();

            if (items.Count > 0 && wasAtBottom)
            {
                aListView.ScrollToItem(items.Count - 1);
            }
        }

        // ListView内部のScrollViewがほぼ最下部までスクロールされているかを判定する。
        // レイアウト未計算(初回描画前)等でNaNになる場合は「最下部にいた」扱いにして
        // 初回の自動スクロールを妨げないようにする
        private static bool IsScrolledToBottom(ListView aListView)
        {
            const float Tolerance = 4f;
            var scrollView = aListView.Q<ScrollView>();
            if (scrollView == null)
            {
                return true;
            }
            var maxScroll = scrollView.contentContainer.layout.height - scrollView.contentViewport.layout.height;
            if (float.IsNaN(maxScroll))
            {
                return true;
            }
            return scrollView.scrollOffset.y >= maxScroll - Tolerance;
        }

        // 実行ログ・ツール呼び出しログ・システムログ共通のListViewを構築する。
        // ログ本文に"エラー"を含む行はエラー色、"警告"を含む行は警告色で強調する
        // (いずれも構造化されたレベル情報を持たないプレーン文字列のため、内容ベースの簡易な色分けとする)
        private static ListView BuildLogListView()
        {
            var listView = new ListView { fixedItemHeight = 18 };
            listView.AddToClassList("mcp-log-list");

            listView.makeItem = () =>
            {
                var label = new Label();
                label.AddToClassList("mcp-log-row");
                return label;
            };

            listView.bindItem = (element, index) =>
            {
                var label = (Label)element;
                var text = listView.itemsSource is IList<string> items && index < items.Count ? items[index] : string.Empty;
                label.text = text;
                label.RemoveFromClassList("mcp-log-row--error");
                label.RemoveFromClassList("mcp-log-row--warning");
                if (!string.IsNullOrEmpty(text) && text.Contains("エラー"))
                {
                    label.AddToClassList("mcp-log-row--error");
                }
                else if (!string.IsNullOrEmpty(text) && text.Contains("警告"))
                {
                    label.AddToClassList("mcp-log-row--warning");
                }
            };

            return listView;
        }
    }
}
