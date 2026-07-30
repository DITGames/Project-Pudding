/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPEnemyAIDriver.cs
 * @author hqrse
 * @date 2026/07/18
 * @brief パーティ戦略を一定間隔で駆動するクラス
 * =====================================*/

using System.Collections;
using System.Linq;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    /// <summary>
    /// パーティ AI を一定間隔で駆動するドライバ。
    /// <para>
    /// 本作のバトルはターン制ではなくプッシャーと並行してリアルタイムに進むため、
    /// 敵は「自分の番が来たら動く」のではなく、一定秒ごとに思考して動く。
    /// その周期を作るのがこのクラスの役割で、
    /// 思考そのものは <see cref="IPPPartyCommandStrategist"/> へ完全に委譲する。
    /// </para>
    /// <para>
    /// MonoBehaviour ではないため、<see cref="RunLoop"/> を呼び出し側のコルーチンとして起動する。
    /// </para>
    /// </summary>
    public sealed class PPEnemyAIDriver
    {
        /// <summary>コマンドの投入先。</summary>
        private readonly BattleManager mManager;
        /// <summary>思考対象の陣営。</summary>
        private readonly BattleSide mSide;
        /// <summary>パーティ側に設定が無い場合に使うフォールバックの AI。</summary>
        private readonly IPPPartyCommandStrategist mPartyCommandStrategist;
        /// <summary>思考間隔（秒）。</summary>
        private readonly float mThinkInterval;

        /// <param name="aManager">コマンドの投入先。</param>
        /// <param name="aSide">思考対象の陣営。</param>
        /// <param name="aPartyCommandStrategist">フォールバックの AI。</param>
        /// <param name="aThinkInterval">思考間隔（秒）。0.1 秒未満は 0.1 秒に丸められる。</param>
        public PPEnemyAIDriver(BattleManager aManager, BattleSide aSide,
            IPPPartyCommandStrategist aPartyCommandStrategist, float aThinkInterval)
        {
            mManager = aManager;
            mSide = aSide;
            mPartyCommandStrategist = aPartyCommandStrategist;
            mThinkInterval = Mathf.Max(0.1f, aThinkInterval);
        }

        /// <summary>
        /// バトルが終了するまで、思考間隔ごとに思考と実行を繰り返すコルーチン。
        /// 待機オブジェクトを使い回して毎周期の生成を避けている。
        /// </summary>
        /// <returns>コルーチンの列挙子。呼び出し側で StartCoroutine する。</returns>
        public IEnumerator RunLoop()
        {
            var wait = new WaitForSeconds(mThinkInterval);
            while (mManager != null && mManager.StateMachine.Current != BattleState.BattleEnd)
            {
                yield return wait;
                PlanAndExecuteOnce();
            }
        }

        /// <summary>
        /// 1 回分の思考と実行を行う。
        /// 計画を立て、実行順に並べてコマンドを積み、まとめて実行する。
        /// バトル終了時と他のコマンド実行中はスキップし、割り込みが起きないようにする。
        /// </summary>
        public void PlanAndExecuteOnce()
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

            var plan = strategist.PlanActions(party, mManager.Context);
            if(plan == null || plan.IsWait) return;

            foreach (var a in plan.Assignments.OrderBy(x => x.Order))
            {
                mManager.EnqueueCommand(a.Command);
            }

            mManager.ExecuteAllCommands();
        }
    }
}
