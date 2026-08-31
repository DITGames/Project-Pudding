/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAITreeWindow.cs
 * @author hqrse
 * @date 2026/08/25
 * @brief 判断ツリーをノードグラフとして編集する専用ウィンドウ
 * =====================================*/

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PPCore
{
    // PPUnitAIProfileDefinition をノードグラフとして編集するウィンドウ
    //
    // 左の列にタブ・グラフ・ツリー一覧を縦へ積み、右の列に選択中ノードの詳細を全高で置く
    // 詳細は条件や行動の設定欄が縦に伸びるため、他と高さを分け合わない 1 本の列にしてある
    // グラフ側はノードの配置と接続だけを扱い、条件や行動の中身は右の詳細で編集する
    public sealed class PPUnitAITreeWindow : EditorWindow
    {
        // インスペクタ幅を保存する EditorPrefs キー(ウィンドウを閉じても復元される)
        private const string PrefKeyInspectorWidth = "PPCore.PPUnitAITreeWindow.InspectorWidth";
        // 最後に開いていたアセットを保存する EditorPrefs キー
        // ウィンドウを閉じて開き直しても同じツリーへ戻れるようにするためのもの
        private const string PrefKeyLastAsset = "PPCore.PPUnitAITreeWindow.LastAssetGuid";
        // 開いていたツリーの並びを保存する EditorPrefs キー。GUID をカンマ区切りで持つ
        private const string PrefKeyOpenAssets = "PPCore.PPUnitAITreeWindow.OpenAssetGuids";
        // 右のインスペクタの既定幅
        // グラフ側に枝を並べる余白を残しつつ、条件の設定欄が折り返さずに読める幅として決めている
        private const float DefaultInspectorWidth = 500f;
        // タブを一度に並べる上限。増えすぎて 1 つあたりが潰れるのを防ぐ
        private const int MaxOpenTabs = 12;
        // タブ列の高さ。横スクロールバーの分を含めて固定する
        private const float TabStripHeight = 34f;
        // 一覧（コンテンツブラウザ）の高さを保存する EditorPrefs キー
        // 既定値を変えた際はキーも変える。前の既定で自動保存された高さを引きずらないため
        private const string PrefKeyBrowserHeight = "PPCore.PPUnitAITreeWindow.BrowserSplitHeight";
        // 一覧を表示するかを保存する EditorPrefs キー
        private const string PrefKeyBrowserVisible = "PPCore.PPUnitAITreeWindow.BrowserVisible";
        // 一覧の絞り込みを保存する EditorPrefs キー
        private const string PrefKeyBrowserFilter = "PPCore.PPUnitAITreeWindow.BrowserFilter";
        // 一覧の既定の高さ。グラフの縦を優先しつつ、数件が一目で入る程度に取る
        private const float DefaultBrowserHeight = 150f;
        // タブを掴んだ位置からこれだけ横へ動かしたら、並べ替えの操作とみなす
        // 押しただけのつもりで並びが変わらないようにするための遊び
        private const float TabDragThreshold = 6f;

        // ノードの詳細で、欄の幅のうちラベルへ回す割合と、その上下限
        private const float NodeInspectorLabelRatio = 0.45f;
        private const float MinNodeInspectorLabelWidth = 150f;
        private const float MaxNodeInspectorLabelWidth = 280f;

        // 編集対象。ドメインリロードをまたいで保持したいのでシリアライズ対象にする
        [SerializeField] private PPUnitAIProfileDefinition mTarget;
        // タブとして開いているツリー。並び順がそのままタブの並びになる
        // ドメインリロードをまたいで保持したいのでシリアライズ対象にする
        [SerializeField] private List<PPUnitAIProfileDefinition> mOpenTargets = new();
        private SerializedObject mSerializedObject;
        private PPUnitAITreeGraphView mGraphView;
        private VisualElement mInspectorContainer;
        private HelpBox mRootWarningBox;
        // 経路の強調表示に関する案内
        private HelpBox mHighlightNoticeBox;
        private float mCurrentInspectorWidth;
        // ヒートマップ表示中か。ON のあいだは個別の経路強調より優先する
        private bool mIsHeatmap;

        // ツリー一覧（コンテンツブラウザ）の状態
        private float mCurrentBrowserHeight;
        private bool mIsBrowserVisible = true;
        // 一覧の種別フィルタ。-1 なら全て、それ以外は PPUnitAITreeKind の値
        private int mBrowserFilter = -1;
        // 一覧の名前フィルタ
        private string mBrowserSearch = "";
        // 一覧の中身。フィルタを変えたときだけ組み直す
        private VisualElement mBrowserListContainer;
        // 絞り込みボタンと、それが表す絞り込み値。押されている見た目を塗り替えるために持つ
        private readonly List<(Button Button, int Filter)> mBrowserFilterButtons = new();

        // タブの並べ替えに使う状態
        // タブ列の中身。並べ替えのたびにここだけ組み直す（ウィンドウ全体を作り直すとグラフまで再生成されるため）
        private VisualElement mTabStripContent;
        // 表示中のタブと、その要素の対応。ドロップ位置を要素の配置から求めるために持つ
        private readonly List<(PPUnitAIProfileDefinition Profile, VisualElement Element)> mTabElements = new();
        // 掴んでいるタブ。掴んでいなければ null
        private PPUnitAIProfileDefinition mDraggingTab;
        // 掴んだ位置。ここからの移動量でクリックと並べ替えを見分ける
        private Vector2 mTabDragStart;
        // 差し込み位置を示す線の色
        private static readonly Color TabDropIndicatorColor = new(0.95f, 0.75f, 0.15f);
        // 実際に並べ替えとして動かしたか。false のまま離したらタブの切り替えとして扱う
        private bool mIsTabDragging;

        [MenuItem("Window/Unit AI Tree")]
        public static void Open()
        {
            var window = GetWindow<PPUnitAITreeWindow>();
            window.titleContent = new GUIContent("Unit AI Tree");
            window.minSize = new Vector2(720, 420);
            window.Show();
        }

        // Project ウィンドウで PPUnitAIProfileDefinition をダブルクリックした際にこのウィンドウで開く
        // aInstanceId : 開かれたアセットのインスタンス ID
        // aLine : 行番号（テキストアセット用。ここでは使わない）
        // return : このウィンドウで処理した場合 true
        [OnOpenAsset]
        public static bool OnOpenAsset(int aInstanceId, int aLine)
        {
            if (EditorUtility.EntityIdToObject(aInstanceId) is not PPUnitAIProfileDefinition asset)
            {
                return false;
            }

            var window = GetWindow<PPUnitAITreeWindow>();
            window.titleContent = new GUIContent("Unit AI Tree");
            window.SetTarget(asset);
            window.Show();
            return true;
        }

        private void OnEnable()
        {
            // 壊れた値が保存されていても引きずらないよう、読み込み時にも丸める
            mCurrentInspectorWidth = ClampInspectorWidth(EditorPrefs.GetFloat(PrefKeyInspectorWidth, DefaultInspectorWidth));
            mCurrentBrowserHeight = ClampBrowserHeight(EditorPrefs.GetFloat(PrefKeyBrowserHeight, DefaultBrowserHeight));
            mIsBrowserVisible = EditorPrefs.GetBool(PrefKeyBrowserVisible, true);
            mBrowserFilter = EditorPrefs.GetInt(PrefKeyBrowserFilter, -1);
            Selection.selectionChanged += HandleSelectionChanged;
            PPUnitAITreeHighlightHub.OnSelectionChanged += HandleHighlightChanged;
            PPUnitAIDebugStore.OnAdded += HandleDebugStoreChanged;
            // 閉じて開き直した場合は、前回編集していたツリーとタブの並びへ戻す
            mTarget ??= LoadLastTarget();
            LoadOpenTargets();
            OpenAsTab(mTarget);
            RebuildUI();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= HandleSelectionChanged;
            PPUnitAITreeHighlightHub.OnSelectionChanged -= HandleHighlightChanged;
            PPUnitAIDebugStore.OnAdded -= HandleDebugStoreChanged;
            SaveLayout();
        }

        // 分割の幅・高さと一覧の状態を書き出す
        //
        // ウィンドウを閉じるときだけでなく、ドラッグで動かした時点でも書き出す
        // 閉じる処理まで待つと、Unity の終了の仕方によっては OnDisable が走らず記録が消えるため
        private void SaveLayout()
        {
            EditorPrefs.SetFloat(PrefKeyInspectorWidth, mCurrentInspectorWidth);
            EditorPrefs.SetFloat(PrefKeyBrowserHeight, mCurrentBrowserHeight);
            EditorPrefs.SetBool(PrefKeyBrowserVisible, mIsBrowserVisible);
            EditorPrefs.SetInt(PrefKeyBrowserFilter, mBrowserFilter);
        }

        // 覚えてよい大きさかを判定する
        //
        // レイアウトが決まる前や、ウィンドウが畳まれる過程では 0 や NaN が返ってくる
        // それをそのまま覚えると、次に開いたときの幅・高さが潰れた値から始まってしまう
        //
        // aValue : 判定する値
        // return : 覚えてよければ true
        private static bool IsUsableSize(float aValue)
            => !float.IsNaN(aValue) && !float.IsInfinity(aValue) && aValue > 1f;

        // 一覧の高さを扱える範囲へ丸める
        // レイアウトが決まりきる前の値をそのまま覚えると、次に開いたとき一覧が全高を取ってしまう
        // aHeight : 丸める高さ
        // return : 丸めた高さ
        private static float ClampBrowserHeight(float aHeight) => Mathf.Clamp(aHeight, 60f, 600f);

        // 右のインスペクタの幅を扱える範囲へ丸める
        // aWidth : 丸める幅
        // return : 丸めた幅
        private static float ClampInspectorWidth(float aWidth) => Mathf.Clamp(aWidth, 240f, 900f);

        // ノードの詳細で使うラベル幅を、欄の幅から求める
        // 通常のインスペクタと同じ「表示幅のおよそ半分」を目安にし、狭すぎ・広すぎにならない範囲へ収める
        // return : ラベル幅
        private float ResolveNodeInspectorLabelWidth()
            => Mathf.Clamp(mCurrentInspectorWidth * NodeInspectorLabelRatio,
                MinNodeInspectorLabelWidth, MaxNodeInspectorLabelWidth);

        // デバッグウィンドウで選ばれた思考記録を受けて、経路の強調表示を更新する
        // aEntry : 選ばれた思考記録。解除時は null
        private void HandleHighlightChanged(PPUnitAIThinkEntry aEntry) => RefreshHighlight();

        // 思考記録が増えたときにヒートマップを取り直す
        private void HandleDebugStoreChanged()
        {
            if (mIsHeatmap) RefreshHighlight();
        }

        // 経路の強調表示・ヒートマップを現在の状態へ更新する
        // ヒートマップが ON のときはそちらを優先し、記録全体の通過回数で濃淡を付ける
        private void RefreshHighlight()
        {
            if (mGraphView == null || mTarget == null) return;

            if (mIsHeatmap)
            {
                mGraphView.ApplyHeatmap(CollectHeatCounts());
                SetHighlightNotice("");
                return;
            }

            var entry = PPUnitAITreeHighlightHub.Selected;
            if (entry == null)
            {
                mGraphView.ClearHighlight();
                SetHighlightNotice("");
                return;
            }

            // 別のツリーを開いている状態で強調しても意味が無いため、その旨だけ知らせる
            if (!ReferenceEquals(entry.Profile, mTarget))
            {
                mGraphView.ClearHighlight();
                string name = entry.Profile != null ? entry.Profile.name : "不明";
                SetHighlightNotice($"選択中の思考は「{name}」のものです。同じツリーを開くと経路が表示されます。");
                return;
            }

            mGraphView.ApplyHighlight(entry.VisitedNodeIds, entry.DecidedNodeId);
            SetHighlightNotice("");
        }

        // ためられている思考記録から、このツリーのノード通過回数を集計する
        // return : ノード ID ごとの通過回数
        private Dictionary<string, int> CollectHeatCounts()
        {
            var counts = new Dictionary<string, int>();
            foreach (var report in PPUnitAIDebugStore.Reports)
            {
                foreach (var entry in report.Units)
                {
                    if (!ReferenceEquals(entry.Profile, mTarget)) continue;

                    foreach (var nodeId in entry.VisitedNodeIds)
                    {
                        if (string.IsNullOrEmpty(nodeId)) continue;

                        counts.TryGetValue(nodeId, out int value);
                        counts[nodeId] = value + 1;
                    }
                }
            }
            return counts;
        }

        // 強調表示に関する案内を出す。空文字なら非表示にする
        // aMessage : 表示する文言
        private void SetHighlightNotice(string aMessage)
        {
            if (mHighlightNoticeBox == null) return;

            mHighlightNoticeBox.text = aMessage;
            mHighlightNoticeBox.style.display = string.IsNullOrEmpty(aMessage)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        // 編集対象を差し替えてグラフを組み立て直す
        // aTarget : 編集する判断ツリー
        public void SetTarget(PPUnitAIProfileDefinition aTarget)
        {
            mTarget = aTarget;
            OpenAsTab(aTarget);
            SaveLastTarget();
            RebuildUI();
        }

        // 開いているツリーが無いときの表示を組み立てる
        // ここでも一覧は出しておく。一覧から選べるのに「Project ウィンドウで選べ」と促すのはちぐはぐなため
        private void BuildEmptyUI()
        {
            var notice = new HelpBox(
                "編集する判断ツリーを下の一覧から選ぶか、Project ウィンドウで選択してください。",
                HelpBoxMessageType.Info);

            if (!mIsBrowserVisible)
            {
                // 一覧を畳んでいる状態では戻す手立てが無くなるため、切り替えボタンだけは出す
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                row.Add(new Button(() =>
                {
                    mIsBrowserVisible = true;
                    RebuildUI();
                })
                { text = "▲ 一覧" });
                rootVisualElement.Add(row);
                rootVisualElement.Add(notice);
                return;
            }

            var split = new TwoPaneSplitView(1, mCurrentBrowserHeight, TwoPaneSplitViewOrientation.Vertical);
            split.Add(notice);
            split.Add(BuildBrowser());
            rootVisualElement.Add(split);
        }

        // プロジェクト内の判断ツリーを一覧するコンテンツブラウザを組み立てる
        //
        // メインツリーとサブツリーを行き来しながら組む場面が多いため、
        // Project ウィンドウへ戻らずにこのウィンドウの中だけで探して開けるようにする
        // 種別（メイン / サブ）と名前の 2 段で絞り込める
        //
        // return : ブラウザの要素
        private VisualElement BuildBrowser()
        {
            var browser = new VisualElement { style = { flexGrow = 1f } };
            // グラフの下へ並ぶため、境目は上端に引く
            browser.style.borderTopWidth = 1f;
            browser.style.borderTopColor = new Color(0.15f, 0.15f, 0.15f);

            browser.Add(BuildBrowserFilterRow());

            var search = new ToolbarSearchField { value = mBrowserSearch };
            search.style.marginLeft = 4f;
            search.style.marginRight = 4f;
            search.style.marginBottom = 2f;
            search.RegisterValueChangedCallback(evt =>
            {
                mBrowserSearch = evt.newValue;
                RefreshBrowserList();
            });
            browser.Add(search);

            var scroll = new ScrollView { style = { flexGrow = 1f } };
            mBrowserListContainer = scroll.contentContainer;
            browser.Add(scroll);

            RefreshBrowserList();
            return browser;
        }

        // 種別の絞り込みボタンを並べる
        //
        // ボタンは PPUnitAITreeKind の列挙子から組み立てる
        // 種別を増やしたときに、インスペクタの選択肢と一覧の絞り込みが片方だけ古くなるのを防ぐため
        // 表示名は InspectorName を使い、インスペクタ側の表記と揃える
        // 種別が増えて 1 行に収まらなくなったら折り返す
        //
        // return : 絞り込み行の要素
        private VisualElement BuildBrowserFilterRow()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginLeft = 4f;
            row.style.marginRight = 4f;
            row.style.marginTop = 4f;
            row.style.marginBottom = 2f;

            mBrowserFilterButtons.Clear();
            AddFilterButton(row, "全て", -1);
            foreach (PPUnitAITreeKind kind in System.Enum.GetValues(typeof(PPUnitAITreeKind)))
            {
                AddFilterButton(row, ToDisplayName(kind), (int)kind);
            }

            ApplyFilterButtonStyles();
            return row;
        }

        // 押されている絞り込みボタンを目立たせる
        //
        // ボタンを作り直さず、既にあるものの見た目だけを塗り替える
        // 押した瞬間にボタンごと作り直すと、押されたボタン自身が処理の途中で消えることになる
        // ウィンドウ全体を組み立て直すのも避ける。グラフまで再生成され、表示位置や分割の幅が戻ってしまうため
        private void ApplyFilterButtonStyles()
        {
            foreach (var (button, filter) in mBrowserFilterButtons)
            {
                bool isActive = mBrowserFilter == filter;
                button.style.backgroundColor = isActive
                    ? new StyleColor(new Color(0.35f, 0.45f, 0.55f))
                    : new StyleColor(StyleKeyword.Null);
                button.style.unityFontStyleAndWeight = isActive ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        // 列挙子の表示名を引く
        // InspectorName が付いていればその文言、無ければ列挙子の名前をそのまま使う
        // aKind : 対象の列挙子
        // return : 表示名
        private static string ToDisplayName(PPUnitAITreeKind aKind)
        {
            var field = typeof(PPUnitAITreeKind).GetField(aKind.ToString());
            var attribute = field?.GetCustomAttribute<InspectorNameAttribute>();
            return attribute != null ? attribute.displayName : aKind.ToString();
        }

        // 絞り込みボタンを 1 つ足す
        // aRow : 追加先の行
        // aText : ボタンの文言
        // aFilter : 押したときに設定する絞り込み値
        private void AddFilterButton(VisualElement aRow, string aText, int aFilter)
        {
            var button = new Button(() =>
            {
                mBrowserFilter = aFilter;
                ApplyFilterButtonStyles();
                RefreshBrowserList();
            })
            { text = aText };

            // 折り返しを効かせるため、幅は文言なりにする（引き伸ばすと折り返した行だけ間延びする）
            button.style.marginLeft = 0f;
            button.style.marginRight = 1f;
            button.style.marginBottom = 1f;
            button.style.paddingLeft = 6f;
            button.style.paddingRight = 6f;

            // 押されている見た目は ApplyFilterButtonStyles がまとめて当てる
            mBrowserFilterButtons.Add((button, aFilter));
            aRow.Add(button);
        }

        // 一覧の中身を現在の絞り込みで組み立て直す
        private void RefreshBrowserList()
        {
            if (mBrowserListContainer == null) return;

            mBrowserListContainer.Clear();

            int count = 0;
            foreach (var profile in EnumerateProfiles())
            {
                mBrowserListContainer.Add(BuildBrowserRow(profile));
                count++;
            }

            if (count == 0)
            {
                mBrowserListContainer.Add(new Label("該当する判断ツリーがありません。")
                {
                    style = { paddingLeft = 6f, paddingTop = 6f, whiteSpace = WhiteSpace.Normal },
                });
            }
        }

        // 絞り込みに合う判断ツリーを名前順で列挙する
        // return : 該当するツリー
        private IEnumerable<PPUnitAIProfileDefinition> EnumerateProfiles()
        {
            var found = new List<PPUnitAIProfileDefinition>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(PPUnitAIProfileDefinition)}"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<PPUnitAIProfileDefinition>(path);
                if (profile == null) continue;
                if (mBrowserFilter >= 0 && (int)profile.TreeKind != mBrowserFilter) continue;
                if (!string.IsNullOrEmpty(mBrowserSearch)
                    && profile.name.IndexOf(mBrowserSearch, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                found.Add(profile);
            }

            found.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
            return found;
        }

        // 一覧の 1 行分を組み立てる
        // 押すとタブとして開き、そのツリーへ切り替わる
        // aProfile : 対象のツリー
        // return : 行の要素
        private VisualElement BuildBrowserRow(PPUnitAIProfileDefinition aProfile)
        {
            bool isActive = ReferenceEquals(aProfile, mTarget);

            var row = new Button(() => SetTarget(aProfile)) { text = aProfile.name };
            row.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.style.marginLeft = 2f;
            row.style.marginRight = 2f;
            row.style.marginTop = 0f;
            row.style.marginBottom = 1f;
            row.style.paddingLeft = 6f;
            // 開いているツリーは一覧側でも分かるようにする
            if (isActive)
            {
                row.style.backgroundColor = new Color(0.26f, 0.34f, 0.42f);
                row.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            string kind = aProfile.TreeKind == PPUnitAITreeKind.SubTree ? "サブツリー" : "メインツリー";
            string description = string.IsNullOrEmpty(aProfile.Description) ? "" : $"\n{aProfile.Description}";
            row.tooltip = $"{kind}\n{AssetDatabase.GetAssetPath(aProfile)}{description}";
            return row;
        }

        // 開いているツリーをタブとして横一列に並べる
        // 複数のツリーを行き来しながら組む場面が多いため、Project ウィンドウへ戻らずに切り替えられるようにする
        // 幅が足りなくなったら横スクロールさせ、タブが潰れて名前が読めなくなるのを防ぐ
        // return : タブ列の要素
        private VisualElement BuildTabStrip()
        {
            var strip = new ScrollView(ScrollViewMode.Horizontal);
            strip.style.flexGrow = 1f;
            // 高さを決めておかないと、スクロールバーの分だけヘッダが縦に伸びる
            strip.style.height = TabStripHeight;
            strip.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            strip.contentContainer.style.flexDirection = FlexDirection.Row;

            mTabStripContent = strip.contentContainer;
            RefreshTabStrip();
            return strip;
        }

        // タブ列の中身だけを組み立て直す
        // 並べ替えのたびにウィンドウ全体を作り直すと、グラフまで再生成されて重いうえ表示位置も飛ぶ
        private void RefreshTabStrip()
        {
            if (mTabStripContent == null) return;

            mTabStripContent.Clear();
            mTabElements.Clear();

            foreach (var profile in mOpenTargets)
            {
                if (profile == null) continue;

                var element = BuildTab(profile);
                mTabElements.Add((profile, element));
                mTabStripContent.Add(element);
            }
        }

        // タブ 1 枚分を組み立てる
        // 本体を押すと切り替え、右端の × で閉じる
        // aProfile : 対象のツリー
        // return : タブの要素
        private VisualElement BuildTab(PPUnitAIProfileDefinition aProfile)
        {
            bool isActive = ReferenceEquals(aProfile, mTarget);

            var tab = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            tab.style.marginLeft = 2f;
            tab.style.marginTop = 2f;
            tab.style.paddingLeft = 8f;
            tab.style.paddingRight = 2f;
            // 選択中のタブだけ明るくし、下端に線を引いてどれを見ているかを示す
            tab.style.backgroundColor = isActive ? new Color(0.26f, 0.26f, 0.26f) : new Color(0.19f, 0.19f, 0.19f);
            tab.style.borderBottomWidth = isActive ? 2f : 0f;
            tab.style.borderBottomColor = new Color(0.35f, 0.60f, 0.85f);

            var label = new Label(aProfile.name)
            {
                style = { unityFontStyleAndWeight = isActive ? FontStyle.Bold : FontStyle.Normal },
            };
            label.style.paddingTop = 3f;
            label.style.paddingBottom = 3f;
            label.style.paddingRight = 6f;
            label.tooltip = $"{AssetDatabase.GetAssetPath(aProfile)}\n左右へドラッグすると並べ替えられます";
            RegisterTabDrag(label, aProfile);
            tab.Add(label);

            var close = new Button(() => CloseTab(aProfile)) { text = "×" };
            close.style.width = 16f;
            close.style.height = 16f;
            close.style.marginLeft = 0f;
            close.style.marginRight = 0f;
            close.style.paddingLeft = 0f;
            close.style.paddingRight = 0f;
            close.tooltip = "このタブを閉じる";
            tab.Add(close);

            return tab;
        }

        // タブのつまみ（名前ラベル）へ、切り替えと並べ替えの操作を結び付ける
        //
        // 押しただけなら切り替え、左右へ動かしてから離したら並べ替え、という 1 つの操作でまとめて扱う
        // 並べ替えの確定はボタンを離した時点に寄せている
        // ドラッグ中にタブ列を組み立て直すと、掴んでいる要素ごと作り替わってポインタの捕捉が外れるため
        //
        // aLabel : つまみになる要素
        // aProfile : そのタブが指すツリー
        private void RegisterTabDrag(VisualElement aLabel, PPUnitAIProfileDefinition aProfile)
        {
            aLabel.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;

                mDraggingTab = aProfile;
                mTabDragStart = evt.position;
                mIsTabDragging = false;
                aLabel.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });

            aLabel.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!ReferenceEquals(mDraggingTab, aProfile) || !aLabel.HasPointerCapture(evt.pointerId)) return;

                // 少し動かすまではクリックとして扱う。押しただけで並びが変わるのを防ぐ
                if (!mIsTabDragging
                    && Mathf.Abs(evt.position.x - mTabDragStart.x) < TabDragThreshold) return;

                mIsTabDragging = true;
                ShowTabDropIndicator(ResolveTabDropIndex(evt.position));
                evt.StopPropagation();
            });

            aLabel.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!ReferenceEquals(mDraggingTab, aProfile)) return;

                aLabel.ReleasePointer(evt.pointerId);
                bool isDragged = mIsTabDragging;
                int dropIndex = isDragged ? ResolveTabDropIndex(evt.position) : -1;

                mDraggingTab = null;
                mIsTabDragging = false;
                ClearTabDropIndicator();

                // 動かしていなければ、ただ押しただけなのでタブの切り替えにする
                if (!isDragged)
                {
                    SetTarget(aProfile);
                    return;
                }

                MoveTab(aProfile, dropIndex);
                evt.StopPropagation();
            });

            // ウィンドウ外へ抜けるなどで捕捉が切れた場合に、掴んだままにしない
            aLabel.RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                mDraggingTab = null;
                mIsTabDragging = false;
                ClearTabDropIndicator();
            });
        }

        // ポインタの位置から、タブを差し込む位置を求める
        // 各タブの中央より左なら手前、右なら奥へ差し込む
        // aPosition : ポインタの位置（ワールド座標）
        // return : 差し込む位置。タブが無ければ 0
        private int ResolveTabDropIndex(Vector3 aPosition)
        {
            if (mTabStripContent == null || mTabElements.Count == 0) return 0;

            float x = mTabStripContent.WorldToLocal(aPosition).x;
            for (int i = 0; i < mTabElements.Count; i++)
            {
                var rect = mTabElements[i].Element.layout;
                if (x < rect.center.x) return i;
            }
            return mTabElements.Count;
        }

        // 差し込む位置を線で示す
        // aIndex : 差し込む位置
        private void ShowTabDropIndicator(int aIndex)
        {
            for (int i = 0; i < mTabElements.Count; i++)
            {
                var style = mTabElements[i].Element.style;
                style.borderLeftColor = TabDropIndicatorColor;
                style.borderRightColor = TabDropIndicatorColor;
                // 差し込む位置の手前と奥どちらのタブに線を出すかを、位置から決める
                style.borderLeftWidth = i == aIndex ? 2f : 0f;
                style.borderRightWidth = i == mTabElements.Count - 1 && aIndex == mTabElements.Count ? 2f : 0f;
            }
        }

        // 差し込み位置の線を消す
        private void ClearTabDropIndicator()
        {
            foreach (var (_, element) in mTabElements)
            {
                element.style.borderLeftWidth = 0f;
                element.style.borderRightWidth = 0f;
            }
        }

        // タブを指定した位置へ動かす
        // aProfile : 動かすタブ
        // aDropIndex : 差し込む位置。動かす前の並びを基準にした値
        private void MoveTab(PPUnitAIProfileDefinition aProfile, int aDropIndex)
        {
            int from = mOpenTargets.IndexOf(aProfile);
            if (from < 0) return;

            // 自分を抜いた後の並びで数え直す。自分より後ろへ動かす場合は 1 つ手前になる
            int to = aDropIndex > from ? aDropIndex - 1 : aDropIndex;
            to = Mathf.Clamp(to, 0, mOpenTargets.Count - 1);
            if (to == from) return;

            mOpenTargets.RemoveAt(from);
            mOpenTargets.Insert(to, aProfile);
            SaveOpenTargets();
            RefreshTabStrip();
        }

        // ツリーをタブとして開く。既に開いていれば並びを変えずそのまま使う
        // 上限を超えた場合は、いま開いているもの以外で最も古いタブを閉じる
        // aTarget : 開くツリー
        private void OpenAsTab(PPUnitAIProfileDefinition aTarget)
        {
            if (aTarget == null) return;

            // 消えたアセットがタブに残らないよう、ここで掃除しておく
            mOpenTargets.RemoveAll(t => t == null);
            if (mOpenTargets.Contains(aTarget)) return;

            mOpenTargets.Add(aTarget);
            while (mOpenTargets.Count > MaxOpenTabs)
            {
                int oldest = mOpenTargets.FindIndex(t => !ReferenceEquals(t, aTarget));
                if (oldest < 0) break;

                mOpenTargets.RemoveAt(oldest);
            }
        }

        // タブを 1 つ閉じる
        // 閉じたのが表示中のタブだった場合は、隣のタブへ移る
        // aTarget : 閉じるツリー
        private void CloseTab(PPUnitAIProfileDefinition aTarget)
        {
            int index = mOpenTargets.IndexOf(aTarget);
            if (index < 0) return;

            mOpenTargets.RemoveAt(index);
            if (!ReferenceEquals(aTarget, mTarget))
            {
                SaveOpenTargets();
                RebuildUI();
                return;
            }

            // 閉じた位置に来たタブ（末尾を閉じたなら 1 つ前）へ移る
            int next = Mathf.Clamp(index, 0, mOpenTargets.Count - 1);
            mTarget = mOpenTargets.Count > 0 ? mOpenTargets[next] : null;
            SaveLastTarget();
            RebuildUI();
        }

        // 最後に開いていたアセットと、タブの並びを覚えておく
        private void SaveLastTarget()
        {
            EditorPrefs.SetString(PrefKeyLastAsset, ToGuid(mTarget));
            SaveOpenTargets();
        }

        // タブの並びを覚えておく
        private void SaveOpenTargets()
        {
            var guids = mOpenTargets.Where(t => t != null).Select(ToGuid).Where(g => !string.IsNullOrEmpty(g));
            EditorPrefs.SetString(PrefKeyOpenAssets, string.Join(",", guids));
        }

        // アセットの GUID を引く
        // aTarget : 対象のツリー
        // return : GUID。アセットでなければ空文字
        private static string ToGuid(PPUnitAIProfileDefinition aTarget)
        {
            string path = aTarget == null ? "" : AssetDatabase.GetAssetPath(aTarget);
            return string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path);
        }

        // 前回のタブの並びを読み直す
        // 削除済みのアセットは黙って落とす
        private void LoadOpenTargets()
        {
            mOpenTargets.RemoveAll(t => t == null);
            if (mOpenTargets.Count > 0) return;

            foreach (string guid in EditorPrefs.GetString(PrefKeyOpenAssets, "").Split(','))
            {
                if (string.IsNullOrEmpty(guid)) continue;

                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;

                var profile = AssetDatabase.LoadAssetAtPath<PPUnitAIProfileDefinition>(path);
                if (profile != null && !mOpenTargets.Contains(profile)) mOpenTargets.Add(profile);
            }
        }

        // 最後に開いていたアセットを読み直す
        // return : 復元できたアセット。記録が無い・削除済みなら null
        private static PPUnitAIProfileDefinition LoadLastTarget()
        {
            string guid = EditorPrefs.GetString(PrefKeyLastAsset, "");
            if (string.IsNullOrEmpty(guid)) return null;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<PPUnitAIProfileDefinition>(path);
        }

        // Project ウィンドウで別のプロファイルを選んだら、そちらへ追従する
        private void HandleSelectionChanged()
        {
            if (Selection.activeObject is not PPUnitAIProfileDefinition profile) return;
            if (ReferenceEquals(profile, mTarget)) return;

            SetTarget(profile);
        }

        // ウィンドウの中身を組み立て直す
        private void RebuildUI()
        {
            rootVisualElement.Clear();

            if (mTarget == null)
            {
                BuildEmptyUI();
                return;
            }

            // 手で追加されたノードなど、ID 未採番のものをここで埋めておく
            mTarget.EnsureNodeIds();
            mSerializedObject = new SerializedObject(mTarget);

            var headerRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            headerRow.style.height = TabStripHeight;
            headerRow.style.flexShrink = 0f;

            // 一覧はグラフの高さを食うため、畳めるようにしておく
            var browserToggle = new Button(() =>
            {
                mIsBrowserVisible = !mIsBrowserVisible;
                RebuildUI();
            })
            { text = mIsBrowserVisible ? "▼ 一覧" : "▲ 一覧" };
            browserToggle.style.marginLeft = 2f;
            browserToggle.tooltip = "判断ツリーの一覧の表示を切り替える";
            headerRow.Add(browserToggle);

            headerRow.Add(BuildTabStrip());

            // 整列は縦位置＝優先度を保ったまま置き直すため、押しても判断の順序は変わらない
            var layoutButton = new Button(() => mGraphView?.AutoLayout()) { text = "自動整列" };
            layoutButton.style.marginRight = 6f;
            headerRow.Add(layoutButton);

            var noteButton = new Button(() => mGraphView?.AddNote()) { text = "付箋を追加" };
            noteButton.style.marginRight = 6f;
            headerRow.Add(noteButton);

            // 左の列にツリーの編集に関わるものを縦へ積み、右の列に詳細を全高で置く
            // 詳細は条件や行動の設定欄が縦に伸びるため、他に高さを分けず 1 本で使えるようにしている
            var leftColumn = new VisualElement { style = { flexGrow = 1f } };
            leftColumn.Add(headerRow);

            // 文言は診断結果から毎回組み立てるため、ここでは箱だけ用意する
            mRootWarningBox = new HelpBox("", HelpBoxMessageType.Warning);
            leftColumn.Add(mRootWarningBox);

            mHighlightNoticeBox = new HelpBox("", HelpBoxMessageType.Info);
            mHighlightNoticeBox.style.display = DisplayStyle.None;
            leftColumn.Add(mHighlightNoticeBox);

            var heatmapToggle = new Toggle("ヒートマップ") { value = mIsHeatmap };
            heatmapToggle.RegisterValueChangedCallback(evt =>
            {
                mIsHeatmap = evt.newValue;
                RefreshHighlight();
            });
            heatmapToggle.style.paddingLeft = 6f;
            heatmapToggle.style.flexShrink = 0f;
            leftColumn.Add(heatmapToggle);

            mGraphView = new PPUnitAITreeGraphView(mSerializedObject, mTarget) { name = "UnitAITreeGraph" };
            mGraphView.style.flexGrow = 1f;
            mGraphView.OnNodeSelectionChanged += ShowNodeInspector;
            mGraphView.OnGraphStructureChanged += RefreshWarnings;

            // 一覧はグラフの下へ、縦の分割で並べる
            if (mIsBrowserVisible)
            {
                var browserSplit = new TwoPaneSplitView(1, mCurrentBrowserHeight,
                    TwoPaneSplitViewOrientation.Vertical);
                var browserPane = BuildBrowser();
                browserSplit.Add(mGraphView);
                browserSplit.Add(browserPane);
                // 監視はペイン自身へ登録する
                // GeometryChangedEvent は「その要素の大きさが変わったとき」にだけ飛び、親子へ伝播しない
                // 分割側へ登録すると、仕切りをドラッグしてもペインが変わるだけで分割自身は変わらず、
                // 値が一度も更新されないまま既定へ戻ってしまう
                browserPane.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    float height = browserPane.resolvedStyle.height;
                    if (!IsUsableSize(height)) return;

                    float clamped = ClampBrowserHeight(height);
                    if (Mathf.Approximately(clamped, mCurrentBrowserHeight)) return;

                    mCurrentBrowserHeight = clamped;
                    SaveLayout();
                });
                leftColumn.Add(browserSplit);
            }
            else
            {
                leftColumn.Add(mGraphView);
            }

            // 分割は中身を入れ終えてから親へ足す
            // 空のまま親へ足すと、2 つ揃った時点での初期化が済んでおらず幅が決まらない
            var split = new TwoPaneSplitView(1, mCurrentInspectorWidth, TwoPaneSplitViewOrientation.Horizontal);
            split.Add(leftColumn);

            mInspectorContainer = new ScrollView { style = { flexGrow = 1f } };
            split.Add(mInspectorContainer);

            // ドラッグでの幅変更を追いかける。監視先は分割ではなくペイン自身（browserPane 側と同じ理由）
            var inspectorPane = mInspectorContainer;
            inspectorPane.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                float width = inspectorPane.resolvedStyle.width;
                if (!IsUsableSize(width)) return;

                float clamped = ClampInspectorWidth(width);
                if (Mathf.Approximately(clamped, mCurrentInspectorWidth)) return;

                mCurrentInspectorWidth = clamped;
                SaveLayout();
            });
            rootVisualElement.Add(split);

            ShowNodeInspector(null);
            RefreshWarnings();
            RefreshHighlight();
            // 開いた直後にどこを見ているか分からなくならないよう、ルートを画面中央へ寄せる
            mGraphView.FrameRootNode();
        }

        // 選択中ノードのインスペクタを右側へ表示する
        // aNode : 選択されたノード。未選択なら null
        private void ShowNodeInspector(PPUnitAINode aNode)
        {
            mInspectorContainer.Clear();

            if (aNode == null)
            {
                mInspectorContainer.Add(new Label("ノードを選択すると、ここで条件と行動を編集できます。")
                {
                    style = { paddingLeft = 8f, paddingTop = 8f, whiteSpace = WhiteSpace.Normal },
                });
                return;
            }

            // 選ぶたびに畳まれていると毎回開き直す手間になるため、最初から展開しておく
            var selected = FindNodeProperty(aNode);
            if (selected == null) return;

            selected.isExpanded = true;

            // IMGUI 側の PropertyDrawer（条件・行動の型ピッカー）をそのまま使いたいので IMGUIContainer で描く
            var container = new IMGUIContainer(() =>
            {
                if (mSerializedObject == null || mSerializedObject.targetObject == null) return;

                mSerializedObject.Update();

                // プロパティは描画のたびに引き直す
                // 配列要素を指す SerializedProperty は要素が消えると無効になるため、
                // 最初に引いたものを持ち回すと、ノードを削除した瞬間に描画側が壊れたプロパティへ触れてしまう
                // （mNodes.Array.data[N] has disappeared / 配列に対する managedReference 参照、として例外が出る）
                var property = FindNodeProperty(aNode);
                if (property == null)
                {
                    // 表示中のノードが消えている。選択なしの表示へ戻す
                    // 描画中に差し替えると今まさに描いている入れ物ごと消すことになるため、次のフレームへ回す
                    mInspectorContainer.schedule.Execute(() => ShowNodeInspector(null));
                    return;
                }

                // ラベル幅をこの欄の幅から決める
                // 通常のインスペクタでは Unity が表示幅に応じて決めてくれるが、
                // 自前の IMGUI 領域ではその面倒を見てもらえず、既定のまま狭く扱われて
                // 日本語の表示名が途中で切れてしまう
                float previousLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = ResolveNodeInspectorLabelWidth();

                EditorGUILayout.PropertyField(property, new GUIContent(aNode.NodeName), true);
                if (mSerializedObject.ApplyModifiedProperties())
                {
                    mTarget.InvalidateNodeMap();
                    // ノード名・割り込み指定・評価除外を書き換えた場合に、グラフ側の表示を即座に追従させる
                    mGraphView.RefreshNodeView(aNode.NodeId);
                    // 行動の設定漏れなどは診断結果に効くため、警告欄も取り直す
                    RefreshWarnings();
                }

                DrawNodeTools(aNode);

                EditorGUIUtility.labelWidth = previousLabelWidth;
            });
            container.style.paddingLeft = 6f;
            container.style.paddingRight = 6f;
            container.style.paddingTop = 6f;
            mInspectorContainer.Add(container);
        }

        // ノード種別ごとの補助操作を描く
        // 標準のフィールド編集では届かない、まとめて書き換える系の操作を置く
        // aNode : 表示中のノード
        private void DrawNodeTools(PPUnitAINode aNode)
        {
            if (aNode is not PPUnitAILotteryNode lottery) return;

            EditorGUILayout.Space();
            if (!GUILayout.Button("重みを正規化（0〜1）")) return;

            // 比率は変わらないため挙動は同じだが、数字がそのまま確率として読めるようになる
            Undo.RecordObject(mTarget, "抽選の重みを正規化");
            lottery.NormalizeWeights();
            EditorUtility.SetDirty(mTarget);
            mSerializedObject.Update();
        }

        // ノードに対応する SerializedProperty を引く
        // aNode : 対象ノード
        // return : 該当する配列要素のプロパティ。見つからなければ null
        private SerializedProperty FindNodeProperty(PPUnitAINode aNode)
        {
            var nodesProperty = mSerializedObject.FindProperty("mNodes");
            for (int i = 0; i < nodesProperty.arraySize; i++)
            {
                var element = nodesProperty.GetArrayElementAtIndex(i);
                if (element.managedReferenceValue is PPUnitAINode node && ReferenceEquals(node, aNode))
                {
                    return element;
                }
            }
            return null;
        }

        // ツリー診断を走らせ、警告欄とグラフ表示へ反映する
        // ウィンドウを開いた時点と、グラフ構造が変わった時点で呼ばれる
        private void RefreshWarnings()
        {
            if (mRootWarningBox == null || mTarget == null) return;

            var issues = PPUnitAITreeValidator.Validate(mTarget);
            if (issues.Count == 0)
            {
                mRootWarningBox.style.display = DisplayStyle.None;
                mGraphView?.ApplyIssues(issues);
                return;
            }

            var builder = new StringBuilder();
            foreach (var issue in issues)
            {
                if (builder.Length > 0) builder.Append('\n');
                builder.Append("・").Append(issue.Message);
            }

            mRootWarningBox.text = builder.ToString();
            mRootWarningBox.style.display = DisplayStyle.Flex;
            mGraphView?.ApplyIssues(issues);
        }
    }
}
