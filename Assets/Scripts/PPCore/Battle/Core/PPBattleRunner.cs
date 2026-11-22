/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleRunner.cs
 * @author hqrse
 * @date 2026/08/02
 * @brief バトルの実行ランナーとリソース供給シミュレーション
 * =====================================*/

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using AttributeUtility;
using CustomConsole;

namespace PPCore
{
    // プッシャーとバトルを繋いだ一連のフローを組み立てるエントリポイント（本番用）
    // パーティ生成は PPPartyFactory を経由し、行動の進行は PPActionScheduler、敵とオートバトルの判断は PPUnitAIStrategist へ任せる
    public class PPBattleRunner : MonoBehaviour
    {
        /* インスペクタ設定 */

        // プッシャーのコイン獲得をゲージへ橋渡しするブリッジ
        [Header("コイン")]
        [Label("コインゲージブリッジ")]
        [SerializeField] private PPCoinResourceBridge mCoinResourceBridge;

        // チャージ用アイテムを味方のスキルゲージへ橋渡しするブリッジ。未設定ならアイテムを扱わない
        [Label("チャージアイテムブリッジ")]
        [SerializeField] private PPChargeItemBridge mChargeItemBridge;

        // 敵のプッシャーを物理で動かすか。無効なら、下の「敵ゲージ供給」で設定した量で代用する
        [Header("敵のプッシャー")]
        [Label("敵のプッシャーを使う")]
        [SerializeField] private bool mIsUseEnemyPusher = false;
        // 敵のプッシャー一式（本体・カメラ）の親。使わない設定のときは無効にする
        [Label("敵のプッシャー本体")]
        [SerializeField] private GameObject mEnemyPusherRoot;
        // 敵のプッシャーを映すワイプ表示。使わない設定のときは無効にする
        [Label("敵のプッシャーのワイプ表示")]
        [SerializeField] private GameObject mEnemyPusherWipe;
        [Label("敵コインゲージブリッジ")]
        [SerializeField] private PPCoinResourceBridge mEnemyCoinResourceBridge;
        [Label("敵チャージアイテムブリッジ")]
        [SerializeField] private PPChargeItemBridge mEnemyChargeItemBridge;
        // ロード画面を出してプッシャーを準備する役。設定するとバトルの開始を読み込みの後まで待つ。未設定なら待たない
        [Label("プッシャーの読み込み")]
        [SerializeField] private PPPusherLoader mPusherLoader;

        // ユニットとビューを対応付けるバインダー
        [Header("UI")]
        [Label("バトルビューバインダー")]
        [SerializeField] private PPStageUnitViewBinder mBattleUnitViewBinder;

        // プレイヤーのコマンド入力を扱うコントローラー
        [Header("インプット")]
        [Label("コマンド入力コントローラ")]
        [SerializeField] private PPBattleCommandInputController mController;
        [Label("コマンドを開く入力")]
        [SerializeField] private InputActionReference mOpenCommandAction;
        [Label("オートバトルを切り替える入力")]
        [SerializeField] private InputActionReference mAutoBattleAction;
        // キー設定を開閉する入力。キー設定では変更できない
        [Label("キー設定を開く入力")]
        [SerializeField] private InputActionReference mOpenConfigAction;
        // 未設定ならキー設定は開けない
        [Label("キー設定")]
        [SerializeField] private PPInputConfigMenuView mConfigMenu;

        // ゲージ供給(シミュレーション)の 1 回分の間隔（秒）
        [Header("バトル設定")]
        [Label("ゲージ供給の間隔(秒)")]
        [FormerlySerializedAs("mTurnTickInterval")]
        [SerializeField] private float mSupplyInterval = 5f;

        // 行動してから次の行動を積めるようになるまでの秒数。演出の長さの代わりに使う
        [Label("行動の演出時間")]
        [Min(0f)]
        [SerializeField] private float mActionPresentationSeconds = 0.5f;

        // 有効なとき、味方が行動を積むタイミングで発動可能なスキルがあれば、スキル選択を開いて選び直せるようにする
        [Label("スキル使用可能時にゲージが溜まったらコマンド選択を開く")]
        [SerializeField] private bool mIsOpenCommandOnGaugeReady = false;

        // 有効なとき、味方も AI が次の行動を決める。バトル中は入力で切り替えられる(既定は T キー)
        [Label("オートバトル")]
        [SerializeField] private bool mIsAutoBattle = false;

        // 敵が供給の間隔ごとに得るゲージ量。敵のプッシャーを使わない設定のときに、代わりに直接補給する
        [Label("敵ゲージ供給")]
        [SerializeField] private PPResourceSimulation mEnemyResourceSimulation = new ();

