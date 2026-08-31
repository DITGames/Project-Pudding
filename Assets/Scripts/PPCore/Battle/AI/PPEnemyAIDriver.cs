/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPEnemyAIDriver.cs
 * @author hqrse
 * @date 2026/07/18
 * @brief パーティ戦略を一定間隔で駆動するクラス
 * =====================================*/

using System.Collections;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
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
