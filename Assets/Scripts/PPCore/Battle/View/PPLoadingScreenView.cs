/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPLoadingScreenView.cs
 * @author hqrse
 * @date 2026/11/19
 * @brief ロード画面の表示
 * =====================================*/

using UnityEngine;
using UnityEngine.UI;
using AttributeUtility;

namespace PPCore
{
    // ロード画面。回るコインと、読み込みの進み具合のゲージを出す
    public class PPLoadingScreenView : MonoBehaviour
    {
        /* インスペクタ設定 */

        // 縦の軸で回すコインの画像
        [Label("回すコイン")]
        [SerializeField] private RectTransform mCoin;
        [Label("1回転の秒数")]
        [Min(0.1f)]
        [SerializeField] private float mTurnSeconds = 1.2f;
        // 進み具合を表すゲージの塗り。fillAmount で 0〜1 を出す
        [Label("ゲージの塗り")]
        [SerializeField] private Image mGaugeFill;

        /* Unity イベント */

        // 読み込み中は時間の流れに関係なく回すため、timeScale に左右されない時間を使う
        private void Update()
        {
            if (mCoin == null) return;

            float angle = Time.unscaledTime / mTurnSeconds * 360f;
            mCoin.localRotation = Quaternion.Euler(0f, angle % 360f, 0f);
        }

        /* 表示 */

        // 画面を出し、ゲージを空にする
        public void Show()
        {
            gameObject.SetActive(true);
            SetProgress(0f);
        }

        // 画面を消す
        public void Hide() => gameObject.SetActive(false);

        // 進み具合(0〜1)をゲージへ出す
        public void SetProgress(float aRatio)
        {
            if (mGaugeFill != null) mGaugeFill.fillAmount = Mathf.Clamp01(aRatio);
        }
    }
}
