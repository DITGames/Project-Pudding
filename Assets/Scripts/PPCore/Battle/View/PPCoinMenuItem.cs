/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPCoinMenuItem.cs
 * @author hqrse
 * @date 2026/11/17
 * @brief コイン型メニューの 1 枚分の表示
 * =====================================*/

using AttributeUtility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PPCore
{
    // コイン型メニューに並ぶ 1 枚分。絵と文字の切り替えだけを持ち、動きは PPCoinMenuView が付ける
    public class PPCoinMenuItem : MonoBehaviour
    {
        /* インスペクタ設定 */

        // 位置・大きさ・回転を動かす部分。ルートは並びの基準位置に置いたまま動かさない
        [Label("動かす部分")]
        [SerializeField] private RectTransform mBody;
        [Label("透明度")]
        [SerializeField] private CanvasGroup mCanvasGroup;
        [Label("コイン")]
        [SerializeField] private Image mCoinImage;
        [Label("アイコン")]
        [SerializeField] private Image mIcon;
        // 選択中だけ表示する縁の光
        [Label("選択中の光")]
        [SerializeField] private Image mHighlight;
        [Label("名前")]
        [SerializeField] private TMP_Text mLabel;
        // コストなどの補足。空なら補足の表示ごと隠す
        [Label("補足")]
        [SerializeField] private TMP_Text mSubLabel;
        // 補足の文字と下地をまとめたもの。未設定なら文字だけを隠す
        [Label("補足の表示")]
        [SerializeField] private GameObject mSubLabelRoot;
        [Label("通常のコイン")]
        [SerializeField] private Sprite mNormalSprite;
        [Label("選べないコイン")]
        [SerializeField] private Sprite mDisabledSprite;

        /* プロパティ */

        public RectTransform Body => mBody;
        public CanvasGroup CanvasGroup => mCanvasGroup;
        // 決定できる項目か。選べない項目もカーソルは乗せられる
        public bool IsInteractable { get; private set; }

        /* 表示 */

        // 項目の内容を流し込む。選べない項目はコインを灰色にし、アイコンも暗くする
        public void Setup(PPCoinMenuEntry aEntry)
        {
            IsInteractable = aEntry.IsInteractable;
            mCoinImage.sprite = IsInteractable ? mNormalSprite : mDisabledSprite;
            mIcon.enabled = aEntry.Icon != null;
            mIcon.sprite = aEntry.Icon;
            mIcon.color = IsInteractable ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);
            mLabel.text = aEntry.Label ?? string.Empty;
            if (mSubLabel != null)
            {
                mSubLabel.text = aEntry.SubLabel ?? string.Empty;
                var root = mSubLabelRoot != null ? mSubLabelRoot : mSubLabel.gameObject;
                root.SetActive(!string.IsNullOrEmpty(aEntry.SubLabel));
            }
            SetFocused(false);
        }

        // 選択中の表示を切り替える
        public void SetFocused(bool aFocused)
        {
            if (mHighlight != null) mHighlight.enabled = aFocused;
        }
    }
}
