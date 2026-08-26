/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleRunner.cs
 * @author hqrse
 * @date 2026/08/02
 * @brief バトルのフロー制御(本番用)
 * =====================================*/

using System.Collections;
using CommandBattleCore;
using UnityEngine;
using UnityEngine.InputSystem;
using AttributeUtility;

namespace PPCore
{
    // プッシャーとバトルを繋いだ一連のフローを組み立てるエントリポイント(本番用)
    // SamplePusherBattleRunner をベースに、パーティ生成を PPPartyFactory 経由に変更し
    // 味方パーティのデバッグ編集・オートバトル切り替えに対応させたもの
    // パーティ生成・ティック進行・入力接続の基本的な流れはサンプルを踏襲する
    public class PPBattleRunner : MonoBehaviour
    {
        // プッシャーのコイン獲得をゲージへ橋渡しするブリッジ
        [Header("コイン")]
        [Label("コインゲージブリッジ")]
        [SerializeField] private PPCoinResourceBridge mCoinResourceBridge;

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
        // 敵がティックごとに得るゲージ量。敵にはプッシャーが無いため直接補給する
        [Label("敵ゲージ供給")]
        [SerializeField] private PPResourceSimulation mEnemyResourceSimulation = new ();

        // 味方として編成するパーティ定義。デバッグ用にエディタから直接割り当てる
        [Header("パーティ")]
        [Label("味方パーティ定義")]
        [SerializeField] private PPPartyDefinition mAllyPartyDefinition;
        // 敵として編成するパーティ定義
        [Label("敵パーティ定義")]
        [SerializeField] private PPPartyDefinition mEnemyPartyDefinition;

        // バトル開始時点で味方をAI操作（オートバトル）にするかどうか
        [Header("デバッグ")]
        [Label("オートバトル(初期状態)")]
        [SerializeField] private bool mIsAutoBattle = false;
        // trueの場合、プッシャーのコイン獲得の代わりにティックごとのゲージ加算で味方を進行させる
        // 実機のコインプッシャーが無いシミュレーション環境向け
        [Label("味方ゲージをシミュレーション供給")]
        [SerializeField] private bool mIsSimulateAllyResource = false;
        // 味方がティックごとに得るゲージ量。mIsSimulateAllyResourceがtrueのときのみ使用
        [Label("味方ゲージ供給(シミュレーション)")]
        [EditCondition(nameof(mIsSimulateAllyResource), true, false)]
        [SerializeField] private PPResourceSimulation mAllyResourceSimulation = new ();

        // バトル進行を統括するマネージャ
        private BattleManager mBattleManager = new();

        // 1 ティック分の行動を集めて並べ替える収集役
        // プレイヤーの予約はここへ積まれ、ティックの終わりにまとめて実行される
        private readonly PPTickActionCollector mActionCollector = new();

        // 敵AIを駆動するドライバ
        private PPEnemyAIDriver mEnemyAIDriver;
        // 味方AIを駆動するドライバ（オートバトル中のみコルーチンを起動する）
        private PPEnemyAIDriver mAllyAIDriver;

        // 敵AIのループを回しているコルーチン
        private Coroutine mEnemyActionCoroutine;
        // 味方AIのループを回しているコルーチン。手動モード中は null
        private Coroutine mAllyActionCoroutine;
        // ターン経過を回しているコルーチン
        private Coroutine mTickCoroutine;

