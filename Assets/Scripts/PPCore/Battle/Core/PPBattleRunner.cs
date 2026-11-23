/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleRunner.cs
 * @author hqrse
 * @date 2026/08/02
 * @brief バトルの実行ランナー
 * =====================================*/

using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using AttributeUtility;
using CustomConsole;

namespace PPCore
{
    // プッシャーとバトルを組み立てるエントリポイント（本番用）
    // パーティ生成は PPPartyFactory を経由し、プッシャーの読み込みを待ってからバトルを始める
    public class PPBattleRunner : MonoBehaviour
    {
        /* インスペクタ設定 */

        // ロード画面を出してプッシャーを準備する役。設定するとバトルの開始を読み込みの後まで待つ。未設定なら待たない
        [Header("プッシャー")]
        [Label("プッシャーの読み込み")]
        [SerializeField] private PPPusherLoader mPusherLoader;

        // ユニットとビューを対応付けるバインダー
        [Header("UI")]
        [Label("バトルビューバインダー")]
        [SerializeField] private PPStageUnitViewBinder mBattleUnitViewBinder;

        // バトル中のメニューを開く入力。キー設定では変更できない
        [Header("インプット")]
        [Label("メニューを開く入力")]
        [SerializeField] private InputActionReference mOpenConfigAction;
        // 未設定なら、メニューを挟まずにキー設定を開く
        [Label("バトル中のメニュー")]
        [SerializeField] private PPBattlePauseMenuView mPauseMenu;
        // メニューが未設定のときに直接開く。メニューを使うときは、メニュー側のキー設定を使う
        [Label("キー設定")]
        [SerializeField] private PPInputConfigMenuView mConfigMenu;

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
        // ステージで負けたときと、ボスに勝ったときに出す。未設定なら結果を出さずにタイトルへ戻る
        [Label("決着の結果画面")]
        [SerializeField] private PPStageResultView mResultView;
        // 結果画面が未設定のときに戻るシーン。Build Profiles に登録したシーンの名前
        [Label("結果のあとに戻るシーン名")]
        [SerializeField] private string mTitleSceneName = "S_Title";

        /* 状態 */

        // バトル進行を統括するマネージャ
        private BattleManager mBattleManager = new();

        /* Unity イベント */

        // プッシャーの読み込みを待ってからバトルを組み立てて開始し、最後にプッシャーを動かし始める
        IEnumerator Start()
        {
            if (mPusherLoader != null) yield return mPusherLoader.Load();
            StartBattle();
            if (mPusherLoader != null) mPusherLoader.Begin();
        }

        // バトル専用の時計を進め、メニューの開閉を受け付ける
        void Update()
        {
            // プッシャーの読み込み中は、まだバトルを組み立てていない
            if (mBattleManager.Context == null) return;

            mBattleManager.AdvanceTime(Time.deltaTime);
            UpdateMenu();
        }

        /* 初期化・接続 */

        // バトルを組み立てて開始する。パーティ生成 → コンテキスト構築 → バトル開始 → View の接続、の順に進む
        private void StartBattle()
        {
            mBattleManager = new BattleManager { TimeProvider = () => Time.time };
            if (mOpenConfigAction != null) mOpenConfigAction.action.Enable();

            // ステージのマスから来たときは持ち越した編成とマスの敵で、Editor のデバッグバトルから始めたときはその設定で、
            // 単体で Play したときはインスペクタの定義で戦う
            var stage = PPStageProgress.Current;
            var stageBattle = stage?.PendingBattle;
            var debugBattle = stageBattle == null ? PPDebugBattleLauncher.TakeRequested() : null;
            var context = new BattleContext() { Rules = new PPBattleRules() };
            if (stageBattle != null)
            {
                context.AllyParty = stage.CreateAllyParty();
                context.EnemyParty = PPPartyFactory.CreateFromDefinition(stageBattle.Enemy, BattleSide.Enemy);
            }
            else if (debugBattle != null)
            {
                CustomConsoleLog.Log("Battle", $"デバッグバトル: {debugBattle.name} の設定で始める");
                context.AllyParty = debugBattle.Ally.Create(BattleSide.Ally);
                context.EnemyParty = debugBattle.Enemy.Create(BattleSide.Enemy);
            }
            else
            {
                context.AllyParty = PPPartyFactory.CreateFromDefinition(mAllyPartyDefinition, BattleSide.Ally);
                context.EnemyParty = PPPartyFactory.CreateFromDefinition(mEnemyPartyDefinition, BattleSide.Enemy);
            }

            if (stageBattle != null) mBattleManager.OnBattleEnded += r => StartCoroutine(ReturnToStage(stage, r));
            PPBattleLogBinder.Bind(mBattleManager);
            mBattleManager.StartBattle(context);
            mBattleUnitViewBinder.Bind(mBattleManager);
        }

        /* 操作 */

        // メニューの開閉を受け付ける。メニューが未設定なら、キー設定を直接開閉する
        private void UpdateMenu()
        {
            bool canOpen = WasPressed(mOpenConfigAction) && mBattleManager.StateMachine.Current != BattleState.BattleEnd;
            if (mPauseMenu != null)
            {
                if (canOpen && !mPauseMenu.IsOpen) mPauseMenu.Open(mBattleManager);
                return;
            }

            if (mConfigMenu != null && canOpen) mConfigMenu.Toggle(mBattleManager);
        }

        // 決着をステージの進行状態へ反映し、少し待ってから次へ進む。勝って続けるならマップのシーンへ戻り、
        // 負けたときとボスに勝ったときは結果画面を出す(決定でタイトルへ戻る)
        // メニューを開いたまま決着して timeScale が 0 のまま残っても待てるよう、待ち時間は timeScale に左右されない時間で数える
        private IEnumerator ReturnToStage(PPStageProgress aStage, BattleResult aResult)
        {
            string returnScene = aStage.ReturnSceneName;
            string stageName = aStage.Definition.DisplayName;
            int floor = aStage.PendingBattle != null ? aStage.PendingBattle.Node.x : 0;
            var outcome = aStage.ResolveBattle(aResult.Type);
            CustomConsoleLog.Log("Battle", outcome == PPStageBattleOutcome.Continue
                ? $"決着: {aResult.Type}。{mReturnToStageDelay} 秒後に {returnScene} へ戻る"
                : $"決着: {aResult.Type}。{mReturnToStageDelay} 秒後に結果を出す ({outcome})");

            yield return new WaitForSecondsRealtime(mReturnToStageDelay);
            if (outcome != PPStageBattleOutcome.Continue && mResultView != null)
            {
                mResultView.Open(outcome, aResult.Type, stageName, floor);
                yield break;
            }
            if (outcome != PPStageBattleOutcome.Continue)
            {
                CustomConsoleLog.Warning("Battle", $"決着の結果画面が未設定のため、結果を出さずに {mTitleSceneName} へ戻ります。PPBattleRunner の「決着の結果画面」を割り当ててください");
                PPStageStartSelection.Clear();
                returnScene = mTitleSceneName;
            }
            // 次のバトルでプッシャーの物理が止まったままにならないよう、時間の流れを戻してから移る
            Time.timeScale = 1f;
            PPSceneTransition.Load(returnScene);
        }

        /* ヘルパー */

        private static bool WasPressed(InputActionReference aAction) => aAction != null && aAction.action.WasPressedThisFrame();
    }
}
