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
    
    [Header("UI")]
    [Label("バトルビューバインダー")]
    [SerializeField] private PPBattleUnitViewBinder mBattleUnitViewBinder;

    [Header("インプット")]
    [Label("コマンド入力コントローラ")]
    [SerializeField] private PPBattleCommandInputController mController;

    [Header("バトル設定")]
    [Label("ターン更新間隔")]
    [SerializeField] private float mTurnTickInterval = 5f;
    
    [Header("味方")]
    [Label("ユニット")]
    [SerializeField] private UnitDefinition mAllyUnit;
    
    [Header("敵")]
    [Label("ユニット")]
    [SerializeField] private UnitDefinition mEnemyUnit;
    [Label("敵AIプロファイル")]
    [SerializeField] private PPPartyAIProfileDefinition mEnemyAIProfile;
    [Label("デフォルト思考間隔")][EditCondition("HasEnemyAIProfile", true, true)]
    [SerializeField] private float mDefaultEnemyThinkDuration = 0.5f;
    private PPEnemyAIDriver mEnemyAIDriver;
    [Label("コイン取得")]
    [SerializeField] private Vector2Int mEnemyResourcePerTick = new Vector2Int(5, 10);
    
    private bool HasEnemyAIProfile => mEnemyAIProfile != null;
    
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
            Rules = new PPBattleRules(),
        };
        context.Rules.CastValidator = new PPBattleCastValidator();
        
        var enemyStrategist = new PPPartyAIStrategistBase(mEnemyAIProfile);
        ((PPBattleParty)context.EnemyParty).Strategist = enemyStrategist;

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
        
        float think = mEnemyAIProfile != null ? mEnemyAIProfile.ThinkInterval : mDefaultEnemyThinkDuration;
        mEnemyAIDriver = new PPEnemyAIDriver(mBattleManager, BattleSide.Enemy, enemyStrategist, think);
        mEnemyActionCoroutine = StartCoroutine(mEnemyAIDriver.RunLoop());
        mTickCoroutine = StartCoroutine(AdvanceTick());
    }

    void Update()
    {
        if (IsCommandInputPressed())
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

    IEnumerator AdvanceTick()
    {
        while (true)
        {
            if(mBattleManager == null) yield break;
            if(mBattleManager.StateMachine.Current == BattleState.BattleEnd) yield break;
            yield return new WaitForSeconds(mTurnTickInterval);
            
            var enemyParty = (PPBattleParty)mBattleManager.Context.GetParty(BattleSide.Enemy);
            if (enemyParty != null)
            {
                enemyParty.ResourcePool.Add(PPResourceType.Normal, mBattleManager.Context.Rules.RandomProvider.NextInt(mEnemyResourcePerTick.x, mEnemyResourcePerTick.y));
            }
            
            mBattleManager.AdvanceTick();
        }
    }

    private void HandleCommandFlushed()
    {
        mBattleManager.ExecuteNextCommand();
    }

    bool IsCommandInputPressed()
    {
        return 
            (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.triangleButton.wasPressedThisFrame);
    }
}
