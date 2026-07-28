/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleUnit.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief バトルユニットのベースクラス
 * =====================================*/
using CommandBattleCore;

namespace PPCore
{
    public class PPBattleUnit : BattleUnit
    {
        public PPParameterSet ExtraParameters { get; }
        
        public PPUnitRole AssignedRole { get; set; } = PPUnitRole.Inherit;
        public PPUnitActionScoreModifier ScoreModifier { get; set; } = new PPUnitActionScoreModifier();
        
        // -1はパーティのIntelligenceを継承
        public float Intelligence { get; set; } = -1f;
        
        // ユニットの属性
        public PPTypeAttribute TypeAttribute { get; }

        public PPBattleUnit(string aUnitId, string aDisplayName, ParameterSet aParameterSet,
            PPParameterSet aExtraParameterSet, PPTypeAttribute aTypeAttribute)  : base(aUnitId, aDisplayName, aParameterSet)
        {
            ExtraParameters = aExtraParameterSet;
            TypeAttribute = aTypeAttribute;
        }

        public bool CanValidateSkill(BattleContext aContext)
        {
            foreach (var skill in Skills)
            {
                var result = aContext.Rules.CastValidator.Validate(this, skill, aContext);
                if (result.CanCast)
                    return true;
            }

            return false;
        }

        public Parameter ResolveParameter(string aId)
        {
            if(Parameters.Parameters.TryGetValue(aId, out var paramDef))
            {
                return paramDef;
            }
            
            if (ExtraParameters.Parameters.TryGetValue(aId, out var paramEx))
            {
                return paramEx;
            }
            
            return null;
        }
    }
}