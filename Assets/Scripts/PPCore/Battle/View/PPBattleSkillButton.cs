/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleSkillButton.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief スキルボタンコンポーネント
 * =====================================*/
using System;
using CommandBattleCore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PPCore
{
    public class PPBattleSkillButton : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [Label("ボタン")] [SerializeField] private Button mButton;
        [Label("アイコン")] [SerializeField] private Image mIcon;
        [Label("スキル名")] [SerializeField] private Text mNameLabel;
        [Label("消費コイン")] [SerializeField] private Text mCostLabel;

        private BattleSkill mSkill;
        private IPPSkillStatusSource mSource;
        private Action<BattleSkill> mOnDecided;

        // 初期フォーカス設定
        public GameObject FocusTarget => mButton.gameObject;
        public RectTransform Rect => (RectTransform)transform;
        
        // フォーカスが乗ったことをの通知(ゲームパッドのリストスクロールに使用)
        public event Action<PPBattleSkillButton> OnSelected;
        public event Action<PPBattleSkillButton> OnDeselected;

        public void Setup(BattleSkill aSkill, IPPSkillStatusSource aSource, Sprite aIcon,
            Action<BattleSkill> aOnSelected)
        {
            mSkill = aSkill;
            mSource = aSource;
            mOnDecided = aOnSelected;

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
        
        private void HandleClick() => mOnDecided?.Invoke(mSkill);

        public void OnSelect(BaseEventData _) => OnSelected?.Invoke(this);
        public void OnDeselect(BaseEventData _) => OnDeselected?.Invoke(this);

        private void Refresh()
        {
            mCostLabel.text = mSource.Cost.ToString();
            mButton.interactable = mSource.IsCastable;
        }

        private void OnDestroy()
        {
            mButton.onClick.RemoveListener(HandleClick);
            if(mSource != null) mSource.Changed -= Refresh;
            (mSource as PPBattleSkillStatusSource)?.Dispose();
        }
    }
}