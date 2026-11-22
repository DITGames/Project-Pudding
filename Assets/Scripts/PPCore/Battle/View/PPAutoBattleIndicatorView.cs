/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPAutoBattleIndicatorView.cs
 * @author hqrse
 * @date 2026/11/22
 * @brief オートバトル中の表示
 * =====================================*/

using TMPro;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // オートバトル中だけ「オートバトル中」と回るアイコン、味方の投入 AI のモードを出す
    // メニューやコマンド入力で止まっている間も回して見せるため、timeScale に左右されない時間で回す
    public class PPAutoBattleIndicatorView : MonoBehaviour
    {
        /* インスペクタ設定 */

        // オートバトル中だけ表示する部分
        [Label("表示する部分")]
        [SerializeField] private GameObject mRoot;
        [Label("回すアイコン")]
        [SerializeField] private RectTransform mIcon;
        // 正の値で時計回り
        [Label("1秒に回る角度")]
        [SerializeField] private float mDegreesPerSecond = 180f;
        [Label("AIモードの文字")]
        [SerializeField] private TMP_Text mModeText;

        /* Unity イベント */

        private void Awake()
        {
            if (mRoot != null) mRoot.SetActive(false);
        }

        private void Update()
        {
            if (mIcon == null || mRoot == null || !mRoot.activeSelf) return;
            mIcon.Rotate(0f, 0f, -mDegreesPerSecond * Time.unscaledDeltaTime);
        }

        /* 表示 */

        public void Show(bool aIsAutoBattle, string aModeName)
        {
            if (mRoot != null) mRoot.SetActive(aIsAutoBattle);
            if (mModeText != null) mModeText.text = aModeName;
        }
    }
}
