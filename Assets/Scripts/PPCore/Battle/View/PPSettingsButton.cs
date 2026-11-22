/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPSettingsButton.cs
 * @author hqrse
 * @date 2026/11/23
 * @brief 設定画面のボタン 1 つ分の表示
 * =====================================*/

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AttributeUtility;

namespace PPCore
{
    // 設定画面のタブ・切り替え・割り当て・値・下のボタンに使う角丸のボタン
    // 開いているタブや選んでいる切り替えはグラデーションで塗り、カーソルのあるボタンは枠で囲む
    public class PPSettingsButton : MonoBehaviour
    {
        /* インスペクタ設定 */

        [Label("背景")]
        [SerializeField] private Image mBackground;
        // 選んでいるときと入力待ちのときに表示する塗り
        [Label("選んでいるときの塗り")]
        [SerializeField] private Image mSelectedFill;
        // カーソルがあるときに表示する枠
        [Label("カーソルの枠")]
        [SerializeField] private Image mFocusFrame;
        [Label("文字")]
        [SerializeField] private TMP_Text mLabel;
        [Label("通常の文字の色")]
        [SerializeField] private Color mTextColor = new(0.92f, 0.94f, 0.98f, 1f);
        // 未割り当て(なし)や開いていないタブの文字の色
        [Label("薄い文字の色")]
        [SerializeField] private Color mDimTextColor = new(0.55f, 0.6f, 0.7f, 1f);
        // 塗りの上に載せる文字の色
        [Label("選んでいるときの文字の色")]
        [SerializeField] private Color mSelectedTextColor = new(0.23f, 0.16f, 0.12f, 1f);

        /* 状態 */

        private bool mIsSelected;
        private bool mIsDim;

        /* プロパティ */

        public string Text => mLabel != null ? mLabel.text : string.Empty;

        /* 表示 */

        // 文字を変える。薄くすると、選んでいないときに文字を薄い色で出す
        public void SetText(string aText, bool aIsDim = false)
        {
            if (mLabel != null) mLabel.text = aText;
            mIsDim = aIsDim;
            RefreshText();
        }

        // 開いているタブ・選んでいる切り替え・入力待ちをグラデーションで塗る
        public void SetSelected(bool aIsSelected)
        {
            mIsSelected = aIsSelected;
            if (mSelectedFill != null) mSelectedFill.enabled = aIsSelected;
            RefreshText();
        }

        public void SetFocused(bool aIsFocused)
        {
            if (mFocusFrame != null) mFocusFrame.enabled = aIsFocused;
        }

        private void RefreshText()
        {
            if (mLabel == null) return;
            mLabel.color = mIsSelected ? mSelectedTextColor : (mIsDim ? mDimTextColor : mTextColor);
        }
    }
}
