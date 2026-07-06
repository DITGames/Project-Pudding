/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPItemSelectState.cs
 * @author hqrse
 * @date 2026/07/02
 * @brief アイテム選択ステート
 * =====================================*/

using CommandBattleCore;

namespace PPCore
{
    public class PPItemSelectState : IPPBattleInputState
    {
        private readonly PPBattleCommandInputController mOwner;
        public PPItemSelectState(PPBattleCommandInputController aOwner) => mOwner = aOwner;

        public void Enter()
        {
            var party = (PPBattleParty)mOwner.Manager.Context.GetParty(mOwner.Context.Unit.Side);
            mOwner.ItemMenu.Show(party, mOwner.Manager.Context);
            mOwner.ItemMenu.OnItemSelected += HandleItemSelected;
            mOwner.ItemMenu.OnBackRequested += mOwner.Back;
        }

        private void HandleItemSelected(PPItemDefinition aDefinition)
        {
            var unit = mOwner.Context.Unit;
            var scope = aDefinition.Target;
            mOwner.Context.TargetScope = scope;
            mOwner.Context.CommandBuilder =
                tgt => new PPItemCommand(unit, aDefinition, mOwner.BuildResolver(scope.CreateResolver(), tgt));

            if (PPTargeting.NeedsManualTarget(scope))
                mOwner.Push(new PPTargetSelectState(mOwner));
            else
                mOwner.Confirm();
        }

        public void Suspend()
        {
            Detach();
            mOwner.ItemMenu.Hide();
        }

        public void Resume()
        {
            mOwner.Context.ClearSelectionKeepingUnit();
            Enter();
        }

        public void Exit()
        {
            Detach();
            mOwner.ItemMenu.Hide();
        }

        private void Detach()
        {
            mOwner.ItemMenu.OnItemSelected -= HandleItemSelected;
            mOwner.ItemMenu.OnBackRequested -= mOwner.Back;
        }
    }
}