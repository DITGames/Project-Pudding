/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageListItem.cs
 * @author hqrse
 * @date 2026/11/21
 * @brief ステージマップやタイトルのメニューに並べる 1 行
 * =====================================*/

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AttributeUtility;

namespace PPCore
{
    // 文字と背景だけの 1 行。選択中は文字の左に印を出し、操作の途中で押さえている行は背景の色で見分ける
    public class PPStageListItem : MonoBehaviour, IPPStageFocusable
    {
        /* インスペクタ設定 */

        [Label("背景")]
        [SerializeField] private Image mBackground;
        // 中央寄せの文字を前提に、印をその左隣へ置く
        [Label("文字")]
        [SerializeField] private TMP_Text mLabel;
        // 画像は PPRuntimeSettings の「メニューの選択中の印」を使う。未設定ならこの Image の画像のまま
        [Label("選択中の印")]
        [SerializeField] private Image mCursor;
        [Label("印と文字の間隔")]
        [SerializeField] private float mCursorGap = 12f;
        [Label("通常の色")]
        [SerializeField] private Color mNormalColor = new(0.15f, 0.15f, 0.2f, 0.9f);
        // 操作の途中で押さえている行の色
        [Label("押さえている行の色")]
        [SerializeField] private Color mMarkedColor = new(0.35f, 0.5f, 0.8f, 1f);
        // 選べない行の文字の色
        [Label("選べない行の文字の色")]
        [SerializeField] private Color mDisabledTextColor = new(1f, 1f, 1f, 0.35f);

        /* 状態 */

        private bool mIsFocused;
        private bool mIsMarked;
        // 選べるときの文字の色。最初に選べなくしたときに覚える
        private Color? mEnabledTextColor;

        /* プロパティ */

        public string Text => mLabel != null ? mLabel.text : string.Empty;

        /* Unity イベント */

        private void Awake()
        {
            if (mCursor == null) return;
            var sprite = PPRuntimeSettings.Current.MenuCursorSprite;
            if (sprite != null) mCursor.sprite = sprite;
            mCursor.gameObject.SetActive(mIsFocused);
        }

        /* 表示 */

        public void SetText(string aText)
        {
            if (mLabel != null) mLabel.text = aText;
            PlaceCursor();
        }

        // 選べない行は文字を薄くする
        public void SetInteractable(bool aIsInteractable)
        {
            if (mLabel == null) return;
            mEnabledTextColor ??= mLabel.color;
            mLabel.color = aIsInteractable ? mEnabledTextColor.Value : mDisabledTextColor;
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
            if (mCursor != null) mCursor.gameObject.SetActive(mIsFocused);
            if (mBackground != null) mBackground.color = mIsMarked ? mMarkedColor : mNormalColor;
        }

        // 印を文字の左端から間隔を空けた位置へ置く。文字の幅は行の幅を超えない
        private void PlaceCursor()
        {
            if (mCursor == null || mLabel == null) return;

            float rowWidth = ((RectTransform)transform).rect.width;
            float textWidth = Mathf.Min(mLabel.GetPreferredValues(mLabel.text).x, rowWidth);
            var cursorRect = mCursor.rectTransform;
            float x = -textWidth * 0.5f - mCursorGap - cursorRect.rect.width * 0.5f;
            cursorRect.anchoredPosition = new Vector2(x, cursorRect.anchoredPosition.y);
        }
    }
}
