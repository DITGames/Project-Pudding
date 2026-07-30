/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file TurnBattleSample.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief ターン制バトルのサンプル
 * =====================================*/

using System.Linq;
using UnityEngine;

namespace CommandBattleCore
{
    // CommandBattleCore だけでターン制バトルを回す最小サンプル
    // バトルの組み立て方（ユニット生成 → パーティ生成 → コンテキスト構築 → イベント購読 → 開始）と、
    // 1 ターンの回し方（行動順取得 → コマンド決定 → キュー実行 → ターン経過）を示すための参照実装
    public class TurnBattleRunnerSample : MonoBehaviour
    {
        // 味方として生成するユニット定義
        [SerializeField] private UnitDefinition[] mAllyDefinitions;
        // 敵として生成するユニット定義
        [SerializeField] private UnitDefinition[] mEnemyDefinitions;

        // このサンプルが駆動するバトルマネージャ
        private BattleManager mBattleManager;

        // 定義からユニットを生成して両パーティを組み立て、
        // ログ出力用のイベントを購読してからバトルを開始する
        private void Start()
        {
            mBattleManager = new BattleManager {TimeProvider = () => Time.time};

            var allies = mAllyDefinitions.Select(d => d.CreateRuntimeUnit()).ToList();
            var enemies = mEnemyDefinitions.Select(d => d.CreateRuntimeUnit()).ToList();

            var context = new BattleContext()
            {
                AllyParty = new BattleParty(BattleSide.Ally, allies),
                EnemyParty = new BattleParty(BattleSide.Enemy, enemies),
            };

            mBattleManager.OnDamageTaken += (u, d) =>
                Debug.Log($"{u.DisplayName} <- {d} dmg (HP: {u.Parameters.Hp.CurrentValue})");
            mBattleManager.OnUnitDefeated += u => Debug.Log($"{u.DisplayName} defeated!");
            mBattleManager.OnBattleEnded += r => Debug.Log($"Battle End: {r.Type}");

            mBattleManager.StartBattle(context);
        }

        // 1 ターン分を進める。行動順に全ユニットのコマンドを積み、
        // まとめて実行してからターンを経過させる
        // インスペクタのコンテキストメニューから手動で呼ぶ想定
        [ContextMenu("Run One Turn")]
        public void RunOneTurn()
        {
            if (mBattleManager.StateMachine.Current == BattleState.BattleEnd) return;

            // 行動順を取得
            var units = mBattleManager.GetTurnOrder();

            // コマンドを選択
            foreach (var unit in units)
            {
                mBattleManager.EnqueueCommand(unit.CommandDecider.DecideCommand(unit, mBattleManager.Context));
            }

            mBattleManager.ExecuteAllCommands();
            mBattleManager.AdvanceTick();
        }
    }
}
