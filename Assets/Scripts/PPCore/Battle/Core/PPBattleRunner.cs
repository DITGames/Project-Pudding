/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleRunner.cs
 * @author hqrse
 * @date 2026/08/02
 * @brief バトルの実行ランナー
 * =====================================*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
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
        // 未設定なら、コインからユニットが生まれない
        [Label("コインとバトルの橋渡し")]
        [SerializeField] private PPCoinUnitBridge mCoinBridge;

        // ユニットが進む道。未設定なら道を使わない
        [Header("バトル画面")]
        [Label("バトルの道")]
        [SerializeField] private PPBattleField mField;
        // 味方と敵の拠点の真上に出す HP バー
        [Label("拠点の HP バー", true)]
        [SerializeField] private PPBattleBaseView[] mBaseViews = new PPBattleBaseView[0];

        // マップのマスから来たときはマップ定義の拠点を使う。敵の拠点はいつもバトル定義のもの
        [Header("拠点")]
        [Label("味方の拠点")]
        [SerializeField] private PPBaseSettings mAllyBase = new("味方の拠点", 20000f);

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

        // マップのマスからもデバッグバトルからも来ずに、このシーンを単体で Play したときに使う
        [Header("単体で Play したとき")]
        // 並べた順にパーティを作る(最大 PPPartyFactory.MaxMembers 体)
        [Label("味方パーティ", true)]
        [SerializeField] private List<PPPartyMemberEntry> mAllyMembers = new();
        [Label("バトル定義")]
        [FormerlySerializedAs("mStageDefinition")]
        [SerializeField] private PPBattleDefinition mBattleDefinition;
        // マップのマスの深さの代わりに、出てくる敵の種類へ割り振るレベル
        [Label("割り振るレベル")]
        [Min(0)]
        [SerializeField] private int mLevelPoints = 0;

        // 決着してから、マップへ戻るか結果画面を出すまでの秒数
        [Header("ステージ")]
        [Label("決着後にマップへ戻るまでの秒数")]
        [Min(0f)]
        [SerializeField] private float mReturnToStageDelay = 2f;
        // ステージで負けたときと、ボスに勝ったときに出す。単体で Play したときは決着のたびに出す。未設定なら結果を出さずにタイトルへ戻る
        [Label("決着の結果画面")]
        [SerializeField] private PPBattleResultView mResultView;
        // 結果画面が未設定のときに戻るシーン。Build Profiles に登録したシーンの名前
        [Label("結果のあとに戻るシーン名")]
        [SerializeField] private string mTitleSceneName = "S_Title";

        // 攻撃が当たるたびにコンソールへ書き出すため、ユニットが多いと重くなる。調べるときだけ入れる
        [Header("デバッグ")]
        [Label("バトルのログを出す")]
        [SerializeField] private bool mOutputBattleLog = false;

        /* 状態 */

        // バトル進行を統括するマネージャ
        private BattleManager mBattleManager = new();
        // このバトルで戦うバトル定義と、深さで割り振るレベルと、味方の拠点。コンテキストを作るときに決める
        private PPBattleDefinition mBattle;
        private int mBattleLevelPoints;
        private PPBaseSettings mBattleAllyBase;
        // ウェーブの進行。バトル定義が無いバトルでは null
        private PPWaveRunner mWaveRunner;

        /* プロパティ */

        // プッシャーの読み込み中はまだバトルを組み立てていないため、Context が null
        public BattleManager BattleManager => mBattleManager;

        /* Unity イベント */

        // パーティを作ってコインの抽選を用意し、プッシャーの読み込みを待ってからバトルを開始して、最後にプッシャーを動かし始める
        // 開始時に並べるコインへも中身を入れて大きさを決めるため、抽選は読み込みより先に用意する
        IEnumerator Start()
        {
            var context = CreateContext();
            if (mCoinBridge != null) mCoinBridge.Prepare(context.AllyParty, context.Rules.RandomProvider);
            if (mPusherLoader != null) yield return mPusherLoader.Load();
            StartBattle(context);
            if (mPusherLoader != null) mPusherLoader.Begin();
        }

        // バトル専用の時計を進め、メニューの開閉を受け付ける
        void Update()
        {
            // プッシャーの読み込み中は、まだバトルを組み立てていない
            if (mBattleManager.Context == null) return;

            mBattleManager.AdvanceTime(Time.deltaTime);
            mWaveRunner?.Update();
            UpdateMenu();
        }

        /* 初期化・接続 */

        // 味方パーティと道を持つコンテキストを作り、戦うバトル定義を決める。敵はウェーブで道に出すため、敵パーティは空にする
        // マップのマスから来たときは持ち越した編成とマスのバトル定義・深さで、Editor のデバッグバトルから始めたときはその設定で、
        // 単体で Play したときはインスペクタの設定で戦う
        private BattleContext CreateContext()
        {
            var stage = PPMapProgress.Current;
            var stageBattle = stage?.PendingBattle;
            var debugBattle = stageBattle == null ? PPDebugBattleLauncher.TakeRequested() : null;
            // 道があるバトルは、拠点の破壊で勝敗を決める
            var context = new BattleContext() { Rules = new PPBattleRules { ResultChecker = new PPBaseResultChecker() } };
            context.EnemyParty = new PPBattleParty(BattleSide.Enemy, new List<BattleUnit>());
            mBattleAllyBase = mAllyBase;
            if (stageBattle != null)
            {
                context.AllyParty = stage.CreateAllyParty();
                mBattle = stageBattle.Battle;
                mBattleLevelPoints = stage.Definition.GetLevelPoints(stageBattle.Depth);
                mBattleAllyBase = stage.Definition.AllyBase;
            }
            else if (debugBattle != null)
            {
                context.AllyParty = debugBattle.CreateAllyParty();
                mBattle = debugBattle.Battle;
                mBattleLevelPoints = debugBattle.LevelPoints;
            }
            else
            {
                context.AllyParty = PPPartyFactory.CreateFromMembers(mAllyMembers, BattleSide.Ally);
                mBattle = mBattleDefinition;
                mBattleLevelPoints = mLevelPoints;
            }
            // 道の長さはバトル定義のもの。道の状態と表示は、この長さで作る
            if (mField != null)
            {
                if (mBattle != null) mField.SetLength(mBattle.FieldLength);
                context.Field = new PPBattleFieldState(mField.Length, mField.BaseFrontDistance, mField.KnockbackDistance, mField.KnockbackSpeed);
            }
            string source = stageBattle != null ? "マップのマス" : debugBattle != null ? "デバッグバトル" : "単体の Play";
            CustomConsoleLog.Log("Battle", $"バトル: {source}。バトル定義={(mBattle != null ? mBattle.name : "なし")} 割り振るレベル={mBattleLevelPoints} 道の長さ={(mField != null ? mField.Length : 0f)}");
            return context;
        }

        // バトルを開始して道の両端に拠点を置き、View とコインの橋渡しを接続する
        private void StartBattle(BattleContext aContext)
        {
            mBattleManager = new BattleManager { TimeProvider = () => Time.time };
            if (mOpenConfigAction != null) mOpenConfigAction.action.Enable();

            var stage = PPMapProgress.Current;
            bool isStageBattle = stage?.PendingBattle != null;
            if (isStageBattle) mBattleManager.OnBattleEnded += r => StartCoroutine(ReturnToStage(stage, r));
            else mBattleManager.OnBattleEnded += r => StartCoroutine(ShowStandaloneResult(r));
            // 出さないときは、履歴を溜め続ける既定のロガーも外す
            if (mOutputBattleLog) PPBattleLogBinder.Bind(mBattleManager);
            else mBattleManager.Logger = null;
            mBattleManager.StartBattle(aContext);
            PlaceBases();
            mBattleUnitViewBinder.Bind(mBattleManager);
            if (mCoinBridge != null) mCoinBridge.Bind(mBattleManager);
            StartWaves();
        }

        // 道の両端に拠点を置き、HP バーを繋ぐ。敵の拠点はバトル定義のもの
        private void PlaceBases()
        {
            if (mBattleManager.Context?.Field == null) return;
            var enemySettings = mBattle != null ? mBattle.EnemyBase : new PPBaseSettings("敵の拠点", 20000f);
            mBattleManager.SetFieldBase(mBattleAllyBase.CreateRuntimeUnit("AllyBase"), BattleSide.Ally);
            mBattleManager.SetFieldBase(enemySettings.CreateRuntimeUnit("EnemyBase"), BattleSide.Enemy);
            foreach (var view in mBaseViews)
            {
                if (view != null) view.Bind(mBattleManager);
            }
        }

        // バトル定義のウェーブを始める。敵は敵拠点の近くに出すが、拠点の前で止まっている味方より左には出さない
        private void StartWaves()
        {
            var field = mBattleManager.Context?.Field;
            if (field == null) return;
            if (mBattle == null)
            {
                CustomConsoleLog.Warning("Battle", "バトル定義が無いため、敵を出しません。PPBattleRunner の「バトル定義」を割り当てるか、マップかデバッグバトルから始めてください", this);
                return;
            }
            float position = Mathf.Max(field.Length - mField.EnemySpawnDistance, field.GetStopLine(BattleSide.Ally));
            mWaveRunner = new PPWaveRunner(mBattle.Wave, mBattleManager, position, mBattleLevelPoints);
            foreach (var pair in mWaveRunner.LevelBonus)
            {
                if (pair.Value > 0) CustomConsoleLog.Log("Battle", $"ウェーブ: {pair.Key.DisplayName} に +{pair.Value} レベル");
            }
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
        private IEnumerator ReturnToStage(PPMapProgress aStage, BattleResult aResult)
        {
            string returnScene = aStage.ReturnSceneName;
            string stageName = aStage.Definition.DisplayName;
            int floor = aStage.PendingBattle != null ? aStage.PendingBattle.Node.x : 0;
            var outcome = aStage.ResolveBattle(aResult.Type);
            CustomConsoleLog.Log("Battle", outcome == PPMapBattleOutcome.Continue
                ? $"決着: {aResult.Type}。{mReturnToStageDelay} 秒後に {returnScene} へ戻る"
                : $"決着: {aResult.Type}。{mReturnToStageDelay} 秒後に結果を出す ({outcome})");

            yield return new WaitForSecondsRealtime(mReturnToStageDelay);
            if (outcome != PPMapBattleOutcome.Continue && mResultView != null)
            {
                mResultView.Open(outcome, aResult.Type, stageName, floor);
                yield break;
            }
            if (outcome != PPMapBattleOutcome.Continue)
            {
                CustomConsoleLog.Warning("Battle", $"決着の結果画面が未設定のため、結果を出さずに {mTitleSceneName} へ戻ります。PPBattleRunner の「決着の結果画面」を割り当ててください");
                PPMapStartSelection.Clear();
                returnScene = mTitleSceneName;
            }
            // 次のバトルでプッシャーの物理が止まったままにならないよう、時間の流れを戻してから移る
            Time.timeScale = 1f;
            PPSceneTransition.Load(returnScene);
        }

        // ステージを通さずに Play したときの決着。少し待ってから結果画面を出す(決定でタイトルへ戻る)
        private IEnumerator ShowStandaloneResult(BattleResult aResult)
        {
            CustomConsoleLog.Log("Battle", $"決着: {aResult.Type}。{mReturnToStageDelay} 秒後に結果を出す");
            yield return new WaitForSecondsRealtime(mReturnToStageDelay);
            if (mResultView == null)
            {
                CustomConsoleLog.Warning("Battle", "決着の結果画面が未設定のため、結果を出しません。PPBattleRunner の「決着の結果画面」を割り当ててください", this);
                yield break;
            }
            mResultView.OpenBattleResult(aResult.Type);
        }

        /* ヘルパー */

        private static bool WasPressed(InputActionReference aAction) => aAction != null && aAction.action.WasPressedThisFrame();
    }
}