        // バトルを組み立てて開始する
        // ユニット生成 → コンテキスト構築 → 敵・味方AIの注入 → ログ用イベント購読 → バトル開始 →
        // View・コインブリッジ・入力の接続 → AIとティックのコルーチン起動、の順に進む
        void Start()
        {
            mBattleManager = new BattleManager { TimeProvider = () => Time.time };

            var context = new BattleContext()
            {
                AllyParty = PPPartyFactory.CreateFromDefinition(mAllyPartyDefinition, BattleSide.Ally),
                EnemyParty = PPPartyFactory.CreateFromDefinition(mEnemyPartyDefinition, BattleSide.Enemy),
                Rules = new PPBattleRules(),
            };
            // スキルゲージ残量を検証するバリデータへ差し替える
            context.Rules.CastValidator = new PPBattleCastValidator();

            var enemyStrategist = CreateStrategist(mEnemyPartyDefinition);
            ((PPBattleParty)context.EnemyParty).Strategist = enemyStrategist;

            // 味方も常にStrategistを持たせておき、オートバトルボタンでいつでも思考を開始できるようにする
            var allyStrategist = CreateStrategist(mAllyPartyDefinition);
            ((PPBattleParty)context.AllyParty).Strategist = allyStrategist;

            mBattleManager.OnBattleEnded += r =>
            {
                // 終了後もコルーチンが回り続けないよう確実に止める
                StopCoroutineIfRunning(ref mEnemyActionCoroutine);
                StopCoroutineIfRunning(ref mAllyActionCoroutine);
                StopCoroutineIfRunning(ref mTickCoroutine);
            };
            PPBattleLogBinder.Bind(mBattleManager, context);
            mBattleManager.StartBattle(context);
            mBattleUnitViewBinder.Bind(mBattleManager);
            // シミュレーション供給時はプッシャーのコイン獲得と二重に加算されないよう購読を行わない
            if (!mIsSimulateAllyResource)
            {
                mCoinResourceBridge.Bind(mBattleManager, BattleSide.Ally);
            }

            mController.Bind(mBattleManager);
            mController.ActionLedger = mActionCollector.Ledger;
            mController.OnCommandConfirmed += HandleCommandConfirmed;

            mEnemyAIDriver = CreateDriver(BattleSide.Enemy, enemyStrategist, mEnemyPartyDefinition);
            mEnemyActionCoroutine = StartCoroutine(mEnemyAIDriver.RunLoop());

            mAllyAIDriver = CreateDriver(BattleSide.Ally, allyStrategist, mAllyPartyDefinition);
            if (mIsAutoBattle)
            {
                StartAllyAutoBattle();
            }

            mTickCoroutine = StartCoroutine(AdvanceTick());
        }

        // コマンド選択・オートバトルの各ボタン押下を監視し、味方の操作モードを切り替える
        void Update()
        {
            if (IsCommandInputPressed())
            {
                // 手動モードへ切り替える。オートバトル中なら思考を止めてから入力を開始する
                StopAllyAutoBattle();
                if (CanSelectAnyCommand())
                {
                    mController.BeginCommandInput();
                }
            }

            if (IsAutoBattlePressed())
            {
                // オートモードへ切り替える。開いている入力UIは強制的に閉じる
                mController.Abort();
                StartAllyAutoBattle();
            }

            if (IsCancelReservationPressed())
            {
                // このティックの予約をやり直せるよう、積んだ行動を全て取り消す
                mController.Abort();
                mActionCollector.CancelAll();
            }
        }

        // パーティ定義に対応する思考ルーチンを生成する
        // 判断の中身はユニット側の AI プロファイル（判断ツリー）が決めるため、ここでは生成するだけ
        // aDefinition : 生成元のパーティ定義
        // return : 生成された思考ルーチン
        private IPPPartyCommandStrategist CreateStrategist(PPPartyDefinition aDefinition)
            => new PPUnitAIStrategist();

        // パーティ定義に対応する思考ドライバを生成する
        // 思考間隔はティック間隔を思考回数で割って決まるため、ドライバへはその 2 つをそのまま渡す
        // aSide : 思考対象の陣営
        // aStrategist : 駆動する思考ルーチン
        // aDefinition : 思考回数を引くパーティ定義
        // return : 生成されたドライバ
        private PPEnemyAIDriver CreateDriver(BattleSide aSide, IPPPartyCommandStrategist aStrategist,
            PPPartyDefinition aDefinition)
            => new PPEnemyAIDriver(mBattleManager, aSide, aStrategist, mTurnTickInterval, aDefinition.ThinkCountPerTick);

