/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleRunner.cs
 * @author hqrse
 * @date 2026/08/02
 * @brief バトルの実行ランナーとリソース供給シミュレーション
 * =====================================*/

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using AttributeUtility;

namespace PPCore
{
    // プッシャーとバトルを繋いだ一連のフローを組み立てるエントリポイント（本番用）
    // パーティ生成は PPPartyFactory を経由し、行動の進行は PPActionScheduler へ任せる
    public class PPBattleRunner : MonoBehaviour
    {
        /* インスペクタ設定 */

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

        // ゲージ供給(シミュレーション)の 1 回分の間隔（秒）
        [Header("バトル設定")]
        [Label("ゲージ供給の間隔(秒)")]
        [SerializeField] private float mTurnTickInterval = 5f;

        // 行動してから次の行動を積めるようになるまでの秒数。演出の長さの代わりに使う
        [Label("行動の演出時間")]
        [Min(0f)]
        [SerializeField] private float mActionPresentationSeconds = 0.5f;

        // 有効なとき、味方が行動を積むタイミングで発動可能なスキルがあれば、スキル選択を開いて選び直せるようにする
        [Label("スキル使用可能時にゲージが溜まったらコマンド選択を開く")]
        [SerializeField] private bool mIsOpenCommandOnGaugeReady = false;

        // 敵が供給の間隔ごとに得るゲージ量。敵のプッシャーができるまでの代わりに直接補給する
        [Label("敵ゲージ供給")]
        [SerializeField] private PPResourceSimulation mEnemyResourceSimulation = new ();

        // 味方として編成するパーティ定義。デバッグ用にエディタから直接割り当てる
        [Header("パーティ")]
        [Label("味方パーティ定義")]
        [SerializeField] private PPPartyDefinition mAllyPartyDefinition;

        // 敵として編成するパーティ定義
        [Label("敵パーティ定義")]
        [SerializeField] private PPPartyDefinition mEnemyPartyDefinition;

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

        /* Unity イベント */

        // バトルを組み立てて開始する。パーティ生成 → コンテキスト構築 → バトル開始 →
        // View・コインブリッジ・入力の接続 → 行動の進行とゲージ供給の開始、の順に進む
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

            mBattleManager.OnBattleEnded += r => mActionScheduler.Unbind();
            PPBattleLogBinder.Bind(mBattleManager, context);
            mBattleManager.StartBattle(context);
            mBattleUnitViewBinder.Bind(mBattleManager);

            mController.Bind(mBattleManager);
            mController.OnCommandConfirmed += HandleCommandConfirmed;
            mController.OnInputClosed += mActionScheduler.ResumeAfterSelection;

            mActionScheduler.SelectionRequester = HandleSelectionRequested;
            mActionScheduler.Bind(mBattleManager, mActionPresentationSeconds);

            mEnemyResourceSimulation.Start((PPBattleParty)context.EnemyParty, context, mTurnTickInterval);
            // シミュレーション供給時はプッシャーのコイン獲得と二重に加算されないよう購読を行わない
            if (mIsSimulateAllyResource)
            {
                mAllyResourceSimulation.Start((PPBattleParty)context.AllyParty, context, mTurnTickInterval);
            }
            else
            {
                mCoinResourceBridge.Bind(mBattleManager, BattleSide.Ally);
            }
        }

        // バトル専用の時計を進め、コマンド選択ボタンの押下でコマンド入力を開始する
        void Update()
        {
            mBattleManager.AdvanceTime(Time.deltaTime);

            if (IsCommandInputPressed() && CanSelectAnyCommand())
            {
                mController.BeginCommandInput();
            }
        }

        // バトルが決着しないまま破棄された場合にも、進行役の購読を外す
        void OnDestroy() => mActionScheduler.Unbind();

        /* 問い合わせ */

        // 今コマンド入力を始められるかを判定する。ターゲットの指定はいつでも行えるため、決着していなければ true
        private bool CanSelectAnyCommand()
            => mBattleManager.Context != null && mBattleManager.StateMachine.Current != BattleState.BattleEnd;

        // コマンド入力の開始操作（キーボードの C、またはゲームパッドの△）が行われたかを判定する
        bool IsCommandInputPressed()
        {
            return
                (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.triangleButton.wasPressedThisFrame);
        }

        /* イベントハンドラ */

        // プレイヤーが確定したコマンドを、そのユニットのスキルの選択へ反映する
        // その場では実行せず、コインゲージが溜まったときに進行役が積む
        private void HandleCommandConfirmed(BattleUnit aUnit, BattleCommandBase aCommand)
            => mActionScheduler.ApplyPlayerCommand(aUnit, aCommand, mController.Context.Target);

        // 行動を積む直前のコマンド選択を開くかを決める。設定が有効で、味方に発動可能なスキルがあるときだけ開く
        private bool HandleSelectionRequested(PPBattleUnit aUnit)
        {
            if (!mIsOpenCommandOnGaugeReady || aUnit.Side != BattleSide.Ally) return false;
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
