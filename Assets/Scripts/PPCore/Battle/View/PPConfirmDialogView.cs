/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPConfirmDialogView.cs
 * @author hqrse
 * @date 2026/11/22
 * @brief はい / いいえ で確かめるダイアログ
 * =====================================*/

using System;
using TMPro;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // 文を出し、「はい」「いいえ」を選ばせる。キャンセルは「いいえ」と同じ。最初は「いいえ」を選んでおく
    public class PPConfirmDialogView : PPStageListMenuBase
    {
        /* 定数 */

        private const int YesRow = 0;
        private const int NoRow = 1;
        private static readonly string[] Texts = { "はい", "いいえ" };

        /* インスペクタ設定 */

        [Header("項目")]
        [Label("行のプレハブ")]
        [SerializeField] private PPStageListItem mItemPrefab;
        [Label("文")]
        [SerializeField] private TMP_Text mMessageText;

        /* 状態 */

        private Action mOnYes;
        private Action mOnNo;

        /* 操作 */

        public void Open(string aMessage, Action aOnYes, Action aOnNo)
        {
            mOnYes = aOnYes;
            mOnNo = aOnNo;
            if (mMessageText != null) mMessageText.text = aMessage;

            OpenPanel();
            var items = PrepareItems(0, mItemPrefab, Texts.Length);
            for (int i = 0; i < items.Count; i++) items[i].SetText(Texts[i]);
            Focus(0, NoRow);
        }

        // 番号で選ぶ。決定の入力と同じ処理で、検証から直接呼ぶこともできる
        public void Choose(int aIndex)
        {
            if (!IsOpen) return;

            var callback = aIndex == YesRow ? mOnYes : mOnNo;
            mOnYes = null;
            mOnNo = null;
            Close();
            callback?.Invoke();
        }

        protected override void OnSubmit(int aGroup, int aIndex) => Choose(aIndex);

        protected override void OnCancel() => Choose(NoRow);
    }
}
