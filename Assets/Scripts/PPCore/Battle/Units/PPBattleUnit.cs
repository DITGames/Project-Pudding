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

        public PPBattleUnit(string aUnitId, string aDisplayName, ParameterSet aParameterSet,
            PPParameterSet aPPParameterSet)  : base(aUnitId, aDisplayName, aParameterSet)
        {
            PPParameters = aPPParameterSet;
        }
    }
}