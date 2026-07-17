/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPCommandSelectState.cs
 * @author hqrse
 * @date 2026/07/01
 * @brief コマンド選択ステート
 * =====================================*/
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    public class PPCommandSelectState : PPBattleMenuStateBase
    {
        public PPCommandSelectState(PPBattleCommandInputController aOwner) : base(aOwner)
        {
        }

        protected override void ShowView(BattleUnit aUnit, RectTransform aAnchor)
        {
            if (aAnchor != null)
            {
                mOwner.CommandMenu.AttachTo(aAnchor);
            }
            mOwner.CommandMenu.Show(aUnit.Skills.Count > 0);
        }
        
        protected override void HideView() => mOwner.CommandMenu.Hide();

        protected override void Subscribe()
        {
            mOwner.CommandMenu.OnAttack += HandleAttack;
            mOwner.CommandMenu.OnSkill += HandleSkill;
            mOwner.CommandMenu.OnDetail += HandleDetail;
            mOwner.CommandMenu.OnBackRequested += mOwner.Back;
        }

        protected override void Unsubscribe()
        {
            mOwner.CommandMenu.OnAttack -= HandleAttack;
            mOwner.CommandMenu.OnSkill -= HandleSkill;
            mOwner.CommandMenu.OnDetail -= HandleDetail;
            mOwner.CommandMenu.OnBackRequested -= mOwner.Back;
        }

        private void HandleAttack()
        {
            var unit = (PPBattleUnit)mOwner.Context.Unit;
            mOwner.Context.TargetScope = TargetScope.SingleEnemy;
            mOwner.Context.CommandBuilder = tgt =>
                new PPAttackCommand(unit, mOwner.BuildResolver(new SingleEnemyResolver(), tgt));
            mOwner.Push(new PPTargetSelectState(mOwner));
        }
        
        private void HandleSkill() => mOwner.Push(new PPSkillSelectState(mOwner));
        
        private void HandleDetail() => mOwner.Push(new PPUnitDetailViewState(mOwner));
    }
}