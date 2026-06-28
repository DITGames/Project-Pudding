/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleUnitView.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief PPバトル中のユニット表示コンポーネント
 * =====================================*/
using CommandBattleCore;
using UnityEngine;
using UnityEngine.UI;

namespace PPCore
{
    public class PPBattleUnitView : MonoBehaviour
    {
        [Label("ステータスウィジェット")]
        [SerializeField] private PPUnitStatusWidget mStatusWidget;
        
        [Label("アイコン")]
        [SerializeField] private Image mUnitIcon;

        [Label("アニメーター")]
        [SerializeField] private Animator mAnimator;
        
        private BattleUnit mBattleUnit;

        public void Initialize(BattleUnit aUnit, PPUnitVisualDefinition aVisualDefinition, BattleSide aSide)
        {
            mBattleUnit = aUnit;
            mUnitIcon.sprite = aVisualDefinition.UnitIcon;
            mAnimator.runtimeAnimatorController = aVisualDefinition.Animator;
            if(aSide == BattleSide.Ally) mUnitIcon.transform.eulerAngles = new Vector3(0, 180, 0);
            mStatusWidget.Bind(new PPBattleUnitStatusSource(mBattleUnit));
        }

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