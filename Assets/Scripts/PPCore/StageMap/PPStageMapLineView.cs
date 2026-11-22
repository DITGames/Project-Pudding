/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageMapLineView.cs
 * @author hqrse
 * @date 2026/11/21
 * @brief マップのマス同士を結ぶ線
 * =====================================*/

using UnityEngine;
using UnityEngine.UI;
using AttributeUtility;

namespace PPCore
{
    // 2 つのマスを結ぶ線。Image 1 枚を中点に置き、向きと長さを合わせる
    [RequireComponent(typeof(RectTransform))]
    public class PPStageMapLineView : MonoBehaviour
    {
        /* インスペクタ設定 */

        [Label("線")]
        [SerializeField] private Image mLine;
        [Label("太さ")]
        [SerializeField] private float mThickness = 8f;
        // マスに重ならないよう、両端をこの長さだけ短くする
        [Label("端の余白")]
        [SerializeField] private float mPadding = 34f;
        [Label("通常の色")]
        [SerializeField] private Color mDimColor = new(1f, 1f, 1f, 0.2f);
        // 現在地から選べるマスへ伸びている線
        [Label("行き先の色")]
        [SerializeField] private Color mActiveColor = new(1f, 0.9f, 0.45f, 1f);
        [Label("通った線の色")]
        [SerializeField] private Color mTakenColor = new(1f, 0.75f, 0.3f, 0.75f);

        /* プロパティ */

        public PPStageMapNode From { get; private set; }
        public PPStageMapNode To { get; private set; }

        /* 表示 */

        public void Setup(PPStageMapNode aFrom, PPStageMapNode aTo, Vector2 aFromPosition, Vector2 aToPosition)
        {
            From = aFrom;
            To = aTo;
            name = $"Line_{aFrom.Floor}_{aFrom.Column}_{aTo.Floor}_{aTo.Column}";

            var rect = (RectTransform)transform;
            var dir = aToPosition - aFromPosition;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = (aFromPosition + aToPosition) * 0.5f;
            rect.sizeDelta = new Vector2(Mathf.Max(dir.magnitude - mPadding * 2f, 1f), mThickness);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

            if (mLine != null) mLine.raycastTarget = false;
            SetState(false, false);
        }

        public void SetState(bool aIsActive, bool aIsTaken)
        {
            if (mLine != null) mLine.color = aIsActive ? mActiveColor : (aIsTaken ? mTakenColor : mDimColor);
        }
    }
}
