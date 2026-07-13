/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file SamplePusherBattleRunner.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief バトルのフロー制御サンプル
 * =====================================*/
using System.Collections;
using System.Collections.Generic;
using CommandBattleCore;
using PPCore;
using UnityEngine;
using UnityEngine.InputSystem;

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
    [Label("味方ユニット")]
    [SerializeField] private UnitDefinition mAllyUnit;
    [Label("敵ユニット")]
    [SerializeField] private UnitDefinition mEnemyUnit;
    
    [Header("UI")]
    [Label("バトルビューバインダー")]
    [SerializeField] private PPBattleUnitViewBinder mBattleUnitViewBinder;

    [Header("インプット")]
    [Label("コマンド入力コントローラ")]
    [SerializeField] private PPBattleCommandInputController mController;

    [Header("バトル設定")]
    [Label("ターン更新間隔")]
    [SerializeField] private float mTurnTickInterval = 1f;
    
    [Header("サンプル")]
    [Label("敵攻撃間隔")]
    [SerializeField] private float mEnemyAttackInterval = 10f;
    
    private BattleManager mBattleManager = new();
    
    private Coroutine mEnemyActionCoroutine;
    private Coroutine mTickCoroutine;
    
    void Start()
    {
        mBattleManager = new BattleManager { TimeProvider = () => Time.time };

        var allyUnit = mAllyUnit.CreateRuntimeUnit();
        var enemyUnit = mEnemyUnit.CreateRuntimeUnit();
        
        var context = new BattleContext()
        {
            AllyParty = new PPBattleParty(mMaxCoin, mBaseCoinConversionRate, BattleSide.Ally, new[]{allyUnit}, null, new Dictionary<PPItemDefinition, int>()),
            EnemyParty = new PPBattleParty(mMaxCoin, mBaseCoinConversionRate, BattleSide.Enemy, new[]{enemyUnit}, null , new Dictionary<PPItemDefinition, int>()),
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
            if (mEnemyActionCoroutine != null)
            {
                StopCoroutine(mEnemyActionCoroutine);
                mEnemyActionCoroutine = null;
            }

            if (mTickCoroutine != null)
            {
                StopCoroutine(mTickCoroutine);
                mTickCoroutine = null;
            }
        };
        mBattleManager.StartBattle(context);
        mBattleUnitViewBinder.Bind(mBattleManager);
        mCoinResourceBridge.Bind(mBattleManager, BattleSide.Ally);
        
        mController.Bind(mBattleManager);
        mController.OnCommandFlushed += HandleCommandFlushed;

        mEnemyActionCoroutine = StartCoroutine(StartEnemyAction());
    }

    void Update()
    {
        if (Keyboard.current.cKey.wasPressedThisFrame || Gamepad.current.triangleButton.wasPressedThisFrame)
        {
            if (CanSelectAnyCommand())
            {
                mController.BeginCommandInput();   
            }
        }
    }

    private bool CanSelectAnyCommand()
    {
        if (mBattleManager.StateMachine.Current == BattleState.BattleEnd)
            return false;
        
        var allyParty = mBattleManager.Context.GetParty(BattleSide.Ally);
        foreach (var unit in allyParty.ActiveMembers)
        {
            if (unit is PPBattleUnit ppUnit)
            {
                // 何らかの発動可能なスキルがあるか?
                if (ppUnit.CanValidateSkill(mBattleManager.Context))
                    return true;
            }
        }
        return false;
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

    IEnumerator AdvanceTick()
    {
        while (true)
        {
            if(mBattleManager == null) yield break;
            if(mBattleManager.StateMachine.Current == BattleState.BattleEnd) yield break;
            yield return new WaitForSeconds(mTurnTickInterval);
            
            mBattleManager.AdvanceTick();
        }
    }

    private void HandleCommandFlushed()
    {
        mBattleManager.ExecuteNextCommand();
    }
}
