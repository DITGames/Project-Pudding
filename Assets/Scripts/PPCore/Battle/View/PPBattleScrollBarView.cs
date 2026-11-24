/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleScrollBarView.cs
 * @author hqrse
 * @date 2026/11/24
 * @brief バトル画面のスクロール位置バー
 * =====================================*/

using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // バトル画面で今見ている範囲を、スクロールできる範囲全体に対するつまみの位置と幅で示す
    // カメラが LateUpdate で動いた後に合わせるため、実行順を遅らせる
    [DefaultExecutionOrder(100)]
    public class PPBattleScrollBarView : MonoBehaviour
    {
        /* インスペクタ設定 */

        [Label("バトル画面のカメラ")]
        [SerializeField] private PPBattleCameraController mCameraController;
        // バーの中で、見ている範囲に合わせて左右の端を伸び縮みさせる
        [Label("つまみ")]
        [SerializeField] private RectTransform mThumb;

        /* Unity イベント */

        private void LateUpdate()
        {
            if (mCameraController == null || mThumb == null) return;
            var range = mCameraController.VisibleRange;
            mThumb.anchorMin = new Vector2(range.x, 0f);
            mThumb.anchorMax = new Vector2(range.y, 1f);
            mThumb.offsetMin = Vector2.zero;
            mThumb.offsetMax = Vector2.zero;
        }
    }
}
