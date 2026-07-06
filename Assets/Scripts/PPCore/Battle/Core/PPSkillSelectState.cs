/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillSelectState.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief スキル選択ステート
 * =====================================*/
using CommandBattleCore;

namespace PPCore
{
    public class PPSkillSelectState : IPPBattleInputState
    {
        private PPBattleCommandInputController mOwner;
        public PPSkillSelectState(PPBattleCommandInputController aOwner) => mOwner = aOwner;

        public void Enter()
        {
            var unit = mOwner.Context.Unit;
            // スキルボタン一覧の生成
            mOwner.SkillMenu.Show(unit, mOwner.Manager.Context);
            mOwner.SkillMenu.OnSkillSelected += HandleSkillSelected;
            mOwner.SkillMenu.OnBackRequested += HandleBack;
        }

        private void HandleSkillSelected(BattleSkill aSkill)
        {
            var unit = mOwner.Context.Unit;
            var scope = (aSkill.SourceDefinition as SkillDefinition)?.TargetScope ?? TargetScope.SingleEnemy;
            mOwner.Context.Skill = aSkill;
            mOwner.Context.TargetScope = scope;
            mOwner.Context.CommandBuilder = tgt =>
                new SkillCommand(unit, aSkill, mOwner.BuildResolver(aSkill.DefaultTargetResolver, tgt));

            // スキルの効果対象によってターゲット選択と行動決定を分岐
            if(PPTargeting.NeedsManualTarget(scope))
                mOwner.Push(new PPTargetSelectState(mOwner));
            else
                mOwner.Confirm();
        }

        private void HandleBack() => mOwner.Back(); // ユニット選択へ戻る

        public void Suspend()
        {
            Detach();
            mOwner.SkillMenu.Hide();
        }

        public void Resume()
        {
            mOwner.Context.ClearSelectionKeepingUnit();
            Enter();
        }

        public void Exit()
        {
            Detach();
            mOwner.SkillMenu.Hide();
        }

        private void Detach()
        {
            mOwner.SkillMenu.OnSkillSelected -= HandleSkillSelected;
            mOwner.SkillMenu.OnBackRequested -= HandleBack;
        }
    }
}