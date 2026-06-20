/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ParameterModifier.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief パラメータ修飾子定義
 * =====================================*/

namespace CommandBattleCore
{
    public enum ParameterModifierType
    {
        Add,
        Multiply,
        Override,
    }

    public sealed class ParameterModifier
    {
        public ParameterModifierType Type { get; }
        public float Value { get; }
        public object Source { get; }
        public int Priority { get; }

        public ParameterModifier(ParameterModifierType aType, object aSource, float aValue, int aPriority = 0)
        {
            Type = aType;
            Source = aSource;
            Value = aValue;
            Priority = aPriority;
        }
    }
}