/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPCommandSelectState.cs
 * @author hqrse
 * @date 2026/07/01
 * @brief コマンド選択ステート
 * =====================================*/
using CommandBattleCore;

namespace PPCore
{
    public class PPCommandSelectState : IPPBattleInputState
    {
        private readonly PPBattleCommandInputController mOwner;
        public PPCommandSelectState(PPBattleCommandInputController aOwner) => mOwner = aOwner;

        public void Enter()
        {
            var unit = mOwner.Context.Unit;
            var party = (PPBattleParty)mOwner.Manager.Context.GetParty(unit.Side);

            bool canSkill = unit.Skills.Count > 0;
            bool canItem = party.Inventory.HasAny;
            bool canSwap = party.ReserveMembers.Count > 0 && (unit.CurrentRestrictions & ActionRestriction.CannotSwap) == 0;
            
            mOwner.CommandMenu.Show(canSkill, canItem, canSwap);
            mOwner.CommandMenu.OnAttack += HandleAttack;
            mOwner.CommandMenu.OnSkill += HandleSkill;
            mOwner.CommandMenu.OnBackRequested += mOwner.Back;
        }

        void HandleAttack()
        {
            var unit = (PPBattleUnit)mOwner.Context.Unit;
            mOwner.Context.TargetScope = TargetScope.SingleEnemy;
            mOwner.Context.CommandBuilder = tgt =>
                new PPBattleAttackCommand(unit, mOwner.BuildResolver(new SingleEnemyResolver(), tgt));
            mOwner.Push(new PPTargetSelectState(mOwner));
        }
        
        private void HandleSkill() => mOwner.Push(new PPSkillSelectState(mOwner));

        public void Suspend() => Detach();

        public void Resume()
        {
            mOwner.Context.ClearSelectionKeepingUnit();
            Enter();
        }

        public void Exit()
        {
            Detach();
            mOwner.CommandMenu.Hide();
        }

        private void Detach()
        {
            mOwner.CommandMenu.OnAttack -= HandleAttack;
            mOwner.CommandMenu.OnSkill -= HandleSkill;
            mOwner.CommandMenu.OnBackRequested -= mOwner.Back;
            mOwner.CommandMenu.Hide();
        }
    }
}