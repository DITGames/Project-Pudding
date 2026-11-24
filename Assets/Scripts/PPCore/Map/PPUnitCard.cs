/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitCard.cs
 * @author hqrse
 * @date 2026/11/21
 * @brief ステージマップの画面に並べる、ユニット 1 体分の札
 * =====================================*/

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AttributeUtility;

namespace PPCore
{
    // ユニットのアイコン・名前・補足を出す札。選択中と、操作の途中で押さえている札を枠の色で見分ける
    // 編成の配置・所持ユニットの一覧・仲間の候補・マップのパーティ表示で、プレハブを変えて使い回す
    public class PPUnitCard : MonoBehaviour, IPPMenuFocusable
    {
        /* インスペクタ設定 */

        [Header("参照")]
        [Label("枠")]
        [SerializeField] private Image mFrame;
        [Label("アイコン")]
        [SerializeField] private Image mIcon;
        // 札の上に出す見出し(配置の名前など)。未設定なら出さない
        [Label("見出し")]
        [SerializeField] private TMP_Text mHeader;
        [Label("名前")]
        [SerializeField] private TMP_Text mName;
        // 名前の下に出す補足。リッチテキストで属性の色などを付けられる
        [Label("補足")]
        [SerializeField] private TMP_Text mSub;

        [Header("色")]
        [Label("通常の枠の色")]
        [SerializeField] private Color mNormalColor = new(0.41f, 0.44f, 0.53f, 1f);
        [Label("選択中の枠の色")]
        [SerializeField] private Color mFocusedColor = new(0.94f, 0.77f, 0.36f, 1f);
        // 編成で入れ替え先を選んでいる間の、元の札の枠の色
        [Label("押さえている枠の色")]
        [SerializeField] private Color mMarkedColor = new(0.36f, 0.75f, 0.92f, 1f);
        [Label("選択中の拡大率")]
        [Min(1f)]
        [SerializeField] private float mFocusScale = 1.04f;

        /* 状態 */

        private bool mIsFocused;
        private bool mIsMarked;

        /* プロパティ */

        // 名前と補足をつないだ文字。検証や表示の確認に使う
        public string Text => $"{(mName != null ? mName.text : string.Empty)} / {(mSub != null ? mSub.text : string.Empty)}";

        /* 表示 */

        // アイコンと文字を出す。アイコンが null なら隠す
        public void SetUnit(Sprite aIcon, string aName, string aSub)
        {
            if (mIcon != null)
            {
                mIcon.sprite = aIcon;
                mIcon.enabled = aIcon != null;
            }
            SetText(mName, aName);
            SetText(mSub, aSub);
        }

        public void SetHeader(string aText) => SetText(mHeader, aText);

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
            if (mFrame != null) mFrame.color = mIsFocused ? mFocusedColor : (mIsMarked ? mMarkedColor : mNormalColor);
            transform.localScale = Vector3.one * (mIsFocused ? mFocusScale : 1f);
        }

        /* ヘルパー */

        private static void SetText(TMP_Text aText, string aValue)
        {
            if (aText == null) return;
            aText.text = aValue ?? string.Empty;
            aText.gameObject.SetActive(!string.IsNullOrEmpty(aValue));
        }
    }
}
