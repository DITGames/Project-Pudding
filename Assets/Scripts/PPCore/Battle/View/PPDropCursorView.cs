/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPDropCursorView.cs
 * @author hqrse
 * @date 2026/11/22
 * @brief コインの投入位置を UI で示すアイコン
 * =====================================*/

using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // プレイヤー操作中だけ、ディスペンサーの投入位置(ばらつきを加える前の点)を画面上の位置に直してアイコンを置く
    // 床やコインには当てないため、降ってくるコインや押し出し板の動きで位置がぶれない
    public class PPDropCursorView : MonoBehaviour
    {
        /* インスペクタ設定 */

        [Label("コインの投入")]
        [SerializeField] private PPCoinDispenser mDispenser;
        // 未設定ならメインカメラを使う
        [Label("プッシャーを映すカメラ")]
        [SerializeField] private Camera mCamera;
        // プレイヤー操作中だけ表示するアイコン。この RectTransform を投入位置へ動かす
        [Label("アイコン")]
        [SerializeField] private RectTransform mIcon;
        // 投入位置から画面上で上下へずらす量(Canvas の単位)。正の値で上へ
        [Label("画面上の高さのずれ")]
        [SerializeField] private float mScreenOffsetY = 0f;

        /* 状態 */

        private Canvas mCanvas;

        /* Unity イベント */

        private void Awake() => mCanvas = GetComponentInParent<Canvas>();

        private void LateUpdate()
        {
            if (mIcon == null) return;

            var camera = mCamera != null ? mCamera : Camera.main;
            // メニューやコマンド入力で時間が止まっている間は投入できないため隠す
            bool isVisible = mDispenser != null && camera != null && mDispenser.IsRunning && mDispenser.IsPlayerControlled && Time.timeScale > 0f;
            if (mIcon.gameObject.activeSelf != isVisible) mIcon.gameObject.SetActive(isVisible);
            if (!isVisible) return;

            var screenPoint = camera.WorldToScreenPoint(mDispenser.GetDropPoint(mDispenser.CursorRatio));
            var parent = (RectTransform)mIcon.parent;
            // 画面全体に重ねる Canvas ではカメラを渡さない
            var uiCamera = mCanvas != null && mCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? mCanvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, uiCamera, out var local))
            {
                mIcon.localPosition = new Vector3(local.x, local.y + mScreenOffsetY, 0f);
            }
        }
    }
}
