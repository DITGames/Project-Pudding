/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ParemterSet.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief ユニットが持つパラメータの定義
 * =====================================*/

using System.Collections.Generic;

namespace CommandBattleCore
{
    public class ParameterSet
    {
        public static readonly string ParamIdMaxHp = "MaxHP";
        public static readonly string ParamIdAttack = "Attack";
        public static readonly string ParamIdDefense = "Defense";
        public static readonly string ParamIdSpeed = "Speed";
        
        public ResourceParameter Hp { get; }
        public Parameter Attack { get; }
        public Parameter Defense { get; }
        public Parameter Speed { get; }
        
        protected readonly Dictionary<string, Parameter> mParameters = new();

        public ParameterSet(float aMaxHp, float aAttack, float aDefense, float aSpeed)
        {
            Hp = new ResourceParameter(aMaxHp);
            
            Attack = RegisterModifiable(ParamIdAttack, new Parameter(aAttack));
            Defense = RegisterModifiable(ParamIdDefense, new Parameter(aDefense));
            Speed = RegisterModifiable(ParamIdSpeed, new Parameter(aSpeed));

            RegisterModifiable(ParamIdMaxHp, Hp.Max);
        }

        // パラメータ登録のヘルパー
        protected Parameter RegisterModifiable(string aKey, Parameter aParameter)
        {
            mParameters[aKey] = aParameter;
            return aParameter;
        }

        // パラメータ取得
        public Parameter Get(string aKey)
        {
            return mParameters.TryGetValue(aKey, out var aParameter) ? aParameter : null;
        }
        
        public IReadOnlyDictionary<string, Parameter> Parameters => mParameters;

        // すべてのパラメータに対してSource関連のModifierを除去する
        public void RemoveModifiersFromSource(object aSource)
        {
            foreach (var param in mParameters.Values)
            {
                param.RemoveModifiersFromSource(aSource);
            }
        }
    }
}