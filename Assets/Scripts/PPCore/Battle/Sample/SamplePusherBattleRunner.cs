/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file SamplePusherBattleRunner.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief バトルのフロー制御サンプル
 * =====================================*/

using PPCore;
using UnityEngine;
using UnityEngine.InputSystem;
using AttributeUtility;

// プッシャーとバトルを繋いだ一連のフローを組み立てるエントリポイント（参照実装）
// ユニット定義を 1 体ずつ直接指定して戦わせる。行動の進行は PPActionScheduler へ任せる
public class SamplePusherBattleRunner : MonoBehaviour
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

    // 敵ゲージ供給の 1 回分の間隔（秒）
    [Header("バトル設定")]
    [Label("ゲージ供給の間隔(秒)")]
    [SerializeField] private float mTurnTickInterval = 5f;

    // 行動してから次の行動を積めるようになるまでの秒数。演出の長さの代わりに使う
    [Label("行動の演出時間")]
    [Min(0f)]
    [SerializeField] private float mActionPresentationSeconds = 0.5f;

    // 味方として生成するユニット定義
    [Header("味方")]
    [Label("ユニット")]
    [SerializeField] private UnitDefinition mAllyUnit;

    // 敵として生成するユニット定義
    [Header("敵")]
    [Label("ユニット")]
    [SerializeField] private UnitDefinition mEnemyUnit;

    // 敵が供給の間隔ごとに得るゲージ量。敵のプッシャーができるまでの代わりに直接補給する
    [Label("敵ゲージ供給")]
    [SerializeField] private PPResourceSimulation mEnemyResourceSimulation = new ();

    /* 状態 */

    // バトル進行を統括するマネージャ
    private BattleManager mBattleManager = new();
    // コインゲージが溜まったユニットを行動させる進行役
    private readonly PPActionScheduler mActionScheduler = new();
    // 敵の次の行動を決める AI
    private readonly PPUnitAIStrategist mEnemyAI = new();

    /* Unity イベント */

    // バトルを組み立てて開始する。ユニット生成 → コンテキスト構築 → バトル開始 →
    // View・コインブリッジ・入力の接続 → 行動の進行とゲージ供給の開始、の順に進む
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

        mBattleManager.OnBattleEnded += r => UnbindBattle();
        PPBattleLogBinder.Bind(mBattleManager, context);
        mBattleManager.StartBattle(context);
        mBattleUnitViewBinder.Bind(mBattleManager);
        mCoinResourceBridge.Bind(mBattleManager, BattleSide.Ally);

        mController.Bind(mBattleManager);
        mController.OnCommandConfirmed += HandleCommandConfirmed;

        mActionScheduler.OnActionFinished += HandleActionFinished;
        mActionScheduler.Bind(mBattleManager, mActionPresentationSeconds);
        // 敵は行動が終わるたびに次の行動を決める。最初の 1 回だけは開始時に決めておく
        mEnemyAI.BindBattle(mBattleManager, BattleSide.Enemy);
        mEnemyAI.ThinkAll(context);
        mEnemyResourceSimulation.Start((PPBattleParty)context.EnemyParty, context, mTurnTickInterval);
    }

    // バトル専用の時計を進め、コマンド入力キーの押下でコマンド入力を開始する
    void Update()
    {
        mBattleManager.AdvanceTime(Time.deltaTime);

        if (IsCommandInputPressed() && CanSelectAnyCommand())
        {
            mController.BeginCommandInput();
        }
    }

    // バトルが決着しないまま破棄された場合にも、進行役と AI の購読を外す
    void OnDestroy() => UnbindBattle();

    /* 操作 */

    // 進行役と AI をバトルから切り離す
    private void UnbindBattle()
    {
        mActionScheduler.Unbind();
        mEnemyAI.Unbind();
    }

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

    // 敵の行動が終わったとき、次の行動を決めさせる
    private void HandleActionFinished(PPBattleUnit aUnit)
    {
        if (aUnit.Side == BattleSide.Enemy) mEnemyAI.Think(aUnit, mBattleManager.Context);
    }

    // プレイヤーが確定したコマンドを、そのユニットのスキルの選択へ反映する
    // その場では実行せず、コインゲージが溜まったときに進行役が積む
    private void HandleCommandConfirmed(BattleUnit aUnit, BattleCommandBase aCommand)
        => mActionScheduler.ApplyPlayerCommand(aUnit, aCommand, mController.Context.Target);
}
