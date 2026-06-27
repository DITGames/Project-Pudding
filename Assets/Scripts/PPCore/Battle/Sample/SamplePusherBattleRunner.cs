/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file SamplePusherBattleRunner.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief バトルのフロー制御サンプル
 * =====================================*/
using System.Collections;
using CommandBattleCore;
using PPCore;
using UnityEngine;

public class SamplePusherBattleRunner : MonoBehaviour
{
    [Header("コイン")]
    [Label("コインリソースブリッジ")]
    [SerializeField] private PPCoinResourceBridge mCoinResourceBridge;
    [Label("コイン上限")]
    [SerializeField] private int mMaxCoin = 100;
    [Label("コイン獲得レート初期値")]
    [SerializeField] private float mBaseCoinConversionRate = 1f;
    
    [Header("バトル設定")]
    [Label("攻撃コスト")]
    [SerializeField] private int mAttackCost;
    [Label("味方ユニット")]
    [SerializeField] private UnitDefinition mAllyUnit;
    [Label("敵ユニット")]
    [SerializeField] private UnitDefinition mEnemyUnit;
    
    [Header("UI")]
    [Label("バトルビューバインダー")]
    [SerializeField] private PPBattleUnitViewBinder mBattleUnitViewBinder;

    [Header("サンプル")]
    [Label("味方攻撃間隔")]
    [SerializeField] private float mAllyAttackInterval = 3f;
    [Label("敵攻撃間隔")]
    [SerializeField] private float mEnemyAttackInterval = 10f;
    
    private BattleManager mBattleManager = new();
    
    private Coroutine mBattleCoroutine;
    
    void Start()
    {
        mBattleManager = new BattleManager { TimeProvider = () => Time.time };

        var allyUnit = mAllyUnit.CreateRuntimeUnit();
        var enemyUnit = mEnemyUnit.CreateRuntimeUnit();
        
        var context = new BattleContext()
        {
            AllyParty = new PPBattleParty(mMaxCoin, mBaseCoinConversionRate, BattleSide.Ally, new[]{allyUnit}),
            EnemyParty = new PPBattleParty(mMaxCoin, mBaseCoinConversionRate, BattleSide.Enemy, new[]{enemyUnit}),
        };
        context.Rules.CastValidator = new PPBattleCastValidator();

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

        mBattleCoroutine = StartCoroutine(StartEnemyAction());
    }
    
    IEnumerator StartAllyAction()
    {
        while (true)
        {
            if(mBattleManager == null) yield break;
            if (mBattleManager.StateMachine.Current == BattleState.BattleEnd) yield break;
            yield return new WaitForSeconds(mAllyAttackInterval);
            
            var units = mBattleManager.Context.GetParty(BattleSide.Ally);
            foreach (var unit in units.ActiveMembers)
            {
                mBattleManager.EnqueueCommand(unit.CommandDecider.DecideCommand(unit, mBattleManager.Context));
            }
            mBattleManager.ExecuteNextCommand();
        }
    }

    IEnumerator StartEnemyAction()
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
