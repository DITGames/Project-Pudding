/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file SampleBattleRunner.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief バトルのフロー制御サンプル
 * =====================================*/
using System.Collections;
using System.Collections.Generic;
using CommandBattleCore;
using PPBattle;
using UnityEngine;

public class SampleBattleRunner : MonoBehaviour
{
    [Label("コインカウンター")]
    [SerializeField] private CoinDropCounter mCoinCounter;
    [Label("攻撃コスト")]
    [SerializeField] private int mAttackCost;
    [Label("味方ユニット")]
    [SerializeField] private UnitDefinition mAllyUnit;
    [Label("敵ユニット")]
    [SerializeField] private UnitDefinition mEnemyUnit;
    [Label("敵攻撃間隔")]
    [SerializeField] private float mEnemyAttackInterval = 10f;
    [Label("バトルビューバインダー")]
    [SerializeField] private PPBattleUnitViewBinder mBattleUnitViewBinder;
    
    private BattleManager mBattleManager = new();
    
    private Coroutine mBattleCoroutine;
    
    private int mCurrentCost = 0;
    void Start()
    {
        if (mCoinCounter != null) mCoinCounter.OnCoinDropped += HandleCoinDropped;
        mBattleManager = new BattleManager { TimeProvider = () => Time.time };

        var allyUnit = mAllyUnit.CreateRuntimeUnit();
        var enemyUnit = mEnemyUnit.CreateRuntimeUnit();

        List<BattleUnit> allyParty = new();
        allyParty.Add(allyUnit);
        List<BattleUnit> enemyParty = new();
        enemyParty.Add(enemyUnit);
        
        var context = new BattleContext()
        {
            AllyParty = new BattleParty(BattleSide.Ally, allyParty),
            EnemyParty = new BattleParty(BattleSide.Enemy, enemyParty),
        };

        mBattleManager.OnDamageTaken += (u, d) =>
        {
            Debug.Log($"{u.DisplayName} damaged {d} : HP{u.Parameters.Hp.CurrentValue}");
        };
        mBattleManager.OnUnitDefeated += u =>
        {
            Debug.Log($"{u.DisplayName} defeated!");
        };
        mBattleManager.OnBattleEnded += r =>
        {
            Debug.Log($"Battle Ended! {r.Type}");
            if (mBattleCoroutine != null)
            {
                StopCoroutine(mBattleCoroutine);
                mBattleCoroutine = null;
            }
        };
        mBattleManager.StartBattle(context);
        mBattleUnitViewBinder.Bind(mBattleManager);

        mBattleCoroutine = StartCoroutine(StartAttack());
    }

    private void OnDestroy()
    {
        if(mCoinCounter != null) mCoinCounter.OnCoinDropped -= HandleCoinDropped;
    }

    void HandleCoinDropped(int aCount)
    {
        mCurrentCost++;

        if (mCurrentCost >= mAttackCost)
        {
            mCurrentCost = 0;
            if (mBattleManager == null) return;
            if (mBattleManager.StateMachine.Current == BattleState.BattleEnd) return;

            var units = mBattleManager.Context.GetParty(BattleSide.Ally);

            foreach (var unit in units.ActiveMembers)
            {
                mBattleManager.EnqueueCommand(unit.CommandDecider.DecideCommand(unit, mBattleManager.Context));
            }

            mBattleManager.ExecuteNextCommand();
        }
    }

    IEnumerator StartAttack()
    {
        while (true)
        {
            if(mBattleManager == null) yield break;
            if (mBattleManager.StateMachine.Current == BattleState.BattleEnd) yield break;
            yield return new WaitForSeconds(mEnemyAttackInterval);

            var units = mBattleManager.Context.GetParty(BattleSide.Enemy);

            foreach (var unit in units.ActiveMembers)
            {
                mBattleManager.EnqueueCommand(unit.CommandDecider.DecideCommand(unit, mBattleManager.Context));
            }

            mBattleManager.ExecuteNextCommand();
        }
    }

}
