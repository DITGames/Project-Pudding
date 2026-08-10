/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyFactory.cs
 * @author hqrse
 * @date 2026/08/02
 * @brief パーティ定義からランタイムパーティを生成するファクトリ
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // PPPartyDefinition などの生成元から PPBattleParty を組み立てるファクトリ
    // パーティの生成経路（デバッグ用のアセット割り当て、将来的なセーブデータ経由）を
    // ここに集約し、呼び出し側は生成元の違いを意識せずランタイムパーティを受け取れるようにする
    public static class PPPartyFactory
    {
        // PPPartyDefinition からランタイムパーティを生成する
        // メンバーごとにユニットを生成し、ロール・知能の上書きを適用してから編成する
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
                if(entry == null) continue;
                var unit = (PPBattleUnit)entry.Unit.CreateRuntimeUnit(entry.Level);
                unit.AssignedRole = ResolveRole(entry);
                unit.Intelligence = ResolveIntelligence(entry);
                units.Add(unit);
            }

            var party = new PPBattleParty(aDefinition.MaxResource, aDefinition.BaseResourceConversionRate, aSide, units, null, aItems)
            {
                PatienceCoefficient = aDefinition.ResolvePatienceCoefficient(),
            };
            return party;
        }

        // 適用するロールを解決する。上書きが Inherit 以外ならそちらを優先する
        // aEntry : 対象のメンバー設定
        private static PPUnitRole ResolveRole(PPPartyMemberEntry aEntry)
        {
            if(aEntry.RoleOverride != PPUnitRole.Inherit) return aEntry.RoleOverride;
            return aEntry.Unit.DefaultRole;
        }

        // 適用する知能を解決する。上書き指定時のみ手入力値を 0～1 に丸めて使う
        // ここで 0 になった場合は、AI 側でプロファイルの値へフォールバックする
        // aEntry : 対象のメンバー設定
        private static float ResolveIntelligence(PPPartyMemberEntry aEntry)
        {
            if(aEntry.IsOverrideIntelligence) return Mathf.Clamp01(aEntry.IntelligenceOverride);
            return aEntry.Unit.DefaultIntelligence;
        }
    }
}
