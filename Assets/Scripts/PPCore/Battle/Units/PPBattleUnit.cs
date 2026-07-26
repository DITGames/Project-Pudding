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
        public PPParameterSet PPParameters { get; }
        
        public PPUnitRole AssignedRole { get; set; } = PPUnitRole.Inherit;
        public PPUnitActionScoreModifier ScoreModifier { get; set; } = new PPUnitActionScoreModifier();
        
        // -1はパーティのIntelligenceを継承
        public float Intelligence { get; set; } = -1f;

        public PPBattleUnit(string aUnitId, string aDisplayName, ParameterSet aParameterSet,
            PPParameterSet aPPParameterSet)  : base(aUnitId, aDisplayName, aParameterSet)
        {
            PPParameters = aPPParameterSet;
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
    }
}