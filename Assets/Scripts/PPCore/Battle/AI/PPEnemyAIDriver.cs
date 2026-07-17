/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPEnemyAIDriver.cs
 * @author hqrse
 * @date 2026/07/18
 * @brief パーティ戦略を一定間隔で駆動するクラス
 * =====================================*/

using System.Collections;
using System.Linq;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    public sealed class PPEnemyAIDriver
    {
        private readonly BattleManager mManager;
        private readonly BattleSide mSide;
        private readonly IPPPartyCommandStrategist mPartyCommandStrategist;
        private readonly float mThinkInterval;

        public PPEnemyAIDriver(BattleManager aManager, BattleSide aSide,
            IPPPartyCommandStrategist aPartyCommandStrategist, float aThinkInterval)
        {
            mManager = aManager;
            mSide = aSide;
            mPartyCommandStrategist = aPartyCommandStrategist;
            mThinkInterval = Mathf.Max(0.1f, aThinkInterval);
        }

        public IEnumerator RunLoop()
        {
            var wait = new WaitForSeconds(mThinkInterval);
            while (mManager != null && mManager.StateMachine.Current != BattleState.BattleEnd)
            {
                yield return wait;
                PlanAndExecuteOnce();
            }
        }

        public void PlanAndExecuteOnce()
        {
            if(mManager == null) return;
            
            var state = mManager.StateMachine.Current;
            if(state == BattleState.BattleEnd || state == BattleState.ActionExecution)
                return;
            
            var party = mManager.Context.GetParty(mSide) as PPBattleParty;
            if(party == null)
                return;
            
            var strategist = party.Strategist ?? mPartyCommandStrategist;
            if(strategist == null)
                return;
            
            var plan = strategist.PlanActions(party, mManager.Context);
            if(plan == null || plan.IsWait) return;

            foreach (var a in plan.Assignments.OrderBy(x => x.Order))
            {
                mManager.EnqueueCommand(a.Command);
            }
            
            mManager.ExecuteAllCommands();
        }
    }
}