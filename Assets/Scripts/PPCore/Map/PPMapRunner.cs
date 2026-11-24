/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPMapRunner.cs
 * @author hqrse
 * @date 2026/11/21
 * @brief ステージマップのシーンのエントリポイント
 * =====================================*/

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using AttributeUtility;
using CustomConsole;

namespace PPCore
{
    // 進行状態からマップを作って表示し、マスの選択・メニュー・編成画面・マスのイベントをつなぐ
    // 選んだ内容があればそれで、進行状態も無ければマップ定義から新しく始め、バトルから戻ったときは持ち越した状態でマップを作り直す
    public class PPMapRunner : MonoBehaviour
    {
        /* 定数 */

        private const string LogTag = "StageMap";
        // スティックをどこまで倒したら 1 回の移動とみなすか(0〜1)
        private const float NavigateThreshold = 0.5f;

        /* インスペクタ設定 */

        [Header("ステージ")]
        // タイトルで選んだ内容が無いとき（このシーンを単体で Play したときなど）に使う
        [Label("マップ定義")]
        [SerializeField] private PPMapDefinition mDefinition;
        // Build Settings に登録したシーンの名前
        [Label("バトルのシーン名")]
        [SerializeField] private string mBattleSceneName = "S_Battle";

        [Header("表示")]
        [Label("マップ")]
        [SerializeField] private PPMapView mMapView;
        [Label("メニュー")]
        [SerializeField] private PPMapMenuView mMenuView;
        [Label("編成画面")]
        [SerializeField] private PPMapFormationView mFormationView;
        [Label("ユニット獲得画面")]
        [SerializeField] private PPMapRecruitView mRecruitView;
        // マスに入れなかった理由やイベントの結果を出す。未設定なら出さない
        [Label("お知らせ")]
        [SerializeField] private TMP_Text mMessageText;

        // キーボードやゲームパッドで選んでいるマスの種類と中身。未設定なら出さない
        [Header("選択中のマス")]
        [Label("見出し")]
        [SerializeField] private TMP_Text mNodeInfoTitle;
        [Label("中身")]
        [SerializeField] private TMP_Text mNodeInfoBody;

        [Header("入力")]
        // 左右で選べるマスを切り替える
        [Label("移動の入力")]
        [SerializeField] private InputActionReference mNavigateAction;
        [Label("決定の入力")]
        [SerializeField] private InputActionReference mSubmitAction;
        [Label("メニューを開く入力")]
        [SerializeField] private InputActionReference mMenuAction;
        // 編成画面を開く。開いている間にもう一度押すと閉じる
        [Label("編成を開く入力")]
        [SerializeField] private InputActionReference mFormationAction;

        /* 状態 */

        private PPMap mMap;
        // キーボードやゲームパッドで選んでいるマス
        private PPMapNode mFocused;
        private int mLastNavigate;

        /* プロパティ */

        public PPMap Map => mMap;
        public PPMapProgress Progress => PPMapProgress.Current;
        // マスのイベントやメニューの途中か、シーンを切り替えている途中なら true
        public bool IsBusy => mMenuView.IsOpen || mFormationView.IsOpen || mRecruitView.IsOpen || PPSceneTransition.IsLoading;

        /* Unity イベント */

        // 選んだ内容があればそれで新しく始め、無ければ持ち越した進行状態を使う。どちらも無ければインスペクタのマップ定義で始める
        // 選んだ内容にマップが無ければ、インスペクタのマップ定義を使う
        void Start()
        {
            var progress = PPMapProgress.Current;
            var selection = PPMapStartSelection.Current;
            if (selection != null)
            {
                var definition = selection.Definition != null ? selection.Definition : mDefinition;
                progress = PPMapProgress.Begin(definition, selection.Seed ?? definition.ResolveSeed(), selection.InitialUnits);
                PPMapStartSelection.Clear();
            }
            else if (progress == null)
            {
                progress = PPMapProgress.Begin(mDefinition, mDefinition.ResolveSeed());
            }

            foreach (var action in new[] { mNavigateAction, mSubmitAction, mMenuAction, mFormationAction })
            {
                if (action != null) action.action.Enable();
            }

            mMap = PPMapGenerator.Generate(progress.Definition, progress.Seed);
            // 始点は選ばずに到達済みのマスとし、最初からその次の段を選べるようにする
            if (!progress.CurrentNode.HasValue && mMap.Start != null) progress.CompleteNode(mMap.Start.Key);
            mMapView.OnNodeClicked += aNode => SelectNode(aNode);
            mMapView.Build(mMap);
            mFocused = FirstSelectable();
            RefreshMap();
            mMapView.ScrollTo(mFocused ?? CurrentMapNode());
            SetMessage(null);
        }

        // メニューや画面を開いている間と、それらを閉じたフレームはマップの入力を受け付けない
        // 編成画面だけは、開いた入力をもう一度押しても閉じられるようにする
        void Update()
        {
            if (mFormationView.IsOpen && WasPressed(mFormationAction))
            {
                mFormationView.Close();
                return;
            }
            if (IsBusy || IsClosedThisFrame()) return;

            int navigate = ReadNavigate();
            if (navigate != 0 && navigate != mLastNavigate) MoveFocus(navigate);
            mLastNavigate = navigate;

            if (WasPressed(mSubmitAction) && mFocused != null) SelectNode(mFocused);
            else if (WasPressed(mFormationAction)) OpenFormation();
            else if (WasPressed(mMenuAction)) mMenuView.Open();
        }