        // 今コマンド入力を始める意味があるかを判定する
        // 味方の中に発動可能なスキルを持つユニットが 1 体でも居れば true
        // ゲージ不足で何も撃てない状態のときに入力画面が開くのを防ぐ
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
        // 敵にはプッシャーが無いため、ティックごとにランダム量のゲージを直接補給して
        // プレイヤー側のコイン収入と釣り合いを取っている
        // ゲージ補給 → 行動の収集と実行 → ターン経過、の順に進める
        // return : コルーチンの列挙子
        IEnumerator AdvanceTick()
        {
            while (true)
            {
                if(mBattleManager == null) yield break;
                if(mBattleManager.StateMachine.Current == BattleState.BattleEnd) yield break;
                yield return new WaitForSeconds(mTurnTickInterval);

                var enemyParty = (PPBattleParty)mBattleManager.Context.GetParty(BattleSide.Enemy);
                mEnemyResourceSimulation.Supply(enemyParty, mBattleManager.Context);

                // シミュレーション用。実機のコインプッシャーが無い環境で味方のゲージ収入を代替する
                if (mIsSimulateAllyResource)
                {
                    var allyParty = (PPBattleParty)mBattleManager.Context.GetParty(BattleSide.Ally);
                    mAllyResourceSimulation.Supply(allyParty, mBattleManager.Context);
                }

                ExecuteTickActions();
                mBattleManager.AdvanceTick();
            }
        }

        // このティック分の行動を集めて並べ替え、順に実行する
        //
        // 収集するのは「プレイヤーの予約」「指示が無いユニットの通常攻撃」「敵 AI の計画」の 3 つ
        // 決まった順ではなく、優先度（先攻・通常・後攻）と速度で並べ直してから流すため、
        // 予約した順序や AI が思考した順序は実行順に影響しない
        private void ExecuteTickActions()
        {
            var context = mBattleManager.Context;

            // 味方は予約が無いユニットを通常攻撃で埋める
            // オートバトル中は味方 AI が計画を立てているため、そちらを優先して既定行動は積まない
            var allyPlan = mAllyActionCoroutine != null ? mAllyAIDriver?.LatestPlan : null;
            if (allyPlan == null)
            {
                mActionCollector.FillDefaultAttacks(
                    (PPBattleParty)context.GetParty(BattleSide.Ally), context);
            }

            var actions = mActionCollector.CollectOrdered(context, mEnemyAIDriver?.LatestPlan, allyPlan);
            foreach (var action in actions)
            {
                mBattleManager.EnqueueCommand(action.Command);
            }
            mBattleManager.ExecuteAllCommands();

            // 流し終えた計画と予約は破棄する。次のティックは改めて集め直す
            mActionCollector.Clear();
            mEnemyAIDriver?.ConsumePlan();
            mAllyAIDriver?.ConsumePlan();
        }

        // 味方AIの思考コルーチンを開始する。既に動いている場合は何もしない
        private void StartAllyAutoBattle()
        {
            if (mAllyActionCoroutine != null) return;
            mAllyActionCoroutine = StartCoroutine(mAllyAIDriver.RunLoop());
        }

        // 味方AIの思考コルーチンを停止する。動いていない場合は何もしない
        private void StopAllyAutoBattle()
        {
            StopCoroutineIfRunning(ref mAllyActionCoroutine);
        }

        // 参照が示すコルーチンが動いていれば停止し、参照を null に戻す
        // aCoroutine : 停止対象のコルーチン参照
        private void StopCoroutineIfRunning(ref Coroutine aCoroutine)
        {
            if (aCoroutine != null)
            {
                StopCoroutine(aCoroutine);
                aCoroutine = null;
            }
        }

        // プレイヤーが確定したコマンドを、このティックの行動として予約する
        // 実行はティックの終わりにまとめて行うため、ここでは積むだけに留める
        // aUnit : 行動するユニット
        // aCommand : 確定したコマンド
        private void HandleCommandConfirmed(BattleUnit aUnit, BattleCommandBase aCommand)
        {
            if (aUnit is not PPBattleUnit unit) return;

            mActionCollector.TryReserve(unit, aCommand);
        }

        // コマンド入力の開始操作が行われたかを判定する
        // return : キーボードの C、またはゲームパッドの△が押された場合 true
        bool IsCommandInputPressed()
        {
            return
                (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.triangleButton.wasPressedThisFrame);
        }

        // 予約の取り消し操作が行われたかを判定する
        // return : キーボードの X、またはゲームパッドの×が押された場合 true
        bool IsCancelReservationPressed()
        {
            return
                (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.crossButton.wasPressedThisFrame);
        }

        // オートバトルへの切り替え操作が行われたかを判定する
        // return : キーボードの T、またはゲームパッドの右肩ボタン(R1)が押された場合 true
        bool IsAutoBattlePressed()
        {
            return
                (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.rightShoulder.wasPressedThisFrame);
        }
    }
}
