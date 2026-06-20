/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file TurnBattleSample.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief ターン制バトルのサンプル
 * =====================================*/

using System.Linq;
using UnityEngine;

namespace CommandBattleCore
{
    public class TurnBattleRunnerSample : MonoBehaviour
    {
        [SerializeField] private UnitDefinition[] mAllyDefinitions;
        [SerializeField] private UnitDefinition[] mEnemyDefinitions;

        private BattleManager mBattleManager;

        private void Start()
        {
            mBattleManager = new BattleManager {TimeProvider = () => Time.time};

            var allies = mAllyDefinitions.Select(d => d.CreateRuntimeUnit()).ToList();
            var enemies = mEnemyDefinitions.Select(d => d.CreateRuntimeUnit()).ToList();

            var context = new BattleContext()
            {
                AllyParty = new BattleParty(BattleSide.Ally, allies),
                EnemyParty = new BattleParty(BattleSide.Enemy, enemies),
            };

            mBattleManager.OnDamageTaken += (u, d) =>
                Debug.Log($"{u.DisplayName} <- {d} dmg (HP: {u.Parameters.Hp.CurrentValue})");
            mBattleManager.OnUnitDefeated += u => Debug.Log($"{u.DisplayName} defeated!");
            mBattleManager.OnBattleEnded += r => Debug.Log($"Battle End: {r.Type}");
            
            mBattleManager.StartBattle(context);
        }

        [ContextMenu("Run One Turn")]
        public void RunOneTurn()
        {
            if (mBattleManager.StateMachine.Current == BattleState.BattleEnd) return;
            
            // 行動順を取得
            var units = mBattleManager.GetTurnOrder();
            
            // コマンドを選択
            foreach (var unit in units)
            {
                mBattleManager.EnqueueCommand(unit.CommandDecider.DecideCommand(unit, mBattleManager.Context));
            }
            
            mBattleManager.ExecuteAllCommands();
            mBattleManager.AdvanceTick();
        }
    }
}