        /* 操作 */

        // 段と列でマスを選ぶ。検証から直接呼ぶときに使う
        public bool SelectNode(int aFloor, int aColumn) => SelectNode(mMap.Get(new Vector2Int(aFloor, aColumn)));

        // マスを選び、種類に応じたイベントを始める。選べないマスや、入れない理由があるマスなら false
        public bool SelectNode(PPMapNode aNode)
        {
            if (aNode == null || IsBusy || !IsSelectable(aNode)) return false;

            var progress = PPMapProgress.Current;
            var stageEvent = PPMapEvents.Resolve(aNode.Type);
            string reason = stageEvent.GetBlockReason(progress, aNode);
            if (reason != null)
            {
                SetMessage(reason);
                return false;
            }

            CustomConsoleLog.Log(LogTag, $"マスを選択: {aNode.Floor}段 {aNode.Column}列 {aNode.Type} バトル定義={(aNode.Battle != null ? aNode.Battle.name : "-")}");
            SetMessage(null);
            stageEvent.Begin(new PPMapEventContext
            {
                Progress = progress,
                Node = aNode,
                BattleSceneName = mBattleSceneName,
                RecruitView = mRecruitView,
                OnFinished = aMessage => HandleEventFinished(aNode, aMessage),
            });
            return true;
        }

        public void OpenFormation() => mFormationView.Open(PPMapProgress.Current, RefreshMap);

        // 左右の入力で、選べるマスを列の順に切り替える
        private void MoveFocus(int aDirection)
        {
            var candidates = SelectableNodes();
            if (candidates.Count == 0) return;

            int index = candidates.IndexOf(mFocused);
            index = index < 0 ? 0 : Mathf.Clamp(index + aDirection, 0, candidates.Count - 1);
            mFocused = candidates[index];
            RefreshMap();
        }

        /* 問い合わせ */

        // 現在地の次の段でつながっているマスだけ選べる。現在地が無い(始点の無いマップ)ときは最下段を選べる
        public bool IsSelectable(PPMapNode aNode)
        {
            var current = CurrentMapNode();
            if (current == null) return aNode.Floor == 0;
            return current.Next.Contains(aNode);
        }

        private PPMapNode CurrentMapNode()
        {
            var key = PPMapProgress.Current?.CurrentNode;
            return key.HasValue ? mMap.Get(key.Value) : null;
        }

        private List<PPMapNode> SelectableNodes()
        {
            var list = new List<PPMapNode>();
            foreach (var node in mMap.AllNodesWithBoss())
            {
                if (IsSelectable(node)) list.Add(node);
            }
            list.Sort((a, b) => a.Column.CompareTo(b.Column));
            return list;
        }

        private PPMapNode FirstSelectable()
        {
            var list = SelectableNodes();
            return list.Count > 0 ? list[0] : null;
        }

        private bool IsClosedThisFrame()
        {
            int frame = Time.frameCount;
            return mMenuView.ClosedFrame == frame || mFormationView.ClosedFrame == frame || mRecruitView.ClosedFrame == frame;
        }

        private int ReadNavigate()
        {
            if (mNavigateAction == null) return 0;

            float x = mNavigateAction.action.ReadValue<Vector2>().x;
            return x > NavigateThreshold ? 1 : x < -NavigateThreshold ? -1 : 0;
        }

        private static bool WasPressed(InputActionReference aAction) => aAction != null && aAction.action.WasPressedThisFrame();

        /* 表示 */

        // マップの状態に合わせて、マスと選択中のマスの表示を書き直す
        private void RefreshMap()
        {
            mMapView.Refresh(PPMapProgress.Current, IsSelectable, mFocused);
            RefreshNodeInfo();
        }

        // 選んでいるマスの種類と、そのマスで起きることを出す。バトルのマスでも敵の中身は見せない
        private void RefreshNodeInfo()
        {
            if (mNodeInfoTitle != null) mNodeInfoTitle.text = mFocused != null ? mMapView.GetTypeName(mFocused.Type) : string.Empty;
            if (mNodeInfoBody == null) return;
            mNodeInfoBody.text = mFocused == null ? string.Empty : mFocused.Type switch
            {
                PPMapNodeType.Battle => "敵と戦う",
                PPMapNodeType.Elite => "強い敵と戦う",
                PPMapNodeType.Boss => "ボスと戦う",
                PPMapNodeType.Upgrade => "所持ユニットのうち、一番レベルの低い 1 体のレベルが 1 上がる",
                PPMapNodeType.Recruit => "候補から 1 体を選んで仲間にする",
                _ => string.Empty,
            };
        }

        private void SetMessage(string aMessage)
        {
            if (mMessageText != null) mMessageText.text = aMessage ?? string.Empty;
        }

        /* イベントハンドラ */

        // その場で終わるイベントの後に、現在地を進めて次の段のマスを選べるようにする
        private void HandleEventFinished(PPMapNode aNode, string aMessage)
        {
            PPMapProgress.Current.CompleteNode(aNode.Key);
            mFocused = FirstSelectable();
            RefreshMap();
            mMapView.ScrollTo(mFocused ?? aNode);
            SetMessage(aMessage);
        }
    }
}
