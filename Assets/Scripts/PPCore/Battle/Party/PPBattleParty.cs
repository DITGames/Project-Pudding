/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleParty.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief バトルパーティのベースクラス
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;

namespace PPCore
{
    // Project-Pudding 固有の要素を載せたバトルパーティ
    // 汎用の BattleParty に対して、パーティ共有の要素を 2 つ追加する
    // 1. コイン変換係数（プッシャーから落ちたコインをゲージ量へ換算する倍率）
    // 2. パーティ単位で行動を組み立てる AI ストラテジスト
    // 行動リソースはユニットが専有するため、パーティ側はリソースを保持しない
    public class PPBattleParty : BattleParty
    {
        // コイン 1 枚あたりのゲージ変換係数。PPCoinResourceBridge が参照する
        public Parameter CoinConversionRate { get; }

        // このパーティの行動計画を立てる AI。プレイヤー操作パーティなら null のまま
        public IPPPartyCommandStrategist Strategist { get; set; }

        // aBaseCoinRate : コイン変換係数の初期値
        // aSide : このパーティの陣営
        // aActiveMembers : 戦場に出すメンバー
        // aReserveMembers : 控えメンバー。不要なら null
        public PPBattleParty(float aBaseCoinRate, BattleSide aSide, IEnumerable<BattleUnit> aActiveMembers,
            IEnumerable<BattleUnit> aReserveMembers = null)
            : base(aSide, aActiveMembers, aReserveMembers)
        {
            CoinConversionRate = new Parameter(aBaseCoinRate);
        }
    }
}
