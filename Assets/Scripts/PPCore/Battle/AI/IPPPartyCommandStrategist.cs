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
        // このティックでパーティが取る行動計画を組み立てる
        // aSelf : 思考主体のパーティ
        // aContext : バトルコンテキスト
        // return : 行動の割り当て。何もしない場合は PPPartyPlan.Wait
        PPPartyPlan PlanActions(BattleParty aSelf, BattleContext aContext);
    }
}
