/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPSettingsRow.cs
 * @author hqrse
 * @date 2026/11/23
 * @brief 設定画面の表の 1 行分の表示
 * =====================================*/

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AttributeUtility;

namespace PPCore
{
    // 左に項目名、右にボタン(割り当ての 2 枠や値の切り替え)を並べる表の 1 行
    // カーソルのある行は、背景を暖色にして枠と印を出す
    public class PPSettingsRow : MonoBehaviour
    {
        /* インスペクタ設定 */

        [Label("背景")]
        [SerializeField] private Image mBackground;
        // カーソルのある行に重ねる枠
        [Label("カーソルの枠")]
        [SerializeField] private Image mFocusFrame;
        // カーソルのある行の、項目名の左に出す印
        [Label("カーソルの印")]
        [SerializeField] private GameObject mFocusMarker;
        [Label("項目名")]
        [SerializeField] private TMP_Text mLabel;
        // ボタンを並べる場所。右寄せで横に並べる
        [Label("ボタンを並べる場所")]
        [SerializeField] private RectTransform mCellContainer;
        [Label("通常の背景の色")]
        [SerializeField] private Color mNormalColor = new(0.15f, 0.19f, 0.27f, 1f);
        [Label("カーソルのある行の背景の色")]
        [SerializeField] private Color mFocusedColor = new(0.35f, 0.26f, 0.21f, 1f);

        /* プロパティ */

        public RectTransform CellContainer => mCellContainer;
        public string Text => mLabel != null ? mLabel.text : string.Empty;

        /* 表示 */

        public void SetText(string aText)
        {
            if (mLabel != null) mLabel.text = aText;
        }

        public void SetFocused(bool aIsFocused)
        {
            if (mBackground != null) mBackground.color = aIsFocused ? mFocusedColor : mNormalColor;
            if (mFocusFrame != null) mFocusFrame.enabled = aIsFocused;
            if (mFocusMarker != null) mFocusMarker.SetActive(aIsFocused);
        }
    }
}
