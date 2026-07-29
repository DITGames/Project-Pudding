/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file SpeedTurnOrderResolver.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 行動順制御の標準実装
 * =====================================*/
using System.Collections.Generic;
using System.Linq;

namespace CommandBattleCore
{
    /// <summary>
    /// 速度パラメータの降順で行動順を決める標準実装。
    /// <see cref="SpeedJitter"/> を設定すると速度に乱数の揺らぎが乗るため、
    /// 速度が同値のユニット同士の順序が毎ターン入れ替わるようになる。
    /// 乱数はシード管理・再現性のため <c>aContext.Rules.RandomProvider</c> を経由する。
    /// </summary>
    public class SpeedTurnOrderResolver : ITurnOrderResolver
    {
        /// <summary>速度に加算する揺らぎの最大値。0 なら揺らぎなしで純粋な速度順になる。</summary>
        public float SpeedJitter { get; set; } = 0f;

        /// <summary>
        /// 敵味方の生存アクティブメンバーをまとめ、速度（＋揺らぎ）の降順に並べて返す。
        /// </summary>
        /// <param name="aContext">バトルコンテキスト。</param>
        /// <returns>先に行動する順に並べたユニットのリスト。</returns>
        public List<BattleUnit> ResolveOrder(BattleContext aContext)
        {
            var all = aContext.AllyParty.GetAliveActiveMembers()
                .Concat(aContext.EnemyParty.GetAliveActiveMembers());

            return all
                .OrderByDescending(u => u.Parameters.Speed.CurrentValue + (SpeedJitter > 0f
                    ? aContext.Rules.RandomProvider.NextFloat() * SpeedJitter : 0f)).ToList();
        }
    }
}
