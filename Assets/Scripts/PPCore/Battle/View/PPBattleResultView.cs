/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleResultView.cs
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
    // ステージのバトルで負けたときと、ボスに勝ってクリアしたときに出す結果の画面。ステージを通さずに Play したバトルでも決着時に出す
    // 項目は「タイトルへ戻る」だけで、決定でタイトルのシーンへ移る。キャンセルでは閉じない
    public class PPBattleResultView : PPMenuListBase
    {
        /* 定数 */

        private const string LogTag = "StageMap";
        private const string BackText = "タイトルへ戻る";

        /* インスペクタ設定 */

        [Header("項目")]
        [Label("行のプレハブ")]
        [SerializeField] private PPMenuListItem mItemPrefab;
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
        public void Open(PPMapBattleOutcome aOutcome, BattleResultType aResult, string aStageName, int aFloor)
        {
            if (IsOpen) return;
            string message = aOutcome == PPMapBattleOutcome.Cleared
                ? $"{aStageName} をクリアした"
                : $"{aStageName} の {aFloor} 段目で力尽きた";
            Show(GetTitle(aOutcome, aResult), message);
            CustomConsoleLog.Log(LogTag, $"結果を表示: {aOutcome} ({aResult}) {aStageName} {aFloor}段");
        }

        // ステージを通さずに Play したバトルの結果を出す。敵の拠点を壊せばクリア、味方の拠点が壊れたらゲームオーバー
        public void OpenBattleResult(BattleResultType aResult)
        {
            if (IsOpen) return;
            var (title, message) = aResult switch
            {
                BattleResultType.Victory => ("クリア", "敵の拠点を壊した"),
                BattleResultType.Defeat => ("ゲームオーバー", "味方の拠点が壊された"),
                BattleResultType.Draw => ("引き分け", "両方の拠点が壊れた"),
                _ => ("撤退", "バトルから退いた"),
            };
            Show(title, message);
            CustomConsoleLog.Log(LogTag, $"結果を表示: {aResult}");
        }

        // タイトルへ戻る。決定の入力と同じ処理で、検証から直接呼ぶこともできる
        public void Select(int aIndex)
        {
            if (!IsOpen || mIsLeaving) return;
            CustomConsoleLog.Log(LogTag, $"結果の画面から {mTitleSceneName} へ戻る");
            mIsLeaving = true;
            PPMapProgress.Clear();
            PPMapStartSelection.Clear();
            // 次のシーンでプッシャーの物理が止まったままにならないよう、時間の流れを戻してから移る
            Time.timeScale = 1f;
            // 切り替えを受け付けられなかったときは、もう一度決定できるようにする
            if (!PPSceneTransition.Load(mTitleSceneName)) mIsLeaving = false;
        }

        protected override void OnSubmit(int aGroup, int aIndex) => Select(aIndex);

        /* 表示 */

        // 見出しと文を差し替えて、「タイトルへ戻る」だけの画面を開く
        private void Show(string aTitle, string aMessage)
        {
            mIsLeaving = false;
            if (mTitleText != null) mTitleText.text = aTitle;
            if (mMessageText != null) mMessageText.text = aMessage;

            OpenPanel();
            var items = PrepareItems(0, mItemPrefab, 1);
            items[0].SetText(BackText);
            Focus(0, 0);
        }

        // 結果を選ばずに閉じないよう、キャンセルでは何もしない
        protected override void OnCancel() { }

        /* 説明文 */

        private static string GetTitle(PPMapBattleOutcome aOutcome, BattleResultType aResult)
        {
            if (aOutcome == PPMapBattleOutcome.Cleared) return "ステージクリア";
            return aResult switch
            {
                BattleResultType.Draw => "引き分け",
                BattleResultType.Escaped => "撤退",
                _ => "敗北",
            };
        }
    }
}
