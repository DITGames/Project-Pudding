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
    // 汎用の BattleParty に対して、パーティ共有の要素を 3 つ追加する
    // 1. 属性別の行動リソースプール（プッシャーから落ちたコインの変換先）
    // 2. アイテムインベントリ
    // 3. パーティ単位で行動を組み立てる AI ストラテジスト
    public class PPBattleParty : BattleParty
    {
        // 属性ごとの行動リソースプール。スキルのコストはここから支払う
        public PPBattleResourcePool ResourcePool { get; }

        // パーティ共有のアイテム所持数
        public PPItemInventory Inventory { get; }

        // コイン 1 枚あたりのリソース変換係数。PPCoinResourceBridge が参照する
        public Parameter CoinConversionRate { get; }

        // このパーティの行動計画を立てる AI。プレイヤー操作パーティなら null のまま
        public IPPPartyCommandStrategist Strategist { get; set; }

        // aMaxCoin : リソースプールの属性ごとの上限値
        // aBaseCoinRate : コイン変換係数の初期値
        // aSide : このパーティの陣営
        // aActiveMembers : 戦場に出すメンバー
        // aReserveMembers : 控えメンバー。不要なら null
        // aItems : 初期所持アイテムと個数。不要なら null
        public PPBattleParty(int aMaxCoin, float aBaseCoinRate, BattleSide aSide, IEnumerable<BattleUnit> aActiveMembers,
            IEnumerable<BattleUnit> aReserveMembers = null, IReadOnlyDictionary<PPItemDefinition, int> aItems = null)
            : base(aSide, aActiveMembers, aReserveMembers)
        {
            ResourcePool = new PPBattleResourcePool(aMaxCoin);
            CoinConversionRate = new Parameter(aBaseCoinRate);
            Inventory = new PPItemInventory(aItems);
        }
    }
}
