/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleItemButton.cs
 * @author hqrse
 * @date 2026/07/02
 * @brief バトル中のアイテムボタン
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PPCore
{
    public class PPBattleItemButton : MonoBehaviour, ISelectHandler
    {
        [Label("ボタン")] [SerializeField] private Button mButton;
        [Label("アイコン")] [SerializeField] private Image mIcon;
        [Label("アイテム名")] [SerializeField] private Text mNameLabel;
        [Label("個数")] [SerializeField] private Text mCountLabel;
        [Label("消費コイン")] [SerializeField] private Text mCostLabel;

        private PPItemDefinition mDefinition;
        private IPPItemStatusSource mSource;
        private Action<PPItemDefinition> mOnSelected;
        
        public GameObject FocusTarget => mButton.gameObject;
        public RectTransform Rect => (RectTransform)transform;

        public event Action<PPBattleItemButton> OnFocused;

        public void Setup(PPItemDefinition aDefinition, IPPItemStatusSource aSource, Sprite aIcon,
            Action<PPItemDefinition> aOnSelected)
        {
            mDefinition = aDefinition;
            mSource = aSource;
            mOnSelected = aOnSelected;

            if (mIcon != null)
            {
                mIcon.enabled = aIcon != null;
                mIcon.sprite = aIcon;
            }

            mNameLabel.text = aSource.DisplayName;
            
            mButton.onClick.AddListener(HandleClick);
            mSource.Changed += Refresh;
            Refresh();
        }
        
        private void HandleClick() => mOnSelected?.Invoke(mDefinition);

        public void OnSelect(BaseEventData _) => OnFocused?.Invoke(this);

        private void Refresh()
        {
            mCountLabel.text = mSource.Count.ToString();
            mCostLabel.text = mSource.Cost.ToString();
            mButton.interactable = mSource.IsUsable;
        }

        private void OnDestroy()
        {
            mButton.onClick.RemoveListener(HandleClick);
            if(mSource != null) mSource.Changed -= Refresh;
            (mSource as PPItemStatusSource)?.Dispose();
        }
    }
}