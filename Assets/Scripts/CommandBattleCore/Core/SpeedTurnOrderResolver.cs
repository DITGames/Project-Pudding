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
    // 速度パラメータの降順で行動順を決める標準実装
    // SpeedJitter を設定すると速度に乱数の揺らぎが乗るため、
    // 速度が同値のユニット同士の順序が毎ターン入れ替わるようになる
    // 揺らぎはユニットごとの試行なので、それぞれ自分の乱数供給元から引く
    public class SpeedTurnOrderResolver : ITurnOrderResolver
    {
        // 速度に加算する揺らぎの最大値。0 なら揺らぎなしで純粋な速度順になる
        public float SpeedJitter { get; set; } = 0f;

        // 敵味方の生存アクティブメンバーをまとめ、速度（＋揺らぎ）の降順に並べて返す
        // aContext : バトルコンテキスト
        // return : 先に行動する順に並べたユニットのリスト
        public List<BattleUnit> ResolveOrder(BattleContext aContext)
        {
            var all = aContext.AllyParty.GetAliveActiveMembers()
                .Concat(aContext.EnemyParty.GetAliveActiveMembers());

            return all
                .OrderByDescending(u => u.Parameters.Speed.CurrentValue + (SpeedJitter > 0f
                    ? u.ResolveRandom(aContext).NextFloat() * SpeedJitter : 0f)).ToList();
        }
    }
}
