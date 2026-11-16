/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitAITreeNodeView.cs
 * @author hqrse
 * @date 2026/08/25
 * @brief 判断ツリーのノード1つ分のグラフ表示
 * =====================================*/

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace PPCore
{
    // 判断ツリーのノード 1 つをグラフ上へ表示するビュー。見た目だけを持ち、実データへの反映はグラフビューが行う
    // 上側に入力ポート、右側にノード種別ごとの出力ポートを生やす
    internal sealed class PPUnitAITreeNodeView : Node
    {
        /* 定数 */

        // 警告アイコンの大きさ。タイトルバーの高さに収まる値にしてある
        private const float IssueIconSize = 16f;
        // 要約欄の背景色。ノード本体の地の色に合わせた不透明な色
        private static readonly Color SummaryBackgroundColor = new(0.17f, 0.17f, 0.17f, 1f);
        // ヒートマップの濃淡に使う固定の配色（寒色→暖色）。種別色に依存すると差が読み取りにくいため固定にしている
        private static readonly Color HeatColorCold = new(0.16f, 0.20f, 0.30f);
        private static readonly Color HeatColorHot = new(0.95f, 0.20f, 0.10f);

        /* 状態 */

        // 表示対象のノード
        public PPUnitAINode Node { get; }
        // 親から繋がる入力ポート
        public Port InputPort { get; }
        private readonly List<Port> mOutputPorts = new();
        // ノード種別ごとのタイトル色。他の表示から戻す際の基準にする
        private Color mTitleColor;
        // 設定内容の要約を出すラベル。中身が空のときは非表示にする
        private readonly Label mSummaryLabel = new();
        // 診断に引っかかっていることを示す警告アイコン。問題が無いときは非表示にする
        private readonly Image mIssueIcon = new();

        // タイトル色を決める 3 つの状態
        // 互いを打ち消さないよう状態として持ち、塗るのは ApplyTitleColor の 1 箇所に集約する

        // 診断に引っかかっているか
        private bool mIsIssue;

        // 通過経路としての強調の種類
        private PPUnitAITreeHighlight mHighlight = PPUnitAITreeHighlight.None;
        // 通過回数の濃淡。ヒートマップ表示中でなければ負値
        private float mHeatRatio = -1f;

        /* プロパティ */

        // 接続口ごとの出力ポート。添字が PPUnitAINode の接続口番号と対応する
        public IReadOnlyList<Port> OutputPorts => mOutputPorts;

        /* 生成 */

        // aNode : 表示対象のノード
        public PPUnitAITreeNodeView(PPUnitAINode aNode)
        {
            Node = aNode;
            title = aNode.NodeName;
            viewDataKey = aNode.NodeId;

            // 空白領域へのドロップでノード追加メニューを出すため、既定の Port ではなく自前のポートを使う
            InputPort = PPUnitAITreePort.Create(Orientation.Horizontal, Direction.Input, Port.Capacity.Single);
            InputPort.portName = "";
            inputContainer.Add(InputPort);

            var ports = aNode.Ports;
            for (int i = 0; i < ports.Count; i++)
            {
                var capacity = ports[i].IsMultiple ? Port.Capacity.Multi : Port.Capacity.Single;
                var port = PPUnitAITreePort.Create(Orientation.Horizontal, Direction.Output, capacity);
                port.portName = ports[i].Name;
                outputContainer.Add(port);
                mOutputPorts.Add(port);
            }

            SetupSummaryLabel();
            SetupIssueIcon();

            ApplyTitleColor(aNode);
            RefreshSummary();
            RefreshBorder();
            RefreshMutedStyle();
            RefreshExpandedState();
            RefreshPorts();

            style.left = aNode.GraphPosition.x;
            style.top = aNode.GraphPosition.y;
        }

        /* 初期化・接続 */

        // 要約ラベルをノードの本体側へ差し込む（ポートの並びと混ざらない位置に置く）
        private void SetupSummaryLabel()
        {
            mSummaryLabel.style.whiteSpace = WhiteSpace.Normal;
            mSummaryLabel.style.maxWidth = 220f;
            mSummaryLabel.style.paddingLeft = 6f;
            mSummaryLabel.style.paddingRight = 6f;
            mSummaryLabel.style.paddingTop = 2f;
            mSummaryLabel.style.paddingBottom = 2f;
            mSummaryLabel.style.fontSize = 10f;
            mSummaryLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
            // 背景を塗っておく
            // 塗らないとノードの後ろに敷いた注記の色が透けてしまい、要約が読みにくくなる
            mSummaryLabel.style.backgroundColor = SummaryBackgroundColor;
            mainContainer.Add(mSummaryLabel);
        }

        // 警告アイコンをタイトルバーの右上へ絶対位置で重ねる
        // グラフを見ただけで問題のある枝が分かるよう、ノード自身にも印を出す
        private void SetupIssueIcon()
        {
            mIssueIcon.image = EditorGUIUtility.IconContent("console.warnicon.sml").image;
            mIssueIcon.style.position = Position.Absolute;
            mIssueIcon.style.top = 2f;
            mIssueIcon.style.right = 2f;
            mIssueIcon.style.width = IssueIconSize;
            mIssueIcon.style.height = IssueIconSize;
            mIssueIcon.style.display = DisplayStyle.None;
            titleContainer.Add(mIssueIcon);
        }

        /* 操作 */

        // 診断で見つかった問題をノードへ反映する（タイトルバーのグレー表示と右上の警告アイコン）
        // アイコンは経路の強調やヒートマップと独立して出る
        public void SetIssues(IReadOnlyList<string> aMessages)
        {
            bool hasIssue = aMessages != null && aMessages.Count > 0;

            mIsIssue = hasIssue;
            mIssueIcon.style.display = hasIssue ? DisplayStyle.Flex : DisplayStyle.None;
            // 何が問題なのかはアイコンにぶら下げる。警告欄まで目を移さずに読めるようにするため
            mIssueIcon.tooltip = hasIssue ? string.Join("\n", aMessages) : "";
            RefreshTitleColor();
        }

        // 思考の経路として通過したノードを強調表示する
        // 確定した行動のノードは別の色にし、ノード全体を囲む太い枠でも示す
        public void SetHighlight(PPUnitAITreeHighlight aState)
        {
            mHighlight = aState;
            RefreshTitleColor();
            RefreshBorder();
        }

        // 通過回数に応じた濃淡を当てる
        // 種別色に依存しない固定の寒色→暖色の配色（青→赤）を使う
        public void SetHeat(float aRatio)
        {
            mHeatRatio = aRatio;
            tooltip = aRatio >= 0f ? $"通過率 : {Mathf.RoundToInt(Mathf.Clamp01(aRatio) * 100f)}%" : "";
            RefreshTitleColor();
        }

        // ノード種別に応じたタイトル色を当てる
        // 種別はノードを作り直さない限り変わらないため、生成時に 1 回だけ決める
        private void ApplyTitleColor(PPUnitAINode aNode)
        {
            Color color = aNode switch
            {
                PPUnitAISelectorNode => new Color(0.20f, 0.32f, 0.42f),
                PPUnitAISequenceNode => new Color(0.20f, 0.40f, 0.34f),
                PPUnitAILotteryNode => new Color(0.33f, 0.20f, 0.42f),
                PPUnitAIProbabilityNode => new Color(0.42f, 0.20f, 0.34f),
                PPUnitAIConditionNode => new Color(0.34f, 0.30f, 0.16f),
                PPUnitAILatchNode => new Color(0.40f, 0.26f, 0.14f),
                PPUnitAISearchNode => new Color(0.16f, 0.40f, 0.22f),
                PPUnitAISubTreeNode => new Color(0.16f, 0.36f, 0.44f),
                PPUnitAIActionNode => new Color(0.45f, 0.16f, 0.16f),
                _ => new Color(0.24f, 0.24f, 0.24f),
            };
            mTitleColor = color;
            titleContainer.style.backgroundColor = color;
        }

        // 枠線の太さと色をまとめて設定する
        private void SetBorder(float aWidth, Color aColor)
        {
            style.borderTopWidth = aWidth;
            style.borderBottomWidth = aWidth;
            style.borderLeftWidth = aWidth;
            style.borderRightWidth = aWidth;
            style.borderTopColor = aColor;
            style.borderBottomColor = aColor;
            style.borderLeftColor = aColor;
            style.borderRightColor = aColor;
        }

        /* 表示 */

        // 表示名と各種の見た目をノードの現在値へ更新する
        // インスペクタでノード名・割り込み指定・評価除外を編集した直後に呼ぶ
        public void RefreshView(bool aIsRoot)
        {
            title = aIsRoot ? $"★ {Node.NodeName}" : Node.NodeName;
            RefreshSummary();
            RefreshBorder();
            RefreshMutedStyle();
        }

        // 枠線を現在の状態へ合わせる。優先順は「経路強調 > 割り込み指定」
        // 経路強調はノード全体を太い枠で囲む。対象外に戻った場合は枠を消す
        private void RefreshBorder()
        {
            if (mHighlight != PPUnitAITreeHighlight.None)
            {
                var color = mHighlight == PPUnitAITreeHighlight.Decided
                    ? new Color(1f, 0.85f, 0.1f)
                    : new Color(0.25f, 0.75f, 1f);
                SetBorder(4f, color);
                return;
            }

            if (Node.IsInterrupt)
            {
                SetBorder(2f, new Color(0.95f, 0.65f, 0.20f));
                return;
            }

            SetBorder(0f, Color.clear);
        }

        // 評価から外されているノードを半透明にする
        // 接続線は残したまま「今は通らない枝」であることを見分けられるようにする
        private void RefreshMutedStyle()
        {
            style.opacity = Node.IsMuted ? 0.35f : 1f;
        }

        // 診断・濃淡・経路強調の 3 状態からタイトル色を確定させる
        // 優先順は「経路強調 > 濃淡 > 診断 > 種別の色」
        private void RefreshTitleColor()
        {
            if (mHighlight != PPUnitAITreeHighlight.None)
            {
                titleContainer.style.backgroundColor = mHighlight switch
                {
                    PPUnitAITreeHighlight.Decided => new Color(0.95f, 0.75f, 0.15f),
                    PPUnitAITreeHighlight.Passed => new Color(0.30f, 0.60f, 0.85f),
                    _ => mTitleColor,
                };
                return;
            }

            if (mHeatRatio >= 0f)
            {
                float t = Mathf.Clamp01(mHeatRatio);
                titleContainer.style.backgroundColor = Color.Lerp(HeatColorCold, HeatColorHot, t);
                return;
            }

            titleContainer.style.backgroundColor = mIsIssue ? new Color(0.30f, 0.30f, 0.30f) : mTitleColor;
        }

        /* 説明文 */

        // 要約の表示をノードの現在の設定へ合わせる
        // 何も返さないノード種別ではラベルごと畳み、余白が空くのを防ぐ
        private void RefreshSummary()
        {
            string summary = Node.Summary;
            bool hasSummary = !string.IsNullOrEmpty(summary);

            mSummaryLabel.text = summary;
            mSummaryLabel.style.display = hasSummary ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
