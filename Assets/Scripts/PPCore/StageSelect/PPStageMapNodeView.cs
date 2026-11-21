/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageMapNodeView.cs
 * @author hqrse
 * @date 2026/11/21
 * @brief マップ上の 1 マスの見た目
 * =====================================*/

using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using AttributeUtility;

namespace PPCore
{
    // マスの進み具合
    public enum PPStageNodeState
    {
        // まだ行けない
        Locked,
        // 今選べる
        Selectable,
        Visited,
        Current,
    }

    // マップ上の 1 マス。種類の色と短い名前を出し、選べるマスはクリックで選べる
    // 選べるマスはゆっくり脈打ち、キーボードやゲームパッドで選んでいるマスは大きく表示する
    [RequireComponent(typeof(RectTransform))]
    public class PPStageMapNodeView : MonoBehaviour, IPointerClickHandler
    {
        /* インスペクタ設定 */

        [Header("参照")]
        // クリックの判定にも使う
        [Label("背景")]
        [SerializeField] private Image mBackground;
        [Label("名前")]
        [SerializeField] private TMP_Text mLabel;
        // 選んでいるマスにだけ出す枠。未設定なら出さない
        [Label("選択中の枠")]
        [SerializeField] private Image mRing;

        [Header("状態")]
        // 種類の色に掛ける明るさ(0〜1)
        [Label("まだ行けないマスの明るさ")]
        [Range(0f, 1f)]
        [SerializeField] private float mLockedBrightness = 0.35f;
        [Label("通過したマスの明るさ")]
        [Range(0f, 1f)]
        [SerializeField] private float mVisitedBrightness = 0.6f;

        [Header("動き")]
        [Label("脈打つ速さ")]
        [SerializeField] private float mPulseSpeed = 3.2f;
        [Label("脈打つ大きさ")]
        [SerializeField] private float mPulseAmount = 0.1f;
        [Label("選択中の拡大率")]
        [SerializeField] private float mFocusScale = 1.25f;

        /* 状態 */

        private Action<PPStageMapNodeView> mOnClick;
        private RectTransform mRect;
        private Color mTypeColor = Color.white;
        private bool mIsFocused;

        /* プロパティ */

        public PPStageMapNode Node { get; private set; }
        public PPStageNodeState State { get; private set; } = PPStageNodeState.Locked;

        /* Unity イベント */

        // 選べるマスだけ脈打たせる
        void Update()
        {
            if (State != PPStageNodeState.Selectable) return;

            float pulse = 1f + Mathf.Sin(Time.unscaledTime * mPulseSpeed) * mPulseAmount;
            mRect.localScale = Vector3.one * pulse * (mIsFocused ? mFocusScale : 1f);
        }

        public void OnPointerClick(PointerEventData aEventData)
        {
            if (State == PPStageNodeState.Selectable) mOnClick?.Invoke(this);
        }

        /* 表示 */

        public void Setup(PPStageMapNode aNode, Vector2 aPosition, string aLabel, Color aColor, Action<PPStageMapNodeView> aOnClick)
        {
            mRect = (RectTransform)transform;
            Node = aNode;
            mOnClick = aOnClick;
            mTypeColor = aColor;

            name = $"Node_{aNode.Floor}_{aNode.Column}_{aNode.Type}";
            mRect.anchoredPosition = aPosition;
            if (mLabel != null) mLabel.text = aLabel;
            SetState(PPStageNodeState.Locked, false);
        }

        public void SetState(PPStageNodeState aState, bool aIsFocused)
        {
            State = aState;
            mIsFocused = aIsFocused;

            float brightness = aState switch
            {
                PPStageNodeState.Locked => mLockedBrightness,
                PPStageNodeState.Visited => mVisitedBrightness,
                _ => 1f,
            };
            if (mBackground != null)
            {
                mBackground.color = new Color(mTypeColor.r * brightness, mTypeColor.g * brightness, mTypeColor.b * brightness, 1f);
            }
            if (mRing != null) mRing.enabled = aIsFocused || aState == PPStageNodeState.Current;
            if (aState != PPStageNodeState.Selectable) mRect.localScale = Vector3.one * (aIsFocused ? mFocusScale : 1f);
        }
    }
}
