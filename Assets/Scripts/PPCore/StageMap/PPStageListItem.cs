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
    // 文字と背景だけの 1 行。選択中は左に印を出し、操作の途中で押さえている行は背景の色で見分ける
    // 印は選択の移動で前の行から滑ってきて、決定で 1 回転する。動きは timeScale に関係なく unscaled の時間で進める
    public class PPStageListItem : MonoBehaviour, IPPStageFocusable, IPPStageFocusMotion
    {
        /* インスペクタ設定 */

        [Label("背景")]
        [SerializeField] private Image mBackground;
        [Label("文字")]
        [SerializeField] private TMP_Text mLabel;
        // 画像は PPRuntimeSettings の「メニューの選択中の印」を使う。未設定ならこの Image の画像のまま
        [Label("選択中の印")]
        [SerializeField] private Image mCursor;
        // 行の中央から見た横の位置。文字の長さに関係なく、どの行でも同じ位置に出す
        [Label("印の位置(横)")]
        [SerializeField] private float mCursorX = -120f;
        [Label("通常の色")]
        [SerializeField] private Color mNormalColor = new(0.15f, 0.15f, 0.2f, 0.9f);
        // 操作の途中で押さえている行の色
        [Label("押さえている行の色")]
        [SerializeField] private Color mMarkedColor = new(0.35f, 0.5f, 0.8f, 1f);
        // 選べない行の文字の色
        [Label("選べない行の文字の色")]
        [SerializeField] private Color mDisabledTextColor = new(1f, 1f, 1f, 0.35f);

        [Header("動き")]
        [Label("移動の時間(秒)")]
        [Min(0f)]
        [SerializeField] private float mMoveDuration = 0.12f;
        // 0 にすると決定の動きを出さず、すぐに決定する
        [Label("決定の時間(秒)")]
        [Min(0f)]
        [SerializeField] private float mSubmitDuration = 0.3f;
        [Label("決定の拡大率")]
        [Min(1f)]
        [SerializeField] private float mSubmitScale = 1.3f;

        /* 状態 */

        private bool mIsFocused;
        private bool mIsMarked;
        private bool mIsInteractable = true;
        // 選べるときの文字の色。最初に選べなくしたときに覚える
        private Color? mEnabledTextColor;
        // 移動の動きの始まりの位置(印の親から見た位置)と経過秒。動いていなければ経過秒は負
        private Vector2 mMoveFrom;
        private float mMoveElapsed = -1f;
        // 決定の動きの経過秒。動いていなければ負
        private float mSubmitElapsed = -1f;

        /* プロパティ */

        public string Text => mLabel != null ? mLabel.text : string.Empty;

        // 印の止まる位置
        private Vector2 CursorRestPosition => new(mCursorX, 0f);

        /* Unity イベント */

        private void Awake()
        {
            if (mCursor == null) return;
            var sprite = PPRuntimeSettings.Current.MenuCursorSprite;
            if (sprite != null) mCursor.sprite = sprite;
            ResetCursor();
        }

        private void Update()
        {
            if (mCursor == null || !mIsFocused) return;
            float delta = Time.unscaledDeltaTime;
            var cursorRect = mCursor.rectTransform;

            if (mMoveElapsed >= 0f)
            {
                mMoveElapsed += delta;
                float t = mMoveDuration > 0f ? Mathf.Clamp01(mMoveElapsed / mMoveDuration) : 1f;
                cursorRect.anchoredPosition = Vector2.LerpUnclamped(mMoveFrom, CursorRestPosition, EaseOutCubic(t));
                if (t >= 1f) mMoveElapsed = -1f;
            }

            if (mSubmitElapsed >= 0f)
            {
                mSubmitElapsed += delta;
                float t = Mathf.Clamp01(mSubmitElapsed / mSubmitDuration);
                // 縦の軸で 1 回転させ、途中で膨らんで戻る
                cursorRect.localRotation = Quaternion.Euler(0f, 360f * EaseOutCubic(t), 0f);
                cursorRect.localScale = Vector3.one * (1f + (mSubmitScale - 1f) * Mathf.Sin(Mathf.PI * t));
                if (t >= 1f) mSubmitElapsed = -1f;
            }
        }

        /* 表示 */

        public void SetText(string aText)
        {
            if (mLabel != null) mLabel.text = aText;
        }

        // 選べない行は文字を薄くし、決定しても動かさない
        public void SetInteractable(bool aIsInteractable)
        {
            mIsInteractable = aIsInteractable;
            if (mLabel == null) return;
            mEnabledTextColor ??= mLabel.color;
            mLabel.color = aIsInteractable ? mEnabledTextColor.Value : mDisabledTextColor;
        }

        public void SetFocused(bool aIsFocused)
        {
            if (mIsFocused != aIsFocused) ResetCursor();
            mIsFocused = aIsFocused;
            Refresh();
        }

        public void SetMarked(bool aIsMarked)
        {
            mIsMarked = aIsMarked;
            Refresh();
        }

        // 前の行の印の位置から、この行の印の位置へ滑らせる
        public void MoveFocusFrom(Component aPrevious)
        {
            if (mCursor == null || mMoveDuration <= 0f || aPrevious is not PPStageListItem previous || previous.mCursor == null) return;

            var parent = (RectTransform)mCursor.rectTransform.parent;
            Vector2 from = parent.InverseTransformPoint(previous.mCursor.rectTransform.position);
            mMoveFrom = from;
            mMoveElapsed = 0f;
            mCursor.rectTransform.anchoredPosition = from;
        }

        public float PlaySubmit()
        {
            if (mCursor == null || !mIsInteractable || mSubmitDuration <= 0f) return 0f;
            mSubmitElapsed = 0f;
            return mSubmitDuration;
        }

        private void Refresh()
        {
            if (mCursor != null) mCursor.gameObject.SetActive(mIsFocused);
            if (mBackground != null) mBackground.color = mIsMarked ? mMarkedColor : mNormalColor;
        }

        // 印を止まる位置に戻し、動きを止める
        private void ResetCursor()
        {
            mMoveElapsed = -1f;
            mSubmitElapsed = -1f;
            if (mCursor == null) return;
            var cursorRect = mCursor.rectTransform;
            cursorRect.anchoredPosition = CursorRestPosition;
            cursorRect.localRotation = Quaternion.identity;
            cursorRect.localScale = Vector3.one;
        }

        /* ヘルパー */

        private static float EaseOutCubic(float aT) => 1f - Mathf.Pow(1f - aT, 3f);
    }
}
