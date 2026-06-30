/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleUnitView.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief PPバトル中のユニット表示コンポーネント
 * =====================================*/
using System;
using CommandBattleCore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PPCore
{
    public class PPBattleUnitView : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [Label("ステータスウィジェット")]
        [SerializeField] private PPUnitStatusWidget mStatusWidget;
        [Label("アイコン")]
        [SerializeField] private Image mUnitIcon;
        [Label("アニメーター")]
        [SerializeField] private Animator mAnimator;

        [Label("選択ボタン")] [SerializeField] private Button mSelectButton;
        [Label("フォーカス枠")] [SerializeField] private GameObject mFocusFrame;
        
        private BattleUnit mBattleUnit;
        public BattleUnit BattleUnit => mBattleUnit;
        public GameObject SelectableObject => mSelectButton.gameObject;

        // ユニットが選択されたことの通知
        public event Action<PPBattleUnitView> OnClicked;

        private void Awake() => mSelectButton.onClick.AddListener(() => OnClicked?.Invoke(this));

        public void Initialize(BattleUnit aUnit, PPUnitVisualDefinition aVisualDefinition, BattleSide aSide)
        {
            mBattleUnit = aUnit;
            mUnitIcon.sprite = aVisualDefinition.UnitIcon;
            mAnimator.runtimeAnimatorController = aVisualDefinition.Animator;
            if(aSide == BattleSide.Ally) mUnitIcon.transform.eulerAngles = new Vector3(0, 180, 0);
            mStatusWidget.Bind(new PPBattleUnitStatusSource(mBattleUnit));
        }

        public void SetSelectable(bool aSelectable)
        {
            mSelectButton.interactable = aSelectable;
        }

        public void SetFocused(bool aFocused)
        {
            if (mFocusFrame != null) mFocusFrame.SetActive(aFocused);
        }
        
        public void OnSelect(BaseEventData _) =>SetFocused(true);
        public void OnDeselect(BaseEventData _) =>SetFocused(false);

        public void CommandExecuted(BattleCommandBase aCommand)
        {
            mAnimator.SetTrigger("Attack");
        }

        public void PlayDamage(float aDmg)
        {
            if(mBattleUnit.IsAlive) mAnimator.SetTrigger("Damaged");
        }

        public void PlayHeal(float aAmt)
        {
            
        }

        public void PlayDefeat()
        {
            mAnimator.SetTrigger("Defeated");
        }

        public void AddStatusIcon(StatusEffect aEffect)
        {
            
        }

        public void RemoveStatusIcon(StatusEffect aEffect)
        {
            
        }
    }
}