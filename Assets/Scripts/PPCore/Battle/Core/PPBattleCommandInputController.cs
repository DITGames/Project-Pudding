/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleCommandInputController.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief コマンド入力のコントローラー
 * =====================================*/
using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PPCore
{
    public class PPBattleCommandInputController : MonoBehaviour
    {
        [Label("ユニットビューバインダー")]
        [SerializeField] private PPBattleUnitViewBinder mViewBinder;
        [Label("コマンドメニュー")]
        [SerializeField] private PPBattleCommandMenuView mCommandMenu;
        [Label("スキルメニュー")]
        [SerializeField] private PPBattleSkillMenuView mSKillMenu;
        [Label("アイテムメニュー")]
        [SerializeField] private PPBattleItemMenuView mItemMenu;
        [Label("戻る")]
        [SerializeField] private Button mBackButton;

        private BattleManager mManager;
        private readonly Stack<IPPBattleInputState> mStateStack = new();
        private readonly PPBattleSelectionContext mContext = new();

        public event Action<BattleUnit, BattleCommandBase> OnCommandConfirmed;
        public event Action OnCommandFlushed;
        
        public BattleManager Manager => mManager;
        public PPBattleUnitViewBinder ViewBinder => mViewBinder;
        public PPBattleCommandMenuView CommandMenu => mCommandMenu;
        public PPBattleSkillMenuView SkillMenu => mSKillMenu;
        public PPBattleItemMenuView ItemMenu => mItemMenu;
        public PPBattleSelectionContext Context => mContext;

        public void Bind(BattleManager aManager)
        {
            mManager = aManager;
            if (mBackButton != null)
            {
                mBackButton.onClick.AddListener(Back);
            }
        }

        // ユニット選択から開始
        public void BeginCommandInput(IPPBattleInputState aInitial = null)
        {
            if (mManager == null || mManager.StateMachine.Current == BattleState.BattleEnd)
                return;
            mContext.Clear();
            ClearStack();
            Push(aInitial ?? new PPUnitSelectState(this));
        }

        // ユニットが選択された状態でコマンド選択から開始
        public void BeginCommandInputFor(BattleUnit aUnit)
        {
            if (mManager == null || mManager.StateMachine.Current == BattleState.BattleEnd)
                return;
            mContext.Clear();
            mContext.Unit = aUnit;
            ClearStack();
            Push(new PPCommandSelectState(this));
        }

        // 入力の中断処理(バトル終了・対象の消滅・キャンセルなど)
        public void Abort()
        {
            ClearStack();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
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
            var command = mContext.CommandBuilder?.Invoke(mContext.Target);
            if (command == null) return;
            
            OnCommandConfirmed?.Invoke(mContext.Unit, command);
            Flush(command);
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

        public ITargetResolver BuildResolver(ITargetResolver aDefault, BattleUnit aTarget)
            => aTarget == null
                ? aDefault
                : aDefault switch
                {
                    SingleEnemyResolver => new SingleEnemyResolver(aTarget),
                    SingleAllyResolver => new SingleAllyResolver(aTarget),
                    _ => aDefault
                };
    }
}