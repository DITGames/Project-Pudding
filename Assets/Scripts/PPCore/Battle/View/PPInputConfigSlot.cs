/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPInputConfigSlot.cs
 * @author hqrse
 * @date 2026/11/18
 * @brief キー設定画面に並ぶコイン 1 枚分の表示
 * =====================================*/

using AttributeUtility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PPCore
{
    // キー設定画面のコイン 1 枚分。割り当てたキーの名前や項目名をコインの中に出す。動きは PPInputConfigMenuView が付ける
    public class PPInputConfigSlot : MonoBehaviour
    {
        /* インスペクタ設定 */

        // 位置・大きさを動かす部分。ルートは並びの基準位置に置いたまま動かさない
        [Label("動かす部分")]
        [SerializeField] private RectTransform mBody;
        [Label("コイン")]
        [SerializeField] private Image mCoinImage;
        // 選択中だけ表示する縁の光
        [Label("選択中の光")]
        [SerializeField] private Image mHighlight;
        [Label("文字")]
        [SerializeField] private TMP_Text mText;
        [Label("通常のコイン")]
        [SerializeField] private Sprite mNormalSprite;
        // 未割り当ての枠に使うコイン
        [Label("空きのコイン")]
        [SerializeField] private Sprite mEmptySprite;

        /* プロパティ */

        public RectTransform Body => mBody;

        /* 表示 */

        // コインの中の文字を変える。空きの枠は灰色のコインにする
        public void SetText(string aText, bool aIsEmpty = false)
        {
            mText.text = aText;
            mCoinImage.sprite = aIsEmpty ? mEmptySprite : mNormalSprite;
        }

        // 選択中の表示を切り替える
        public void SetFocused(bool aFocused) => mHighlight.enabled = aFocused;
    }
}