        // 味方として編成するパーティ定義。デバッグ用にエディタから直接割り当てる
        [Header("パーティ")]
        [Label("味方パーティ定義")]
        [SerializeField] private PPPartyDefinition mAllyPartyDefinition;

        // 敵として編成するパーティ定義
        [Label("敵パーティ定義")]
        [SerializeField] private PPPartyDefinition mEnemyPartyDefinition;

        // ステージのマスから来たとき、決着してからマップへ戻るまでの秒数。単体で Play したときは戻らない
        [Header("ステージ")]
        [Label("決着後にマップへ戻るまでの秒数")]
        [Min(0f)]
        [SerializeField] private float mReturnToStageDelay = 2f;

        // trueの場合、プッシャーのコイン獲得の代わりに供給の間隔ごとのゲージ加算で味方を進行させる
        // 実機のコインプッシャーが無いシミュレーション環境向け
        [Header("デバッグ")]
        [Label("味方ゲージをシミュレーション供給")]
        [SerializeField] private bool mIsSimulateAllyResource = false;

        // 味方が供給の間隔ごとに得るゲージ量。mIsSimulateAllyResourceがtrueのときのみ使用
        [Label("味方ゲージ供給(シミュレーション)")]
        [EditCondition(nameof(mIsSimulateAllyResource), true, false)]
        [SerializeField] private PPResourceSimulation mAllyResourceSimulation = new ();

        /* 状態 */

        // バトル進行を統括するマネージャ
        private BattleManager mBattleManager = new();
        // コインゲージが溜まったユニットを行動させる進行役
        private readonly PPActionScheduler mActionScheduler = new();
        // 敵の次の行動を決める AI
        private readonly PPUnitAIStrategist mEnemyAI = new();
        // オートバトル中に味方の次の行動を決める AI
        private readonly PPUnitAIStrategist mAllyAI = new();

        /* Unity イベント */

        // 敵のプッシャーを使わない設定なら、物理が動き出す前に一式を無効にする
        void Awake()
        {
            if (mEnemyPusherRoot != null) mEnemyPusherRoot.SetActive(IsEnemyPusherAvailable);
            if (mEnemyPusherWipe != null) mEnemyPusherWipe.SetActive(IsEnemyPusherAvailable);
        }

        // プッシャーの読み込みを待ってからバトルを組み立てて開始し、最後にプッシャーを動かし始める
        IEnumerator Start()
        {
            if (mPusherLoader != null) yield return mPusherLoader.Load();
            StartBattle();
            if (mPusherLoader != null) mPusherLoader.Begin();
        }

        // バトルを組み立てて開始する。パーティ生成 → コンテキスト構築 → バトル開始 →
        // View・コインブリッジ・入力の接続 → AI の接続と最初の思考 → 行動の進行とゲージ供給の開始、の順に進む
        private void StartBattle()
        {
            mBattleManager = new BattleManager { TimeProvider = () => Time.time };
            foreach (var action in new[] { mOpenCommandAction, mAutoBattleAction, mOpenConfigAction })
            {
                if (action != null) action.action.Enable();
            }

            // ステージのマスから来たときは持ち越した編成とマスの敵で、単体で Play したときはインスペクタの定義で戦う
            var stage = PPStageProgress.Current;
            var stageBattle = stage?.PendingBattle;
            var context = new BattleContext()
            {
                AllyParty = stageBattle != null
                    ? stage.CreateAllyParty()
                    : PPPartyFactory.CreateFromDefinition(mAllyPartyDefinition, BattleSide.Ally),
                EnemyParty = PPPartyFactory.CreateFromDefinition(stageBattle != null ? stageBattle.Enemy : mEnemyPartyDefinition, BattleSide.Enemy),
                Rules = new PPBattleRules(),
            };
            // スキルゲージ残量を検証するバリデータへ差し替える
            context.Rules.CastValidator = new PPBattleCastValidator();

            mBattleManager.OnBattleEnded += r => UnbindBattle();
            if (stageBattle != null) mBattleManager.OnBattleEnded += r => StartCoroutine(ReturnToStage(stage, r));
            PPBattleLogBinder.Bind(mBattleManager, context);
            mBattleManager.StartBattle(context);
            mBattleUnitViewBinder.Bind(mBattleManager);

            mController.Bind(mBattleManager);
            mController.OnCommandConfirmed += HandleCommandConfirmed;
            mController.OnInputClosed += mActionScheduler.ResumeAfterSelection;

            mActionScheduler.SelectionRequester = HandleSelectionRequested;
            mActionScheduler.OnActionFinished += HandleActionFinished;
            mActionScheduler.Bind(mBattleManager, mActionPresentationSeconds);

            // AI は行動が終わるたびに次の行動を決める。最初の 1 回だけは開始時にまとめて決めておく
            mEnemyAI.BindBattle(mBattleManager, BattleSide.Enemy);
            mAllyAI.BindBattle(mBattleManager, BattleSide.Ally);
            mEnemyAI.ThinkAll(context);
            if (mIsAutoBattle) mAllyAI.ThinkAll(context);

            // 敵のコインは、プッシャーを使う設定ならプッシャーから、そうでなければ設定した量の供給から得る
            if (IsEnemyPusherAvailable)
            {
                mEnemyCoinResourceBridge.Bind(mBattleManager, BattleSide.Enemy);
                if (mEnemyChargeItemBridge != null) mEnemyChargeItemBridge.Bind(mBattleManager, BattleSide.Enemy);
            }
            else
            {
                mEnemyResourceSimulation.Start((PPBattleParty)context.EnemyParty, context, mSupplyInterval);
            }
            // シミュレーション供給時はプッシャーのコイン獲得と二重に加算されないよう購読を行わない
            if (mIsSimulateAllyResource)
            {
                mAllyResourceSimulation.Start((PPBattleParty)context.AllyParty, context, mSupplyInterval);
            }
            else
            {
                mCoinResourceBridge.Bind(mBattleManager, BattleSide.Ally);
                if (mChargeItemBridge != null) mChargeItemBridge.Bind(mBattleManager, BattleSide.Ally);
            }
        }

