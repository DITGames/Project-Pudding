/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleRunner.cs
 * @author hqrse
 * @date 2026/08/02
 * @brief バトルのフロー制御(本番用)
 * =====================================*/

using System.Collections;
using BattleInput;
using CommandBattleCore;
using CustomConsole;
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
        // プッシャーのコイン獲得をリソースへ橋渡しするブリッジ
        [Header("コイン")]
        [Label("コインリソースブリッジ")]
        [SerializeField] private PPCoinResourceBridge mCoinResourceBridge;

        // ユニットとビューを対応付けるバインダー
        [Header("UI")]
        [Label("バトルビューバインダー")]
        [SerializeField] private PPBattleUnitViewBinder mBattleUnitViewBinder;

        // プレイヤーのコマンド入力を扱うコントローラー
        [Header("インプット")]
        [Label("コマンド入力コントローラ")]
        [SerializeField] private PPBattleCommandInputController mController;
        // 新しい入力システムの試作。PPBattleCommandInputController とは独立した別系統として並行稼働させる
        // 未アサインでも既存の入力に影響しないよう、Start 側で null チェックしてから Bind する
        [Label("コマンド入力(新規試作)")]
        [SerializeField] private BattleCommandInput mNewCommandInput;

        // ターン経過の間隔（秒）
        [Header("バトル設定")]
        [Label("ターン更新間隔")]
        [SerializeField] private float mTurnTickInterval = 5f;
        // パーティにAIプロファイルが設定されていない場合に使う、1ティックあたりの思考回数
        [Label("デフォルト思考回数(1ティックあたり)")]
        [SerializeField] private int mDefaultThinkCountPerTick = 1;
        // 敵がティックごとに得るリソース量の範囲（最小, 最大）。敵にはプッシャーが無いため直接補給する
        [Label("敵コイン取得")]
        [SerializeField] private PPResourceSimulation mEnemyResourceSimulation;

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
        // trueの場合、プッシャーのコイン獲得の代わりにティックごとのリソース加算で味方を進行させる
        // 実機のコインプッシャーが無いシミュレーション環境向け
        [Label("味方リソースをシミュレーション供給")]
        [SerializeField] private bool mIsSimulateAllyResource = false;
        // 味方がティックごとに得るリソース量の範囲（最小, 最大）。mIsSimulateAllyResourceがtrueのときのみ使用
        [Label("味方コイン取得(シミュレーション)")]
        [EditCondition(nameof(mIsSimulateAllyResource), true, false)]
        [SerializeField] private PPResourceSimulation mAllyResourceSimulation = new ();

        // バトル進行を統括するマネージャ
        private BattleManager mBattleManager = new();

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
            // リソース消費を検証するバリデータへ差し替える
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
            mController.OnCommandFlushed += HandleCommandFlushed;

            // 新しい入力システムの試作。既存コントローラーとは別に同じ BattleManager を直接 Bind する
            if (mNewCommandInput != null)
            {
                mNewCommandInput.Bind(mBattleManager);
            }

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
        }

        // パーティ定義に対応する思考ルーチンを生成する
        // プロファイル未設定のパーティは戦術を 1 つも持たないため常に待機になる
        // 味方はオートバトルに切り替えて初めて動き出すので、設定漏れに気付けるよう生成時に警告を出す
        // aDefinition : 生成元のパーティ定義
        // return : 生成された思考ルーチン
        private IPPPartyCommandStrategist CreateStrategist(PPPartyDefinition aDefinition)
        {
            if (aDefinition.AIProfile == null)
            {
                CustomConsoleLog.Warning("AI", $"{aDefinition.name} にAIプロファイルが未設定のため、このパーティは常に待機します。");
            }
            return new PPPartyTacticsStrategist(aDefinition.AIProfile);
        }

        // パーティ定義に対応する思考ドライバを生成する
        // 思考間隔はティック間隔を思考回数で割って決まるため、ドライバへはその 2 つをそのまま渡す
        // aSide : 思考対象の陣営
        // aStrategist : 駆動する思考ルーチン
        // aDefinition : 思考回数を引くパーティ定義
        // return : 生成されたドライバ
        private PPEnemyAIDriver CreateDriver(BattleSide aSide, IPPPartyCommandStrategist aStrategist,
            PPPartyDefinition aDefinition)
        {
            int thinkCount = aDefinition.AIProfile != null
                ? aDefinition.AIProfile.ThinkCountPerTick
                : mDefaultThinkCountPerTick;
            return new PPEnemyAIDriver(mBattleManager, aSide, aStrategist, mTurnTickInterval, thinkCount);
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
                    foreach (var entry in mEnemyResourceSimulation.mEntries)
                    {
                        enemyParty.ResourcePool.Add(entry.mType, mBattleManager.Context.Rules.RandomProvider.NextInt(entry.mAmount.x, entry.mAmount.y));
                    }
                }

                // シミュレーション用。実機のコインプッシャーが無い環境で味方のリソース収入を代替する
                if (mIsSimulateAllyResource)
                {
                    var allyParty = (PPBattleParty)mBattleManager.Context.GetParty(BattleSide.Ally);
                    if (allyParty != null)
                    {
                        foreach (var entry in mAllyResourceSimulation.mEntries)
                        {
                            allyParty.ResourcePool.Add(entry.mType, mBattleManager.Context.Rules.RandomProvider.NextInt(entry.mAmount.x, entry.mAmount.y));
                        }
                    }
                }

                mBattleManager.AdvanceTick();
            }
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
