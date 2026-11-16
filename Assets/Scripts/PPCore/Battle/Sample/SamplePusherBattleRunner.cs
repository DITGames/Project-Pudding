/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file SamplePusherBattleRunner.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief バトルのフロー制御サンプル
 * =====================================*/

using System.Collections;
using System.Collections.Generic;
using PPCore;
using UnityEngine;
using UnityEngine.InputSystem;
using AttributeUtility;

// プッシャーとバトルを繋いだ一連のフローを組み立てるエントリポイント（参照実装）
// ティックでターンを進めつつ、プレイヤーは任意のタイミングでコマンド入力を開始できる（入力中は timeScale が 0）
public class SamplePusherBattleRunner : MonoBehaviour, IPPPendingActionSource
{
    /* インスペクタ設定 */

    // プッシャーのコイン獲得をゲージへ橋渡しするブリッジ
    [Header("コイン")]
    [Label("コインゲージブリッジ")]
    [SerializeField] private PPCoinResourceBridge mCoinResourceBridge;

    // コイン 1 枚あたりのゲージ変換係数の初期値
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

    // 1 ティックあたりの思考回数。判断の中身はユニット定義側のビヘイビアツリーが決める
    [Label("思考回数(1ティックあたり)")]
    [SerializeField] private int mThinkCountPerTick = 1;
    // 敵がティックごとに得るゲージ量。敵にはプッシャーが無いため直接補給する
    [Label("敵ゲージ供給")]
    [SerializeField] private PPResourceSimulation mEnemyResourceSimulation = new ();

    /* 状態 */

    // 敵 AI を駆動するドライバ
    private PPEnemyAIDriver mEnemyAIDriver;
    // 敵の思考ルーチン。バトル進行のイベントを購読しているため、終了時に外せるよう持っておく
    private IPPPartyCommandStrategist mEnemyStrategist;
    // 1 ティック分の行動を集めて並べ替える収集役
    private readonly PPTickActionCollector mActionCollector = new();
    // バトル進行を統括するマネージャ
    private BattleManager mBattleManager = new();
    // 敵 AI のループを回しているコルーチン
    private Coroutine mEnemyActionCoroutine;
    // ターン経過を回しているコルーチン
    private Coroutine mTickCoroutine;

    /* Unity イベント */

    // バトルを組み立てて開始する。ユニット生成 → コンテキスト構築 → 敵 AI の注入 → イベント購読 → バトル開始 →
    // View・コインブリッジ・入力の接続 → AI とティックのコルーチン起動、の順に進む
    void Start()
    {
        mBattleManager = new BattleManager { TimeProvider = () => Time.time };

        var allyUnit = mAllyUnit.CreateRuntimeUnit();
        var enemyUnit = mEnemyUnit.CreateRuntimeUnit();

        var context = new BattleContext()
        {
            AllyParty = new PPBattleParty(mBaseCoinConversionRate, BattleSide.Ally, new[]{allyUnit}),
            EnemyParty = new PPBattleParty(mBaseCoinConversionRate, BattleSide.Enemy, new[]{enemyUnit}),
            Rules = new PPBattleRules(),
        };
        // スキルゲージ残量を検証するバリデータへ差し替える
        context.Rules.CastValidator = new PPBattleCastValidator();

        mEnemyStrategist = new PPUnitAIStrategist();
        ((PPBattleParty)context.EnemyParty).Strategist = mEnemyStrategist;

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

            // 思考ルーチンがバトル進行のイベントを拾い続けないよう、購読も併せて外す
            mEnemyStrategist?.Unbind();
        };
        PPBattleLogBinder.Bind(mBattleManager, context);
        mBattleManager.StartBattle(context);
        mBattleUnitViewBinder.Bind(mBattleManager);
        mCoinResourceBridge.Bind(mBattleManager, BattleSide.Ally);

        mController.Bind(mBattleManager);
        mController.ActionLedger = mActionCollector.Ledger;
        mController.OnCommandConfirmed += HandleCommandConfirmed;

        // バトル中の出来事と実行待ちの行動は思考ルーチンからは辿れないため、開始時に渡す
        mEnemyStrategist.BindBattle(mBattleManager, BattleSide.Enemy, this);

        // 思考間隔はティック間隔を思考回数で割って決まるため、ドライバへはその 2 つをそのまま渡す
        mEnemyAIDriver = new PPEnemyAIDriver(mBattleManager, BattleSide.Enemy, mEnemyStrategist, mTurnTickInterval, mThinkCountPerTick);
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

    // バトルが決着しないまま破棄された場合にも、思考ルーチンの購読を外す
    void OnDestroy() => mEnemyStrategist?.Unbind();

    /* 操作 */

    // 一定間隔でターンを進めるコルーチン
    // プッシャーを持たない敵には、ティックごとにランダム量のゲージを直接補給する
    IEnumerator AdvanceTick()
    {
        while (true)
        {
            if(mBattleManager == null) yield break;
            if(mBattleManager.StateMachine.Current == BattleState.BattleEnd) yield break;
            yield return new WaitForSeconds(mTurnTickInterval);

            var enemyParty = (PPBattleParty)mBattleManager.Context.GetParty(BattleSide.Enemy);
            mEnemyResourceSimulation.Supply(enemyParty, mBattleManager.Context);

            ExecuteTickActions();
            mBattleManager.AdvanceTick();
        }
    }

    // このティック分の行動（プレイヤーの予約・指示が無いユニットの通常攻撃・敵 AI の計画）を集めて実行する
    // 優先度と速度で並べ直してから流す
    private void ExecuteTickActions()
    {
        var context = mBattleManager.Context;
        mActionCollector.FillDefaultAttacks((PPBattleParty)context.GetParty(BattleSide.Ally), context);

        foreach (var action in mActionCollector.CollectOrdered(context, mEnemyAIDriver?.LatestPlan))
        {
            mBattleManager.EnqueueCommand(action.Command);
        }
        mBattleManager.ExecuteAllCommands();

        mActionCollector.Clear();
        mEnemyAIDriver?.ConsumePlan();
    }

    /* 問い合わせ */

    // 指定した陣営の、まだ実行されていない行動を列挙する
    // 思考時点ではコマンド列が空のため、プレイヤーの予約と敵 AI の計画を直接読む
    public IEnumerable<PPPendingAction> EnumeratePending(BattleSide aSide)
    {
        foreach (var reservation in mActionCollector.Reservations)
        {
            if (reservation.Unit != null && reservation.Unit.Side == aSide) yield return reservation;
        }

        if (mEnemyAIDriver == null || mEnemyAIDriver.Side != aSide || mEnemyAIDriver.LatestPlan == null) yield break;

        foreach (var assignment in mEnemyAIDriver.LatestPlan.Assignments)
        {
            yield return PPPendingAction.FromCommand(assignment.Unit, assignment.Command);
        }
    }

    // 今コマンド入力を始める意味があるかを判定する
    // 味方の中に発動可能なスキルを持つユニットが 1 体でも居れば true
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

    // コマンド入力の開始操作が行われたかを判定する
    // return : キーボードの C、またはゲームパッドの△が押された場合 true
    bool IsCommandInputPressed()
    {
        return
            (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.triangleButton.wasPressedThisFrame);
    }

    /* イベントハンドラ */

    // プレイヤーが確定したコマンドを、このティックの行動として予約する
    private void HandleCommandConfirmed(BattleUnit aUnit, BattleCommandBase aCommand)
    {
        if (aUnit is not PPBattleUnit unit) return;

        mActionCollector.TryReserve(unit, aCommand);
    }
}
