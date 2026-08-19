/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPReflectionAccessor.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief オブジェクトのフィールド・プロパティへリフレクション経由でアクセスする共通処理
 * get_field/set_fieldツールおよびPlanExecutorのWaitUntil/Assert条件評価から共用する
 * =====================================*/

using System;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace MCPBridge.Editor.Tools
{
    public static class MCPReflectionAccessor
    {
        private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // aTargetのaMemberName(フィールドまたはプロパティ)の値を取得する
        public static object GetValue(object aTarget, string aMemberName)
        {
            var type = aTarget.GetType();

            var field = type.GetField(aMemberName, InstanceFlags);
            if (field != null)
            {
                return field.GetValue(aTarget);
            }

            var property = type.GetProperty(aMemberName, InstanceFlags);
            if (property != null)
            {
                return property.GetValue(aTarget);
            }

            throw new InvalidOperationException($"フィールド/プロパティが見つかりません: {aMemberName}");
        }

        // aTargetのaMemberName(フィールドまたはプロパティ)にaValueを書き込む
        public static void SetValue(object aTarget, string aMemberName, JToken aValue)
        {
            var type = aTarget.GetType();

            var field = type.GetField(aMemberName, InstanceFlags);
            if (field != null)
            {
                field.SetValue(aTarget, MCPArgumentConverter.ConvertValue(aValue, field.FieldType));
                return;
            }

            var property = type.GetProperty(aMemberName, InstanceFlags);
            if (property != null && property.CanWrite)
            {
                property.SetValue(aTarget, MCPArgumentConverter.ConvertValue(aValue, property.PropertyType));
                return;
            }

            throw new InvalidOperationException($"書き込み可能なフィールド/プロパティが見つかりません: {aMemberName}");
        }
    }
}
