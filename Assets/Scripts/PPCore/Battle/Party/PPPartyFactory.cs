/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyFactory.cs
 * @author hqrse
 * @date 2026/08/02
 * @brief パーティ定義からランタイムパーティを生成するファクトリ
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;

namespace PPCore
{
    // PPPartyDefinition などの生成元から PPBattleParty を組み立てるファクトリ
    // パーティの生成経路（デバッグ用のアセット割り当て、将来的なセーブデータ経由）を
    // ここに集約し、呼び出し側は生成元の違いを意識せずランタイムパーティを受け取れるようにする
    public static class PPPartyFactory
    {
        // PPPartyDefinition からランタイムパーティを生成する
        // メンバーごとにユニットを生成して編成する
        // aDefinition : 生成元のパーティ定義
        // aSide : このパーティの陣営
        // aItems : 初期所持アイテム。null なら空のインベントリになる
        // return : 生成されたランタイムパーティ
        public static PPBattleParty CreateFromDefinition(PPPartyDefinition aDefinition, BattleSide aSide,
            IReadOnlyDictionary<PPItemDefinition, int> aItems = null)
        {
            var units = new List<BattleUnit>();
            foreach (var entry in aDefinition.Members)
            {
                if(entry == null || entry.Unit == null) continue;

                units.Add((PPBattleUnit)entry.Unit.CreateRuntimeUnit(entry.Level));
            }

            return new PPBattleParty(aDefinition.MaxResource, aDefinition.BaseResourceConversionRate, aSide, units, null, aItems);
        }
    }
}
