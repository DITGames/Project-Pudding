/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleCommandInputController.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief PPコマンド入力のコントローラー
 * =====================================*/

using System;
using System.Collections.Generic;
using CommandBattleCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace PPCore
{
    public class PPBattleCommandInputController : MonoBehaviour
    {
        [Label("ユニットビューバインダー")]
        [SerializeField] private PPBattleUnitViewBinder mViewBinder;
        [Label("スキルメニュー")]
        [SerializeField] private PPBattleSkillMenuView mSKillMenu;
        [Label("戻る")]
        [SerializeField] private Button mBackButton;

        private BattleManager mManager;
        private readonly Stack<IPPBattleInputState> mStateStack = new();
        private readonly PPBattleSelectionContext mContext = new();

        public event Action<BattleUnit, BattleCommandBase> OnCommandConfirmed;
        public event Action OnCommandFlushed;
        
        public BattleManager Manager => mManager;
        public PPBattleUnitViewBinder ViewBinder => mViewBinder;
        public PPBattleSkillMenuView SkillMenu => mSKillMenu;
        public PPBattleSelectionContext Context => mContext;

        public void Bind(BattleManager aManager)
        {
            mManager = aManager;
            if (mBackButton != null)
            {
                mBackButton.onClick.AddListener(Back);
            }
        }

        public void BeginCommandInput()
        {
            if (mManager == null || mManager.StateMachine.Current == BattleState.BattleEnd) return;
            BeginUnitSelect();
        }

        public void BeginUnitSelect()
        {
            mContext.Clear();
            ClearStack();
            Push(new PPUnitSelectState(this));
        }

        public void Push(IPPBattleInputState aNext)
        {
            if(mStateStack.Count > 0) mStateStack.Peek().Suspend();
            mStateStack.Push(aNext);
            aNext.Enter();
        }

        public void Back()
        {
            // ひとつ前のコマンドに戻す
            if (mStateStack.Count > 0)
            {
                mStateStack.Pop().Exit();
                if(mStateStack.Count > 0)
                {
                    mStateStack.Peek().Resume();
                    return;
                }
            }
            BeginUnitSelect();
        }

        public void Confirm()
        {
            var command = BuildCommand(mContext);
            if (command != null)
            {
                OnCommandConfirmed?.Invoke(mContext.Unit, command);
                Flush(command);
            }
        }

        private void Flush(BattleCommandBase aCommand)
        {
            ClearStack();
            mManager.EnqueueCommand(aCommand);
            OnCommandFlushed?.Invoke();
        }

        private void ClearStack()
        {
            while (mStateStack.Count > 0)
            {
                mStateStack.Pop().Exit();
            }
        }

        private BattleCommandBase BuildCommand(PPBattleSelectionContext aContext)
        {
            var resolver = BuildResolver(aContext.Skill, aContext.Target);
            return new SkillCommand(aContext.Unit, aContext.Skill, resolver);
        }

        private ITargetResolver BuildResolver(BattleSkill aSkill, BattleUnit aTarget)
        {
            if (aTarget == null) return aSkill.DefaultTargetResolver;
            return aSkill.DefaultTargetResolver switch
            {
                SingleEnemyResolver => new SingleEnemyResolver(aTarget),
                SingleAllyResolver => new SingleAllyResolver(aTarget),
                _ => aSkill.DefaultTargetResolver
            };
        }
    }
}