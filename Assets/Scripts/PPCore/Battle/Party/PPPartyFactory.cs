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
        // メンバーごとにユニットを生成して編成し、それぞれへ専用の乱数供給元を割り当てる
        // aDefinition : 生成元のパーティ定義
        // aSide : このパーティの陣営
        // return : 生成されたランタイムパーティ
        public static PPBattleParty CreateFromDefinition(PPPartyDefinition aDefinition, BattleSide aSide)
        {
            var units = new List<BattleUnit>();
            for (int i = 0; i < aDefinition.Members.Count; i++)
            {
                var entry = aDefinition.Members[i];
                if(entry == null || entry.Unit == null) continue;

                var unit = (PPBattleUnit)entry.Unit.CreateRuntimeUnit(entry.Level);
                unit.Random = CreateRandom(entry, aSide, i);
                units.Add(unit);
            }

            return new PPBattleParty(aDefinition.BaseResourceConversionRate, aSide, units);
        }

        // 編成 1 体分の乱数供給元を作る
        //
        // シードを固定しない場合も、ユニットごとに別の乱数列を割り当てる
        // 1 本の列を全員で共有すると他のユニットの消費で自分の乱数がずれてしまい、
        // 単体の挙動を追えなくなるため、独立させること自体に意味がある
        //
        // 固定する場合は、編成位置と陣営をシードへ混ぜる
        // 同じユニット定義を複数体並べたときに、全員が同じ行動を取るのを防ぐため
        // aEntry : 編成 1 体分の設定
        // aSide : このパーティの陣営
        // aIndex : 編成上の位置
        // return : 割り当てる乱数供給元
        private static IRandomProvider CreateRandom(PPPartyMemberEntry aEntry, BattleSide aSide, int aIndex)
        {
            if (!aEntry.IsFixedSeed) return new DefaultRandomProvider();

            int seed = aEntry.Seed;
            seed = seed * 397 + aIndex;
            seed = seed * 397 + (int)aSide;
            return new DefaultRandomProvider(seed);
        }
    }
}
