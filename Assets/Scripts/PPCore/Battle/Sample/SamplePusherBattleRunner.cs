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

// プッシャーとバトルを繋いだ一連のフローを組み立てるエントリポイント
// バトルの組み立て方（パーティ生成 → Rules 設定 → Strategist 注入 →
// View / 入力 / コイン橋渡しの Bind → AI ドライバとティックのコルーチン起動）を
// 一通り示す参照実装で、新しくバトルを立ち上げる際はこの流れをなぞる
// 本作のバトルはターン制ではなく、プッシャーと並行してリアルタイムに進む
// 一定間隔のティックでターンを進めつつ、プレイヤーは任意のタイミングで
// コマンド入力を開始できる（入力中は timeScale が 0 になり盤面が止まる）
public class SamplePusherBattleRunner : MonoBehaviour
{
    // プッシャーのコイン獲得をリソースへ橋渡しするブリッジ
    [Header("コイン")]
    [Label("コインリソースブリッジ")]
    [SerializeField] private PPCoinResourceBridge mCoinResourceBridge;
    // 属性ごとのリソース上限
    [Label("コイン上限")]
    [SerializeField] private int mMaxCoin = 100;
    // コイン 1 枚あたりのリソース変換係数の初期値
    [Label("コイン獲得レート初期値")]
    [SerializeField] private float mBaseCoinConversionRate = 1f;

    // ユニットとビューを対応付けるバインダー
    [Header("UI")]
    [Label("バトルビューバインダー")]
    [SerializeField] private PPBattleUnitViewBinder mBattleUnitViewBinder;

    // プレイヤーのコマンド入力を扱うコントローラー
    [Header("インプット")]
    [Label("コマンド入力コントローラ")]
    [SerializeField] private PPBattleCommandInputController mController;

    // ターン経過の間隔（秒）
    [Header("バトル設定")]
    [Label("ターン更新間隔")]
    [SerializeField] private float mTurnTickInterval = 5f;

    // 味方として生成するユニット定義
    [Header("味方")]
    [Label("ユニット")]
    [SerializeField] private UnitDefinition mAllyUnit;

    // 敵として生成するユニット定義
    [Header("敵")]
    [Label("ユニット")]
    [SerializeField] private UnitDefinition mEnemyUnit;
    // 敵 AI の性格プロファイル。未設定でも既定値で動作する
    [Label("敵AIプロファイル")]
    [SerializeField] private PPPartyAIProfileDefinition mEnemyAIProfile;
    // プロファイル未設定時に使う思考間隔（秒）
    [Label("デフォルト思考間隔")][EditCondition("HasEnemyAIProfile", true, true)]
    [SerializeField] private float mDefaultEnemyThinkDuration = 0.5f;
    // 敵 AI を駆動するドライバ
    private PPEnemyAIDriver mEnemyAIDriver;
    // 敵がティックごとに得るリソース量の範囲（最小, 最大）
    [Label("コイン取得")]
    [SerializeField] private Vector2Int mEnemyResourcePerTick = new Vector2Int(5, 10);

    // 敵 AI プロファイルが設定されているか。思考間隔の入力欄の出し分けに使う
    private bool HasEnemyAIProfile => mEnemyAIProfile != null;

    // バトル進行を統括するマネージャ
    private BattleManager mBattleManager = new();

    // 敵 AI のループを回しているコルーチン
    private Coroutine mEnemyActionCoroutine;
    // ターン経過を回しているコルーチン
    private Coroutine mTickCoroutine;

    // バトルを組み立てて開始する
    // ユニット生成 → コンテキスト構築 → 敵 AI の注入 → ログ用イベント購読 → バトル開始 →
    // View・コインブリッジ・入力の接続 → AI とティックのコルーチン起動、の順に進む
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
        // リソース消費を検証するバリデータへ差し替える
        context.Rules.CastValidator = new PPBattleCastValidator();

        var enemyStrategist = new PPPartyAIStrategistBase(mEnemyAIProfile);
        ((PPBattleParty)context.EnemyParty).Strategist = enemyStrategist;

        mBattleManager.OnBattleEnded += r =>
        {
            // 終了後もコルーチンが回り続けないよう確実に止める
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
        PPBattleLogBinder.Bind(mBattleManager, context);
        mBattleManager.StartBattle(context);
        mBattleUnitViewBinder.Bind(mBattleManager);
        mCoinResourceBridge.Bind(mBattleManager, BattleSide.Ally);

        mController.Bind(mBattleManager);
        mController.OnCommandFlushed += HandleCommandFlushed;

        // プロファイルがあればその思考間隔を優先する
        float think = mEnemyAIProfile != null ? mEnemyAIProfile.ThinkInterval : mDefaultEnemyThinkDuration;
        mEnemyAIDriver = new PPEnemyAIDriver(mBattleManager, BattleSide.Enemy, enemyStrategist, think);
        mEnemyActionCoroutine = StartCoroutine(mEnemyAIDriver.RunLoop());
        mTickCoroutine = StartCoroutine(AdvanceTick());
    }

    // コマンド入力キーの押下を監視し、行動可能なユニットが居る場合のみ入力を開始する
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

    // 今コマンド入力を始める意味があるかを判定する
    // 味方の中に発動可能なスキルを持つユニットが 1 体でも居れば true
    // リソース不足で何も撃てない状態のときに入力画面が開くのを防ぐ
    // return : 入力を開始できる場合 true
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

    // 一定間隔でターンを進めるコルーチン
    // 敵にはプッシャーが無いため、ティックごとにランダム量のリソースを直接補給して
    // プレイヤー側のコイン収入と釣り合いを取っている
    // return : コルーチンの列挙子
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
                enemyParty.ResourcePool.Add(PPTypeAttribute.Normal, mBattleManager.Context.Rules.RandomProvider.NextInt(mEnemyResourcePerTick.x, mEnemyResourcePerTick.y));
            }

            mBattleManager.AdvanceTick();
        }
    }

    // プレイヤーのコマンドがキューへ流された直後に、それを即座に実行する
    private void HandleCommandFlushed()
    {
        mBattleManager.ExecuteNextCommand();
    }

    // コマンド入力の開始操作が行われたかを判定する
    // return : キーボードの C、またはゲームパッドの△が押された場合 true
    bool IsCommandInputPressed()
    {
        return
            (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.triangleButton.wasPressedThisFrame);
    }
}
