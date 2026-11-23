/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageResultView.cs
 * @author hqrse
 * @date 2026/11/23
 * @brief ステージの敗北・クリアを出してタイトルへ戻る画面
 * =====================================*/

using TMPro;
using UnityEngine;
using AttributeUtility;
using CustomConsole;

namespace PPCore
{
    // ステージのバトルで負けたときと、ボスに勝ってクリアしたときに出す結果の画面
    // 項目は「タイトルへ戻る」だけで、決定でタイトルのシーンへ移る。キャンセルでは閉じない
    public class PPStageResultView : PPStageListMenuBase
    {
        /* 定数 */

        private const string LogTag = "StageMap";
        private const string BackText = "タイトルへ戻る";

        /* インスペクタ設定 */

        [Header("項目")]
        [Label("行のプレハブ")]
        [SerializeField] private PPStageListItem mItemPrefab;
        [Label("見出し")]
        [SerializeField] private TMP_Text mTitleText;
        // 見出しの下に出す一文。未設定なら出さない
        [Label("文")]
        [SerializeField] private TMP_Text mMessageText;

        // Build Profiles に登録したシーンの名前
        [Header("シーン")]
        [Label("戻るシーン名")]
        [SerializeField] private string mTitleSceneName = "S_Title";

        /* 状態 */

        // タイトルへの切り替えを始めたか
        private bool mIsLeaving;

        /* 操作 */

        // 結果を出す。aFloor はバトルのマスの段(最下段の始点が 0)
        public void Open(PPStageBattleOutcome aOutcome, BattleResultType aResult, string aStageName, int aFloor)
        {
            if (IsOpen) return;
            mIsLeaving = false;
            if (mTitleText != null) mTitleText.text = GetTitle(aOutcome, aResult);
            if (mMessageText != null) mMessageText.text = aOutcome == PPStageBattleOutcome.Cleared
                ? $"{aStageName} をクリアした"
                : $"{aStageName} の {aFloor} 段目で力尽きた";

            OpenPanel();
            var items = PrepareItems(0, mItemPrefab, 1);
            items[0].SetText(BackText);
            Focus(0, 0);
            CustomConsoleLog.Log(LogTag, $"結果を表示: {aOutcome} ({aResult}) {aStageName} {aFloor}段");
        }

        // タイトルへ戻る。決定の入力と同じ処理で、検証から直接呼ぶこともできる
        public void Select(int aIndex)
        {
            if (!IsOpen || mIsLeaving) return;
            CustomConsoleLog.Log(LogTag, $"結果の画面から {mTitleSceneName} へ戻る");
            mIsLeaving = true;
            PPStageProgress.Clear();
            PPStageStartSelection.Clear();
            // 次のシーンでプッシャーの物理が止まったままにならないよう、時間の流れを戻してから移る
            Time.timeScale = 1f;
            // 切り替えを受け付けられなかったときは、もう一度決定できるようにする
            if (!PPSceneTransition.Load(mTitleSceneName)) mIsLeaving = false;
        }

        protected override void OnSubmit(int aGroup, int aIndex) => Select(aIndex);

        // 結果を選ばずに閉じないよう、キャンセルでは何もしない
        protected override void OnCancel() { }

        /* 説明文 */

        private static string GetTitle(PPStageBattleOutcome aOutcome, BattleResultType aResult)
        {
            if (aOutcome == PPStageBattleOutcome.Cleared) return "ステージクリア";
            return aResult switch
            {
                BattleResultType.Draw => "引き分け",
                BattleResultType.Escaped => "撤退",
                _ => "敗北",
            };
        }
    }
}
