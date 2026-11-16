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
using CustomConsole;

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

        // ゲージ供給(シミュレーション)の 1 回分と、状態異常・クールダウンの更新の間隔（秒）
        [Header("バトル設定")]
        [Label("更新間隔(ゲージ供給・状態異常)")]
        [SerializeField] private float mTurnTickInterval = 5f;

        // 行動してから次の行動を積めるようになるまでの秒数。演出の長さの代わりに使う
        [Label("行動の演出時間")]
        [Min(0f)]
        [SerializeField] private float mActionPresentationSeconds = 0.5f;

        // 敵が更新間隔ごとに得るゲージ量。敵のプッシャーができるまでの代わりに直接補給する
        [Label("敵ゲージ供給")]
        [SerializeField] private PPResourceSimulation mEnemyResourceSimulation = new ();

        // 味方として編成するパーティ定義。デバッグ用にエディタから直接割り当てる
        [Header("パーティ")]
        [Label("味方パーティ定義")]
        [SerializeField] private PPPartyDefinition mAllyPartyDefinition;

        // 敵として編成するパーティ定義
        [Label("敵パーティ定義")]
        [SerializeField] private PPPartyDefinition mEnemyPartyDefinition;

        // trueの場合、プッシャーのコイン獲得の代わりに更新間隔ごとのゲージ加算で味方を進行させる
        // 実機のコインプッシャーが無いシミュレーション環境向け
        [Header("デバッグ")]
        [Label("味方ゲージをシミュレーション供給")]
        [SerializeField] private bool mIsSimulateAllyResource = false;

        // 味方が更新間隔ごとに得るゲージ量。mIsSimulateAllyResourceがtrueのときのみ使用
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

            mActionScheduler.Bind(mBattleManager, mActionPresentationSeconds);
            mActionScheduler.StartStatusTick(mTurnTickInterval);

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

        // 今コマンド入力を始める意味があるかを判定する
        // 味方の中に発動可能なスキルを持つユニットが 1 体でも居れば true
        private bool CanSelectAnyCommand()
        {
            if (mBattleManager.Context == null || mBattleManager.StateMachine.Current == BattleState.BattleEnd)
                return false;

            var allyParty = mBattleManager.Context.GetParty(BattleSide.Ally);
            foreach (var unit in allyParty.ActiveMembers)
            {
                if (unit is PPBattleUnit ppUnit && ppUnit.CanValidateSkill(mBattleManager.Context))
                    return true;
            }
            return false;
        }

        // コマンド入力の開始操作（キーボードの C、またはゲームパッドの△）が行われたかを判定する
        bool IsCommandInputPressed()
        {
            return
                (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.triangleButton.wasPressedThisFrame);
        }

        /* イベントハンドラ */

        // プレイヤーが確定したコマンドを受け取る。現在の進行は通常攻撃だけを行うため、確定した内容は使わない
        private void HandleCommandConfirmed(BattleUnit aUnit, BattleCommandBase aCommand)
        {
            CustomConsoleLog.Warning("Battle",
                $"{aUnit?.DisplayName} のコマンド選択は、スキルの選択が未実装のため反映されません。");
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
