/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPParameterSet.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief ユニットが持つパラメータのセット
 * =====================================*/
using System.Collections.Generic;
using CommandBattleCore;

namespace PPCore
{
    public class PPParameterSet
    {
        public static readonly string ParameterIdAttackCost = "AttackCost";
        
        public Parameter AttackCost { get; }
        
        protected readonly Dictionary<string, Parameter> mParameters = new();

        public PPParameterSet(float aAttackCost)
        {
            AttackCost = RegisterModifiable(ParameterIdAttackCost, new Parameter(aAttackCost));
        }

        protected Parameter RegisterModifiable(string aKey, Parameter aParameter)
        {
            mParameters[aKey] = aParameter;
            return aParameter;
        }

        public Parameter Get(string aKey)
        {
            return mParameters.TryGetValue(aKey, out Parameter aParameter) ? aParameter : null;
        }
        
        public IReadOnlyDictionary<string, Parameter> Parameters => mParameters;

        public void RemoveModifiesFromSource(object aSource)
        {
            foreach (var param in mParameters.Values)
            {
                param.RemoveModifiersFromSource(aSource);
            }
        }
    }
}