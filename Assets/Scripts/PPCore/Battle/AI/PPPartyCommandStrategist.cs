/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPPartyCommandStrategist.cs
 * @author hqrse
 * @date 2026/07/16
 * @brief パーティ単位の行動計画（ストラテジスト・計画・駆動ドライバ）
 * =====================================*/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PPCore
{
    // パーティ全体を俯瞰して行動計画を立てる AI のインターフェース
    // ユニット 1 体ずつ独立に決める ICommandDecider と違い、
    // パーティ全員分の行動を 1 回の思考でまとめて組み立てるのが役割
    // 実装は PPUnitAIStrategist、駆動は PPEnemyAIDriver が担う
    public interface IPPPartyCommandStrategist
    {
        // バトル進行の管理役と、実行待ちの行動の供給元を受け取る
        //
        // PlanActions が受け取るバトルコンテキストからは進行の管理役を辿れないため、
        // 「誰に殴られたか」のようなバトル中の出来事を知るには開始時に別途渡す必要がある
        // 実行待ちの行動も、思考時点ではコマンド列に積まれていないため進行役から借りる
        //
        // aManager : バトル進行の管理役
        // aSide : この思考ルーチンが担当する陣営
        // aPendingSource : 実行待ちの行動の供給元。使わない場合は null でよい
        void BindBattle(BattleManager aManager, BattleSide aSide, IPPPendingActionSource aPendingSource);

        // バトル進行の管理役との繋がりを断つ
        // BindBattle で張った購読を残したままにすると、バトルが終わったあとや
        // 進行役を作り直したあとも、古い思考ルーチンがイベントを拾い続けてしまう
        void Unbind();

        // このティックでパーティが取る行動計画を組み立てる
        // aSelf : 思考主体のパーティ
        // aContext : バトルコンテキスト
        // return : 行動の割り当て。何もしない場合は PPPartyPlan.Wait
        PPPartyPlan PlanActions(BattleParty aSelf, BattleContext aContext);
    }

    // ユニット 1 体分の行動割り当て。誰が・何を・どの順番で実行するかを 1 組にしたもの
    public readonly struct PPPartyActionAssignment
    {
        // 行動するユニット
        public PPBattleUnit Unit { get; }
        // 実行するコマンド
        public BattleCommandBase Command { get; }
        // 同一ティック内での実行順序。小さいほど先に実行される
        public int Order { get; }

        // aUnit : 行動するユニット
        // aCommand : 実行するコマンド
        // aOrder : 実行順序
        public PPPartyActionAssignment(PPBattleUnit aUnit, BattleCommandBase aCommand, int aOrder = 0)
        {
            Unit = aUnit;
            Command = aCommand;
            Order = aOrder;
        }
    }

    // 1 ティック分のパーティ行動計画
    // 割り当てが空であることが、そのまま「今回は動かずゲージを溜める」の意味になる
    // 待機のためだけの状態やフラグを持たせず、空リストで表現している
    public sealed class PPPartyPlan
    {
        // 採用された行動の割り当て。実行前に PPPartyActionAssignment.Order 順へ並べ替えられる
        public IReadOnlyList<PPPartyActionAssignment> Assignments { get; }
        // 待機（何もせずゲージを溜める）かどうか
        public bool IsWait => Assignments.Count == 0;

        // 待機を表す共有インスタンス。毎回の思考で新規生成しないためのもの
        public static readonly PPPartyPlan Wait = new PPPartyPlan(Array.Empty<PPPartyActionAssignment>());

        // aAssignments : 採用された行動の割り当て
        public PPPartyPlan(IReadOnlyList<PPPartyActionAssignment> aAssignments) => Assignments = aAssignments;
    }

    // パーティ AI を一定間隔で駆動するドライバ
    // 本作のバトルはターン制ではなくプッシャーと並行してリアルタイムに進むため、
    // 敵は「自分の番が来たら動く」のではなく、一定間隔で思考して動く
    // その周期を作るのがこのクラスの役割で、思考そのものは IPPPartyCommandStrategist へ完全に委譲する
    // MonoBehaviour ではないため、RunLoop を呼び出し側のコルーチンとして起動する
    public sealed class PPEnemyAIDriver
    {
        // コマンドの投入先
        private readonly BattleManager mManager;
        // 思考対象の陣営
        private readonly BattleSide mSide;
        // パーティ側に設定が無い場合に使うフォールバックの AI
        private readonly IPPPartyCommandStrategist mPartyCommandStrategist;
        // 思考間隔（秒）。ティック間隔を思考回数で割って求める
        private readonly float mThinkInterval;

        // 直近の思考で立てた計画。ティックの終わりに収集役が取りに来る
        // 複数回思考した場合は最後の内容で上書きされる
        public PPPartyPlan LatestPlan { get; private set; }

        // 思考対象の陣営。実行待ちの行動を陣営で絞り込む側から参照する
        public BattleSide Side => mSide;

        // aManager : コマンドの投入先
        // aSide : 思考対象の陣営
        // aPartyCommandStrategist : フォールバックの AI
        // aTickInterval : ターン経過の間隔（秒）。呼び出し側のティック駆動と同じ値を渡す
        // aThinkCountPerTick : 1 ティックあたりの思考回数。1 未満は 1 に丸められる
        public PPEnemyAIDriver(BattleManager aManager, BattleSide aSide,
            IPPPartyCommandStrategist aPartyCommandStrategist, float aTickInterval, int aThinkCountPerTick)
        {
            mManager = aManager;
            mSide = aSide;
            mPartyCommandStrategist = aPartyCommandStrategist;
            // 思考間隔は秒で直接指定せず、ティック間隔を思考回数で割って求める
            // クールタイムなど AI の時間感覚がティック基準で揃うため、ティック側だけ調整すれば済む
            mThinkInterval = Mathf.Max(0.1f, aTickInterval / Mathf.Max(1, aThinkCountPerTick));
        }

        // 計画を破棄する。ティックの行動を流し終えたタイミングで呼ぶ
        public void ConsumePlan() => LatestPlan = null;

        // バトルが終了するまで、思考間隔ごとに思考を繰り返すコルーチン
        // 待機オブジェクトを使い回して毎周期の生成を避けている
        // return : コルーチンの列挙子。呼び出し側で StartCoroutine する
        public IEnumerator RunLoop()
        {
            var wait = new WaitForSeconds(mThinkInterval);
            while (mManager != null && mManager.StateMachine.Current != BattleState.BattleEnd)
            {
                yield return wait;
                PlanOnce();
            }
        }

        // 1 回分の思考を行い、結果を保持する
        // ここでは実行せず、ティックの終わりに収集役がまとめて流す
        // 1 ティックに複数回思考する場合は、最後に立てた計画が採用される
        // バトル終了時と他のコマンド実行中はスキップし、割り込みが起きないようにする
        public void PlanOnce()
        {
            if(mManager == null) return;

            // 実行中に割り込むと行動が入れ子になるため、この 2 状態では思考しない
            var state = mManager.StateMachine.Current;
            if(state == BattleState.BattleEnd || state == BattleState.ActionExecution)
                return;

            var party = mManager.Context.GetParty(mSide) as PPBattleParty;
            if(party == null)
                return;

            // パーティ個別の AI を優先し、無ければコンストラクタで受けたものを使う
            var strategist = party.Strategist ?? mPartyCommandStrategist;
            if(strategist == null)
                return;

            LatestPlan = strategist.PlanActions(party, mManager.Context);
        }
    }
}
