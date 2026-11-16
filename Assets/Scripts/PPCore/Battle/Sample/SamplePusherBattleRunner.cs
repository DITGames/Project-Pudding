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
using CustomConsole;

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

    // 敵ゲージ供給の 1 回分と、状態異常・クールダウンの更新の間隔（秒）
    [Header("バトル設定")]
    [Label("更新間隔(ゲージ供給・状態異常)")]
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

    // 敵が更新間隔ごとに得るゲージ量。敵のプッシャーができるまでの代わりに直接補給する
    [Label("敵ゲージ供給")]
    [SerializeField] private PPResourceSimulation mEnemyResourceSimulation = new ();

    /* 状態 */

    // バトル進行を統括するマネージャ
    private BattleManager mBattleManager = new();
    // コインゲージが溜まったユニットを行動させる進行役
    private readonly PPActionScheduler mActionScheduler = new();

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

        mBattleManager.OnBattleEnded += r => mActionScheduler.Unbind();
        PPBattleLogBinder.Bind(mBattleManager, context);
        mBattleManager.StartBattle(context);
        mBattleUnitViewBinder.Bind(mBattleManager);
        mCoinResourceBridge.Bind(mBattleManager, BattleSide.Ally);

        mController.Bind(mBattleManager);
        mController.OnCommandConfirmed += HandleCommandConfirmed;

        mActionScheduler.Bind(mBattleManager, mActionPresentationSeconds);
        mActionScheduler.StartStatusTick(mTurnTickInterval);
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
