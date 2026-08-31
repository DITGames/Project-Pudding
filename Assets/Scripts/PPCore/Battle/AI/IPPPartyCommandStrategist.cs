/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IPPPartyCommandStrategist.cs
 * @author hqrse
 * @date 2026/07/16
 * @brief パーティ全体を俯瞰して行動計画を立てる
 * =====================================*/

using CommandBattleCore;

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
}
