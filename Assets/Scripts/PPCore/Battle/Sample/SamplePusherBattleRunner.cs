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

/// <summary>
/// プッシャーとバトルを繋いだ一連のフローを組み立てるエントリポイント。
/// <para>
/// バトルの組み立て方（パーティ生成 → Rules 設定 → Strategist 注入 →
/// View / 入力 / コイン橋渡しの Bind → AI ドライバとティックのコルーチン起動）を
/// 一通り示す参照実装で、新しくバトルを立ち上げる際はこの流れをなぞる。
/// </para>
/// <para>
/// 本作のバトルはターン制ではなく、プッシャーと並行してリアルタイムに進む。
/// 一定間隔のティックでターンを進めつつ、プレイヤーは任意のタイミングで
/// コマンド入力を開始できる（入力中は timeScale が 0 になり盤面が止まる）。
/// </para>
/// </summary>
public class SamplePusherBattleRunner : MonoBehaviour
{
    /// <summary>プッシャーのコイン獲得をリソースへ橋渡しするブリッジ。</summary>
    [Header("コイン")]
    [Label("コインリソースブリッジ")]
    [SerializeField] private PPCoinResourceBridge mCoinResourceBridge;
    /// <summary>属性ごとのリソース上限。</summary>
    [Label("コイン上限")]
    [SerializeField] private int mMaxCoin = 100;
    /// <summary>コイン 1 枚あたりのリソース変換係数の初期値。</summary>
    [Label("コイン獲得レート初期値")]
    [SerializeField] private float mBaseCoinConversionRate = 1f;

    /// <summary>ユニットとビューを対応付けるバインダー。</summary>
    [Header("UI")]
    [Label("バトルビューバインダー")]
    [SerializeField] private PPBattleUnitViewBinder mBattleUnitViewBinder;

    /// <summary>プレイヤーのコマンド入力を扱うコントローラー。</summary>
    [Header("インプット")]
    [Label("コマンド入力コントローラ")]
    [SerializeField] private PPBattleCommandInputController mController;

    /// <summary>ターン経過の間隔（秒）。</summary>
    [Header("バトル設定")]
    [Label("ターン更新間隔")]
    [SerializeField] private float mTurnTickInterval = 5f;

    /// <summary>味方として生成するユニット定義。</summary>
    [Header("味方")]
    [Label("ユニット")]
    [SerializeField] private UnitDefinition mAllyUnit;

    /// <summary>敵として生成するユニット定義。</summary>
    [Header("敵")]
    [Label("ユニット")]
    [SerializeField] private UnitDefinition mEnemyUnit;
    /// <summary>敵 AI の性格プロファイル。未設定でも既定値で動作する。</summary>
    [Label("敵AIプロファイル")]
    [SerializeField] private PPPartyAIProfileDefinition mEnemyAIProfile;
    /// <summary>プロファイル未設定時に使う思考間隔（秒）。</summary>
    [Label("デフォルト思考間隔")][EditCondition("HasEnemyAIProfile", true, true)]
    [SerializeField] private float mDefaultEnemyThinkDuration = 0.5f;
    /// <summary>敵 AI を駆動するドライバ。</summary>
    private PPEnemyAIDriver mEnemyAIDriver;
    /// <summary>敵がティックごとに得るリソース量の範囲（最小, 最大）。</summary>
    [Label("コイン取得")]
    [SerializeField] private Vector2Int mEnemyResourcePerTick = new Vector2Int(5, 10);

    /// <summary>敵 AI プロファイルが設定されているか。思考間隔の入力欄の出し分けに使う。</summary>
    private bool HasEnemyAIProfile => mEnemyAIProfile != null;

    /// <summary>バトル進行を統括するマネージャ。</summary>
    private BattleManager mBattleManager = new();

    /// <summary>敵 AI のループを回しているコルーチン。</summary>
    private Coroutine mEnemyActionCoroutine;
    /// <summary>ターン経過を回しているコルーチン。</summary>
    private Coroutine mTickCoroutine;

    /// <summary>
    /// バトルを組み立てて開始する。
    /// ユニット生成 → コンテキスト構築 → 敵 AI の注入 → ログ用イベント購読 → バトル開始 →
    /// View・コインブリッジ・入力の接続 → AI とティックのコルーチン起動、の順に進む。
    /// </summary>
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

    /// <summary>
    /// コマンド入力キーの押下を監視し、行動可能なユニットが居る場合のみ入力を開始する。
    /// </summary>
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

    /// <summary>
    /// 今コマンド入力を始める意味があるかを判定する。
    /// 味方の中に発動可能なスキルを持つユニットが 1 体でも居れば true。
    /// リソース不足で何も撃てない状態のときに入力画面が開くのを防ぐ。
    /// </summary>
    /// <returns>入力を開始できる場合 true。</returns>
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

    /// <summary>
    /// 一定間隔でターンを進めるコルーチン。
    /// 敵にはプッシャーが無いため、ティックごとにランダム量のリソースを直接補給して
    /// プレイヤー側のコイン収入と釣り合いを取っている。
    /// </summary>
    /// <returns>コルーチンの列挙子。</returns>
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

    /// <summary>
    /// プレイヤーのコマンドがキューへ流された直後に、それを即座に実行する。
    /// </summary>
    private void HandleCommandFlushed()
    {
        mBattleManager.ExecuteNextCommand();
    }

    /// <summary>
    /// コマンド入力の開始操作が行われたかを判定する。
    /// </summary>
    /// <returns>キーボードの C、またはゲームパッドの△が押された場合 true。</returns>
    bool IsCommandInputPressed()
    {
        return
            (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.triangleButton.wasPressedThisFrame);
    }
}
