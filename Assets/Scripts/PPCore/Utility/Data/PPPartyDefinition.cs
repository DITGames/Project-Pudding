/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyDefinition.cs
 * @author hqrse
 * @date 2026/07/23
 * @brief パーティ定義
 * =====================================*/
using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    public enum PPPartyEncounterType
    {
        Trash,
        Elite,
        Boss,
        Custom,
    }

    [Serializable]
    public sealed class PPPartyMemberEntry
    {
        [Label("ユニット定義")] public PPUnitDefinition Unit;
        [Label("レベル")] public int Level = 1;
        
        [Header("上書き")]
        [Label("ロール上書き")] public PPUnitRole RoleOverride = PPUnitRole.Inherit;
        [Label("行動スコアを上書きする?")] public bool IsOverrideActionScore = false;
        [Label("行動スコア上書き値")]public PPUnitActionScoreModifier ActionScoreOverride = new();
    }
    
    [CreateAssetMenu(fileName = "PPPartyDefinition", menuName = "Project-Pudding/Battle/PPPartyDefinition")]
    public class PPPartyDefinition : ScriptableObject
    {
        [Header("パーティ")]
        [Label("パーティメンバー", true)] public List<PPPartyMemberEntry> Members = new();
        [Label("リソース上限")] public int MaxResource = 100;
        [Label("リソース変換レート初期値")] public float BaseResourceConversionRate = 1f;
        
        [Header("AI")]
        [Label("パーティ種別")] public PPPartyEncounterType EncounterType = PPPartyEncounterType.Trash;
        [Label("カスタム忍耐係数")]
        [EditCondition(nameof(IsCustomEncounterType), true, false)]
        public float CustomPatienceCoefficient = 1f;
        
        protected bool IsCustomEncounterType
            => EncounterType == PPPartyEncounterType.Custom;
        
        private static float DefaultCoefficientFor(PPPartyEncounterType aType)
            => aType switch
            {
                PPPartyEncounterType.Trash => 0.5f,
                PPPartyEncounterType.Elite => 1f,
                PPPartyEncounterType.Boss => 1.8f,
                _ => 1.0f,
            };
        
        public float ResolvePatienceCoefficient()
            => EncounterType == PPPartyEncounterType.Custom ? CustomPatienceCoefficient : DefaultCoefficientFor(EncounterType);

        public PPBattleParty CreateRuntimeParty(BattleSide aSide,
            IReadOnlyDictionary<PPItemDefinition, int> aItems = null)
        {
            var units = new List<BattleUnit>();
            foreach (var entry in Members)
            {
                if(entry == null) continue;
                var unit = (PPBattleUnit)entry.Unit.CreateRuntimeUnit(entry.Level);
                unit.AssignedRole = ResolveRole(entry);
                unit.ScoreModifier = entry.IsOverrideActionScore ? entry.ActionScoreOverride : entry.Unit.ActionScoreModifier;
                units.Add(unit);
            }
            
            var party = new PPBattleParty(MaxResource, BaseResourceConversionRate, aSide, units, null, aItems)
            {
                PatienceCoefficient = ResolvePatienceCoefficient(),
            };
            return party;
        }

        private static PPUnitRole ResolveRole(PPPartyMemberEntry aEntry)
        {
            if(aEntry.RoleOverride != PPUnitRole.Inherit) return aEntry.RoleOverride;
            return aEntry.Unit.DefaultRole;
        }
    }
}