/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillSelectState.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief PPスキル選択コマンド
 * =====================================*/

using CommandBattleCore;
using UnityEngine;
using UnityEngine.UIElements;

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
            mOwner.Context.Skill = aSkill;

            // スキルの効果対象によってターゲット選択と行動決定を分岐
            bool needsTarget = aSkill.DefaultTargetResolver is SingleEnemyResolver or SingleAllyResolver;
            if(needsTarget) mOwner.Push(new PPTargetSelectState(mOwner));
            else mOwner.Confirm();
        }

        private void HandleBack() => mOwner.Back(); // ユニット選択へ戻る

        public void Suspend()
        {
            Detach();
            mOwner.SkillMenu.Hide();
        }

        public void Resume()
        {
            mOwner.Context.Skill = null;
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