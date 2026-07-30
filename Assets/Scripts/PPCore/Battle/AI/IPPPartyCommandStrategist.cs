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
    /// <summary>
    /// パーティ全体を俯瞰して行動計画を立てる AI のインターフェース。
    /// <para>
    /// ユニット単位で決める <see cref="ICommandDecider"/> と違い、
    /// パーティ共有のリソースを誰に割り当てるかまで含めて 1 回で決めるのが役割。
    /// 実装は <see cref="PPPartyAIStrategistBase"/>、駆動は <see cref="PPEnemyAIDriver"/> が担う。
    /// </para>
    /// </summary>
    public interface IPPPartyCommandStrategist
    {
        /// <summary>
        /// このティックでパーティが取る行動計画を組み立てる。
        /// </summary>
        /// <param name="aSelf">思考主体のパーティ。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        /// <returns>行動の割り当て。何もしない場合は <see cref="PPPartyPlan.Wait"/>。</returns>
        PPPartyPlan PlanActions(BattleParty aSelf, BattleContext aContext);
    }
}
