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
    public class PPTitleMenuView : PPStageListMenuBase
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
        [SerializeField] private PPStageListItem mItemPrefab;
        [Label("キー設定画面")]
        [SerializeField] private PPInputConfigMenuView mConfigMenu;

        // Build Profiles に登録したシーンの名前
        [Header("シーン")]
        [Label("はじめからで開くシーン名")]
        [SerializeField] private string mNewGameSceneName = "S_StageMap";

        /* Unity イベント */

        private void Start() => Open();

        // キー設定画面を開いている間と、それを閉じたフレームはメニューの入力を受け付けない
        protected override void Update()
        {
            if (mConfigMenu != null && (mConfigMenu.IsOpen || mConfigMenu.ClosedFrame == Time.frameCount)) return;
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
                    PPStageStartSelection.Begin();
                    CustomConsoleLog.Log(LogTag, $"はじめから: {mNewGameSceneName} へ進む");
                    return PPSceneTransition.Load(mNewGameSceneName);
                case ContinueRow:
                    // 復元はセーブの仕組みと一緒に作る。それまでは選べない
                    return false;
                case ConfigRow:
                    if (mConfigMenu == null) return false;
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

        /* 問い合わせ */

        // セーブの判定ができるまでは、常にセーブデータがないものとする
        protected virtual bool HasSaveData() => false;
    }
}