        // バトル専用の時計を進め、入力に応じてキー設定の開閉・オートバトルの切り替え・コマンド入力の開始を行う
        // キー設定を開いている間は、ほかの入力を受け付けない
        void Update()
        {
            // プッシャーの読み込み中は、まだバトルを組み立てていない
            if (mBattleManager.Context == null) return;

            mBattleManager.AdvanceTime(Time.deltaTime);

            if (mConfigMenu != null && WasPressed(mOpenConfigAction) && !mController.IsInputOpen && CanSelectAnyCommand())
            {
                mConfigMenu.Toggle(mBattleManager);
            }
            if (mConfigMenu != null && mConfigMenu.IsOpen) return;

            if (IsAutoBattleTogglePressed() && CanSelectAnyCommand())
            {
                SetAutoBattle(!mIsAutoBattle);
            }

            if (IsCommandInputPressed() && CanSelectAnyCommand())
            {
                mController.BeginCommandInput();
            }
        }

        // バトルが決着しないまま破棄された場合にも、進行役と AI の購読を外す
        void OnDestroy() => UnbindBattle();

        /* 操作 */

        // オートバトルを切り替える。有効にした時点で、味方全員の次の行動を AI に決めさせる
        // 無効にしても、AI が決めたスキルの選択と狙う相手はそのまま残る（コマンド入力で変えられる）
        public void SetAutoBattle(bool aIsEnabled)
        {
            mIsAutoBattle = aIsEnabled;
            CustomConsoleLog.Log("Battle", $"オートバトル: {(mIsAutoBattle ? "ON" : "OFF")}");
            if (mIsAutoBattle && CanSelectAnyCommand()) mAllyAI.ThinkAll(mBattleManager.Context);
        }

        // 決着をステージの進行状態へ反映し、少し待ってからマップのシーンへ戻る
        // 入力中に決着して timeScale が 0 のまま残っても待てるよう、待ち時間は timeScale に左右されない時間で数える
        private IEnumerator ReturnToStage(PPStageProgress aStage, BattleResult aResult)
        {
            string returnScene = aStage.ReturnSceneName;
            aStage.ResolveBattle(aResult.Type);
            CustomConsoleLog.Log("Battle", $"決着: {aResult.Type}。{mReturnToStageDelay} 秒後に {returnScene} へ戻る");

            yield return new WaitForSecondsRealtime(mReturnToStageDelay);
            // 次のバトルでプッシャーの物理が止まったままにならないよう、時間の流れを戻してから移る
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(returnScene);
        }

        // 進行役と AI をバトルから切り離す
        private void UnbindBattle()
        {
            mActionScheduler.Unbind();
            mEnemyAI.Unbind();
            mAllyAI.Unbind();
            // 決着後に落ちたコインやアイテムでゲージが動かないよう、橋渡しも外す
            if (mCoinResourceBridge != null) mCoinResourceBridge.Unbind();
            if (mChargeItemBridge != null) mChargeItemBridge.Unbind();
            if (mEnemyCoinResourceBridge != null) mEnemyCoinResourceBridge.Unbind();
            if (mEnemyChargeItemBridge != null) mEnemyChargeItemBridge.Unbind();
        }

