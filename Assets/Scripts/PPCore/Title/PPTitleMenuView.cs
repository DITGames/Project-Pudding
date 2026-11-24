/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPTitleMenuView.cs
 * @author hqrse
 * @date 2026/11/22
 * @brief タイトル画面のメニュー
 * =====================================*/

using UnityEngine;
using AttributeUtility;
using CustomConsole;

namespace PPCore
{
    // タイトルで開くメニュー。はじめから・つづきから・設定・終了を選ぶ
    public class PPTitleMenuView : PPMenuListBase
    {
        /* 定数 */

        private const string LogTag = "Title";
        private const int NewGameRow = 0;
        private const int ContinueRow = 1;
        private const int ConfigRow = 2;
        private const int QuitRow = 3;
        private static readonly string[] Texts = { "はじめから", "つづきから", "設定", "終了" };

        /* インスペクタ設定 */

        [Header("項目")]
        [Label("行のプレハブ")]
        [SerializeField] private PPMenuListItem mItemPrefab;
        [Label("設定画面")]
        [SerializeField] private PPInputConfigMenuView mConfigMenu;
        // 未設定なら、ユニットを選ばずにマップ定義の初期パーティで始める
        [Label("初期ユニット選択画面")]
        [SerializeField] private PPInitialUnitSelectView mUnitSelect;
        // 未設定なら、ステージを選ばずにマップのシーンに設定されたマップ定義で始める
        [Label("ステージ選択画面")]
        [SerializeField] private PPMapSelectView mStageSelect;

        // Build Profiles に登録したシーンの名前
        [Header("シーン")]
        [Label("ユニットとステージを選んだ後に開くシーン名")]
        [SerializeField] private string mNewGameSceneName = "S_Map";

        /* 状態 */

        // 設定画面を開くためにメニューを閉じているか
        private bool mIsInConfig;

        /* Unity イベント */

        private void Start() => Open();

        // 設定画面を開いている間と、それやユニット・ステージの選択画面を閉じたフレームはメニューの入力を受け付けない
        // 設定画面を閉じたら、閉じておいたメニューを開き直す
        protected override void Update()
        {
            int frame = Time.frameCount;
            if (mIsInConfig && mConfigMenu != null && !mConfigMenu.IsOpen)
            {
                mIsInConfig = false;
                Open();
                Focus(0, ConfigRow);
            }
            if (mConfigMenu != null && (mConfigMenu.IsOpen || mConfigMenu.ClosedFrame == frame)) return;
            if (mUnitSelect != null && mUnitSelect.ClosedFrame == frame) return;
            if (mStageSelect != null && mStageSelect.ClosedFrame == frame) return;
            base.Update();
        }

        /* 操作 */

        public void Open()
        {
            OpenPanel();
            var items = PrepareItems(0, mItemPrefab, Texts.Length);
            for (int i = 0; i < items.Count; i++)
            {
                items[i].SetText(Texts[i]);
                items[i].SetInteractable(i != ContinueRow || HasSaveData());
            }
            Focus(0, 0);
        }

        // 項目を選ぶ。検証から直接呼ぶときにも使う。選べない項目なら false
        public bool Select(int aIndex)
        {
            switch (aIndex)
            {
                case NewGameRow:
                    return StartNewGame();
                case ContinueRow:
                    // 復元はセーブの仕組みと一緒に作る。それまでは選べない
                    return false;
                case ConfigRow:
                    if (mConfigMenu == null) return false;
                    // 設定画面の後ろに透けて見えないよう、開いている間はメニューを閉じておく
                    Close();
                    mIsInConfig = true;
                    mConfigMenu.Open();
                    return true;
                case QuitRow:
                    Quit();
                    return true;
                default:
                    return false;
            }
        }

        protected override void OnSubmit(int aGroup, int aIndex) => Select(aIndex);

        // タイトルより前の画面はないため、キャンセルでは閉じない
        protected override void OnCancel() { }

        // メニューを閉じ、初期ユニット → ステージの順に選ばせてから次のシーンへ進む
        // 選択画面のキャンセルでは 1 つ前の画面へ戻る。未設定の選択画面は飛ばす
        private bool StartNewGame()
        {
            PPMapStartSelection.Begin();
            if (mUnitSelect == null && mStageSelect == null) return LoadNewGameScene();

            Close();
            bool isOpened = mUnitSelect != null ? OpenUnitSelect() : OpenStageSelect();
            if (!isOpened) Open();
            return isOpened;
        }

        private bool OpenUnitSelect() => mUnitSelect.Open(HandleUnitPicked, Open);

        private bool OpenStageSelect() => mStageSelect.Open(HandleStagePicked, mUnitSelect != null ? ReturnToUnitSelect : Open);

        private void ReturnToUnitSelect()
        {
            if (!OpenUnitSelect()) Open();
        }

        private bool LoadNewGameScene()
        {
            CustomConsoleLog.Log(LogTag, $"はじめから: {mNewGameSceneName} へ進む");
            return PPSceneTransition.Load(mNewGameSceneName);
        }

        /* 問い合わせ */

        // セーブの判定ができるまでは、常にセーブデータがないものとする
        protected virtual bool HasSaveData() => false;

        /* イベントハンドラ */

        // 選んだユニットを入れ物に入れ、ステージを選ばせる。ステージ選択が無ければそのまま次のシーンへ進む
        private void HandleUnitPicked(PPInitialPartyMember aPicked)
        {
            var definition = mUnitSelect.Definition;
            PPMapStartSelection.Current.SetInitialUnits(definition.CreateUnits(aPicked));
            if (mStageSelect == null || !OpenStageSelect()) LoadNewGameScene();
        }

        // 選んだステージとそのシードを入れ物に入れ、次のシーンへ進む
        private void HandleStagePicked(PPMapDefinition aMap)
        {
            PPMapStartSelection.Current.SetStage(aMap, aMap.ResolveSeed());
            LoadNewGameScene();
        }

        /* ヘルパー */

        // アプリを終了する。Editor では Play を止める
        private static void Quit()
        {
            CustomConsoleLog.Log(LogTag, "終了");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
