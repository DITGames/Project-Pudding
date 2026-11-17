/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPInputConfigSlot.cs
 * @author hqrse
 * @date 2026/11/18
 * @brief キー設定画面に並ぶ札 1 枚分の表示
 * =====================================*/

using AttributeUtility;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace PPCore
{
    // キー設定画面の札 1 枚分。割り当てたキーの名前や項目名を出す。動きは PPInputConfigMenuView が付ける
    public class PPInputConfigSlot : MonoBehaviour
    {
        /* インスペクタ設定 */

        // 位置・大きさを動かす部分。ルートは並びの基準位置に置いたまま動かさない
        [Label("動かす部分")]
        [SerializeField] private RectTransform mBody;
        [Label("下地")]
        [FormerlySerializedAs("mCoinImage")]
        [SerializeField] private Image mBackground;
        // 選択中だけ表示する枠
        [Label("選択中の枠")]
        [SerializeField] private Image mHighlight;
        [Label("文字")]
        [SerializeField] private TMP_Text mText;
        [Label("文字の色")]
        [SerializeField] private Color mTextColor = Color.white;
        // 未割り当ての枠の文字の色
        [Label("空きの文字の色")]
        [SerializeField] private Color mEmptyTextColor = new(0.55f, 0.55f, 0.55f, 1f);

        /* プロパティ */

        public RectTransform Body => mBody;

        /* 表示 */

        // 札の文字を変える。空きの枠は文字を灰色にする
        public void SetText(string aText, bool aIsEmpty = false)
        {
            mText.text = aText;
            mText.color = aIsEmpty ? mEmptyTextColor : mTextColor;
        }

        // 選択中の表示を切り替える
        public void SetFocused(bool aFocused) => mHighlight.enabled = aFocused;
    }
}
