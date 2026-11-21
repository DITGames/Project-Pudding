/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageListItem.cs
 * @author hqrse
 * @date 2026/11/21
 * @brief ステージセレクトのメニューに並べる 1 行
 * =====================================*/

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AttributeUtility;

namespace PPCore
{
    // 文字と背景だけの 1 行。選択中と、操作の途中で押さえている行を色で見分ける
    public class PPStageListItem : MonoBehaviour, IPPStageFocusable
    {
        /* インスペクタ設定 */

        [Label("背景")]
        [SerializeField] private Image mBackground;
        [Label("文字")]
        [SerializeField] private TMP_Text mLabel;
        [Label("通常の色")]
        [SerializeField] private Color mNormalColor = new(0.15f, 0.15f, 0.2f, 0.9f);
        [Label("選択中の色")]
        [SerializeField] private Color mFocusedColor = new(0.85f, 0.65f, 0.2f, 1f);
        // 操作の途中で押さえている行の色
        [Label("押さえている行の色")]
        [SerializeField] private Color mMarkedColor = new(0.35f, 0.5f, 0.8f, 1f);

        /* 状態 */

        private bool mIsFocused;
        private bool mIsMarked;

        /* プロパティ */

        public string Text => mLabel != null ? mLabel.text : string.Empty;

        /* 表示 */

        public void SetText(string aText)
        {
            if (mLabel != null) mLabel.text = aText;
        }

        public void SetFocused(bool aIsFocused)
        {
            mIsFocused = aIsFocused;
            Refresh();
        }

        public void SetMarked(bool aIsMarked)
        {
            mIsMarked = aIsMarked;
            Refresh();
        }

        private void Refresh()
        {
            if (mBackground == null) return;
            mBackground.color = mIsFocused ? mFocusedColor : (mIsMarked ? mMarkedColor : mNormalColor);
        }
    }
}
