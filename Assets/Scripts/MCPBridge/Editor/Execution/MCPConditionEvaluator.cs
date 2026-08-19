/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPConditionEvaluator.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief WaitUntil/Assertステップの条件式を評価する
 * 条件式の形式: {objectPath, componentType, field, operator, value}
 * operatorは "==" / "!=" / ">" / ">=" / "<" / "<=" に対応する
 * =====================================*/

using System;
using MCPBridge.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace MCPBridge.Editor.Execution
{
    public static class MCPConditionEvaluator
    {
        // 数値同士の"=="/"!="判定で許容する誤差(float⇔double変換の丸め誤差を吸収する)
        private const double Epsilon = 1e-4;

        public static bool Evaluate(JObject aCondition)
        {
            var target = MCPSceneObjectResolver.ResolveComponentOrGameObject(
                aCondition.Value<string>("objectPath"),
                aCondition.Value<string>("componentType"));

            var actual = MCPReflectionAccessor.GetValue(target, aCondition.Value<string>("field"));
            var expected = aCondition["value"];
            var op = aCondition.Value<string>("operator") ?? "==";

            return Compare(actual, expected, op);
        }

        private static bool Compare(object aActual, JToken aExpected, string aOperator)
        {
            if (aOperator == "==" || aOperator == "!=")
            {
                var equal = ValuesEqual(MCPArgumentConverter.ToJToken(aActual), aExpected);
                return aOperator == "==" ? equal : !equal;
            }

            var actualNumber = Convert.ToDouble(aActual);
            var expectedNumber = aExpected.Value<double>();
            return aOperator switch
            {
                ">" => actualNumber > expectedNumber,
                ">=" => actualNumber >= expectedNumber,
                "<" => actualNumber < expectedNumber,
                "<=" => actualNumber <= expectedNumber,
                _ => throw new NotSupportedException($"未対応の演算子です: {aOperator}"),
            };
        }

        // Vector3等(JObject)のx/y/z個々の数値要素までepsilon付きで再帰的に比較する。
        // 数値以外(文字列等)は従来通りJToken.DeepEqualsに委ねる
        private static bool ValuesEqual(JToken aActualToken, JToken aExpectedToken)
        {
            if (IsNumericToken(aActualToken) && IsNumericToken(aExpectedToken))
            {
                return Math.Abs(aActualToken.Value<double>() - aExpectedToken.Value<double>()) < Epsilon;
            }

            if (aActualToken.Type == JTokenType.Object && aExpectedToken.Type == JTokenType.Object)
            {
                var actualObj = (JObject)aActualToken;
                var expectedObj = (JObject)aExpectedToken;
                if (actualObj.Count != expectedObj.Count)
                {
                    return false;
                }
                foreach (var property in expectedObj.Properties())
                {
                    var actualValue = actualObj[property.Name];
                    if (actualValue == null || !ValuesEqual(actualValue, property.Value))
                    {
                        return false;
                    }
                }
                return true;
            }

            return JToken.DeepEquals(aActualToken, aExpectedToken);
        }

        private static bool IsNumericToken(JToken aToken)
        {
            return aToken.Type == JTokenType.Integer || aToken.Type == JTokenType.Float;
        }
    }
}