        /* 問い合わせ */

        // 敵のプッシャーを使えるか。使う設定で、橋渡しが割り当てられているときだけ true
        private bool IsEnemyPusherAvailable => mIsUseEnemyPusher && mEnemyCoinResourceBridge != null;

        // 今コマンド入力を始められるかを判定する。ターゲットの指定はいつでも行えるため、決着していなければ true
        private bool CanSelectAnyCommand()
            => mBattleManager.Context != null && mBattleManager.StateMachine.Current != BattleState.BattleEnd;

        // コマンド入力の開始操作が行われたかを判定する(既定はキーボードの C、ゲームパッドの Y / △)
        private bool IsCommandInputPressed() => WasPressed(mOpenCommandAction);

        // オートバトルの切り替え操作が行われたかを判定する(既定はキーボードの T)
        private bool IsAutoBattleTogglePressed() => WasPressed(mAutoBattleAction);

        private static bool WasPressed(InputActionReference aAction) => aAction != null && aAction.action.WasPressedThisFrame();

        /* イベントハンドラ */

        // ユニットの行動が終わったとき、AI が担当するユニットなら次の行動を決めさせる
        private void HandleActionFinished(PPBattleUnit aUnit)
        {
            if (aUnit.Side == BattleSide.Enemy) mEnemyAI.Think(aUnit, mBattleManager.Context);
            else if (mIsAutoBattle) mAllyAI.Think(aUnit, mBattleManager.Context);
        }

        // プレイヤーが確定したコマンドを、そのユニットのスキルの選択へ反映する
        // その場では実行せず、コインゲージが溜まったときに進行役が積む
        private void HandleCommandConfirmed(BattleUnit aUnit, BattleCommandBase aCommand)
            => mActionScheduler.ApplyPlayerCommand(aUnit, aCommand, mController.Context.Target);

        // 行動を積む直前のコマンド選択を開くかを決める。設定が有効で、味方に発動可能なスキルがあるときだけ開く
        // オートバトル中は AI が決めるため開かない
        private bool HandleSelectionRequested(PPBattleUnit aUnit)
        {
            if (!mIsOpenCommandOnGaugeReady || mIsAutoBattle || aUnit.Side != BattleSide.Ally) return false;
            if (!aUnit.CanValidateSkill(mBattleManager.Context)) return false;

            return mController.BeginSkillSelect(aUnit);
        }
    }

    // プッシャーを持たない陣営へ、一定間隔ごとにゲージを供給するための設定
    // コインはコインブリッジと同じく 1 枚ずつ順番に配り、スキルゲージは生存ユニットへ均等分配する
    [Serializable]
    public class PPResourceSimulation
    {
        /* インスペクタ設定 */

        // 1 回の間隔で獲得するコイン枚数の範囲（最小, 最大）。ゲージ量はパーティのコイン変換係数を掛けて決まる
        [Label("コイン獲得枚数")]
        [SerializeField] private Vector2Int mCoinGaugeAmount = new Vector2Int(5, 10);
        // 1 回の間隔で供給するスキルゲージ量の範囲（最小, 最大）
        [Label("スキルゲージ供給量")]
        [SerializeField] private Vector2Int mSkillGaugeAmount = new Vector2Int(0, 0);

        /* 操作 */

        // 供給を開始する。以後は間隔ごとに自分で次の回を予約し、バトルが終わるとタイマーごと破棄される
        public void Start(PPBattleParty aParty, BattleContext aContext, float aInterval)
        {
            if (aParty == null || aContext == null) return;

            SupplyOnce(aParty, aContext, Mathf.Max(0.1f, aInterval));
        }

        // 1 回分の設定量を抽選して供給し、次の回を予約する。コインは間隔の中へ均等に散らして 1 枚ずつ配る
        // 乱数はシード管理・再現性のため aContext.Rules.RandomProvider を経由する
        private void SupplyOnce(PPBattleParty aParty, BattleContext aContext, float aInterval)
        {
            var random = aContext.Rules.RandomProvider;

            int coins = random.NextInt(mCoinGaugeAmount.x, mCoinGaugeAmount.y);
            for (int i = 0; i < coins; i++)
            {
                aContext.Timers.Schedule(aInterval * i / coins,
                    () => aParty.DistributeCoins(1, aParty.CoinConversionRate.CurrentValue));
            }

            PPGaugeUtility.DistributeToAliveUnits(aParty.ActiveMembers, PPGaugeKind.Skill,
                random.NextInt(mSkillGaugeAmount.x, mSkillGaugeAmount.y));

            aContext.Timers.Schedule(aInterval, () => SupplyOnce(aParty, aContext, aInterval));
        }
    }
}
