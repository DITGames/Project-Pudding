/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattlePauseMenuView.cs
 * @author hqrse
 * @date 2026/11/22
 * @brief バトル中に開くメニュー
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;
using AttributeUtility;
using CustomConsole;

namespace PPCore
{
    // バトル中に開くメニュー。つづける・キー設定・AI モード・リタイア・ゲーム終了を選ぶ
    // 開いている間はバトルの時計と物理を止める。キー設定やダイアログを開いている間は、メニューの入力を止める
    public class PPBattlePauseMenuView : PPStageListMenuBase
    {
        /* 定数 */

        private const string LogTag = "Battle";
        private const int ResumeRow = 0;
        private const int ConfigRow = 1;
        private const int AIModeRow = 2;
        private const int RetireRow = 3;
        private const int QuitRow = 4;
        private const int RowCount = 5;
        private const string QuitMessage = "ゲームを終了しますか？\n進行状況は保存されません";

        /* インスペクタ設定 */

        [Header("項目")]
        [Label("行のプレハブ")]
        [SerializeField] private PPStageListItem mItemPrefab;
        [Label("キー設定画面")]
        [SerializeField] private PPInputConfigMenuView mConfigMenu;
        [Label("確認のダイアログ")]
        [SerializeField] private PPConfirmDialogView mConfirmDialog;
        // キー設定を開いている間にメニューを見えなくするのに使う。未設定なら隠さない
        [Label("メニュー全体の透明度")]
        [SerializeField] private CanvasGroup mCanvasGroup;

        // Build Profiles に登録したシーンの名前
        [Header("シーン")]
        [Label("リタイアで戻るシーン名")]
        [SerializeField] private string mTitleSceneName = "S_Title";

        /* 状態 */

        private BattleManager mManager;
        private List<PPStageListItem> mItems;
        // リタイアでシーンを切り替え始めたか
        private bool mIsLeaving;
        // AI モードの行に出す、今の型の名前を返す
        private Func<string> mGetAIModeName;
        private Action mSwitchAIMode;

        /* Unity イベント */

        // キー設定やダイアログを開いている間と、それらを閉じたフレームはメニューの入力を受け付けない
        // キー設定は閉じるときに時間の流れを戻すため、メニューを開いている間は毎フレーム止め直す
        protected override void Update()
        {
            // リタイアでシーンを切り替えている間は、戻した時間の流れを止め直さない
            if (mIsLeaving) return;

            bool isConfigOpen = mConfigMenu != null && mConfigMenu.IsOpen;
            if (IsOpen && !isConfigOpen) Time.timeScale = 0f;
            // キー設定はメニューより奥の Canvas にあるため、開いている間はメニューを見えなくする
            if (mCanvasGroup != null) mCanvasGroup.alpha = isConfigOpen ? 0f : 1f;

            int frame = Time.frameCount;
            if (mConfigMenu != null && (mConfigMenu.IsOpen || mConfigMenu.ClosedFrame == frame)) return;
            if (mConfirmDialog != null && (mConfirmDialog.IsOpen || mConfirmDialog.ClosedFrame == frame)) return;
            base.Update();
        }

        /* 操作 */

        // メニューを開き、バトルの時計と物理を止める
        public void Open(BattleManager aManager, Func<string> aGetAIModeName, Action aSwitchAIMode)
        {
            if (IsOpen) return;
            mManager = aManager;
            mGetAIModeName = aGetAIModeName;
            mSwitchAIMode = aSwitchAIMode;
            mManager?.Context?.Clock.Pause();
            Time.timeScale = 0f;

            OpenPanel();
            mItems = PrepareItems(0, mItemPrefab, RowCount);
            mItems[ResumeRow].SetText("つづける");
            mItems[ConfigRow].SetText("キー設定");
            mItems[RetireRow].SetText("リタイア");
            mItems[QuitRow].SetText("ゲーム終了");
            RefreshAIMode(mItems[AIModeRow]);
            Focus(0, ResumeRow);
        }

        // メニューを閉じ、止めていた時計と物理を戻す
        public override void Close()
        {
            if (!IsOpen) return;
            base.Close();
            mManager?.Context?.Clock.Resume();
            Time.timeScale = 1f;
        }

        // 項目を選ぶ。決定の入力と同じ処理で、検証から直接呼ぶこともできる
        public void Select(int aIndex)
        {
            switch (aIndex)
            {
                case ResumeRow:
                    Close();
                    break;
                case ConfigRow:
                    if (mConfigMenu != null) mConfigMenu.Open();
                    break;
                case AIModeRow:
                    mSwitchAIMode?.Invoke();
                    RefreshAIMode(mItems[AIModeRow]);
                    break;
                case RetireRow:
                    Retire();
                    break;
                case QuitRow:
                    if (mConfirmDialog != null) mConfirmDialog.Open(QuitMessage, Quit, null);
                    else Quit();
                    break;
            }
        }

        protected override void OnSubmit(int aGroup, int aIndex) => Select(aIndex);

        // キャンセルは「つづける」と同じ
        protected override void OnCancel() => Close();

        // ステージの進行を捨ててタイトルへ戻る。切り替わるまでメニューは開いたままにして、バトルの入力を止めておく
        private void Retire()
        {
            CustomConsoleLog.Log(LogTag, $"リタイア: {mTitleSceneName} へ戻る");
            PPStageProgress.Clear();
            PPStageStartSelection.Clear();
            // 次のシーンでプッシャーの物理が止まったままにならないよう、時間の流れを戻してから移る
            mIsLeaving = true;
            Time.timeScale = 1f;
            PPSceneTransition.Load(mTitleSceneName);
        }

        // アプリを終了する。Editor では Play を止める
        private static void Quit()
        {
            CustomConsoleLog.Log(LogTag, "ゲーム終了");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /* 表示 */

        private void RefreshAIMode(PPStageListItem aItem)
        {
            string name = mGetAIModeName != null ? mGetAIModeName() : "-";
            aItem.SetText($"AIモード：{name}");
        }
    }
}
