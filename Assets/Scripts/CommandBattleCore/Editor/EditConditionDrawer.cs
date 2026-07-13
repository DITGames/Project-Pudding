/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file EditConditionDrawer.cs
 * @author hqrse
 * @date 2026/07/12
 * @brief EditConditionAttributeの表示クラス
 * =====================================*/
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CommandBattleCore
{
    [CustomPropertyDrawer(typeof(EditConditionAttribute))]
    public class EditConditionDrawer : PropertyDrawer
    {
        private const BindingFlags MemberFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var editConditionAttr = (EditConditionAttribute)attribute;
            bool conditionMet = EvaluateCondition(editConditionAttr, property);

            if (!conditionMet && editConditionAttr.Hides)
            {
                // Unity側の仕様で負の高さを返すと後続のスペーシングと相殺され、実質高さ0になる
                return -EditorGUIUtility.standardVerticalSpacing;
            }

            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var editConditionAttr = (EditConditionAttribute)attribute;
            bool conditionMet = EvaluateCondition(editConditionAttr, property);

            if (!conditionMet && editConditionAttr.Hides)
            {
                return;
            }

            bool prevEnabled = GUI.enabled;
            if (!conditionMet)
            {
                GUI.enabled = false;
            }

            EditorGUI.PropertyField(position, property, label, true);

            GUI.enabled = prevEnabled;
        }

        private static bool EvaluateCondition(EditConditionAttribute attr, SerializedProperty property)
        {
            object target = GetParentObject(property);
            if (target == null)
            {
                return true;
            }

            var type = target.GetType();

            while (type != null)
            {
                var field = type.GetField(attr.ConditionMember, MemberFlags);
                if (field != null && field.FieldType == typeof(bool))
                {
                    return (bool)field.GetValue(target) != attr.Negate;
                }

                var prop = type.GetProperty(attr.ConditionMember, MemberFlags);
                if (prop != null && prop.PropertyType == typeof(bool))
                {
                    return (bool)prop.GetValue(target, null) != attr.Negate;
                }

                var method = type.GetMethod(attr.ConditionMember, MemberFlags, null, System.Type.EmptyTypes, null);
                if (method != null && method.ReturnType == typeof(bool))
                {
                    return (bool)method.Invoke(target, null) != attr.Negate;
                }

                type = type.BaseType;
            }

            Debug.LogWarning($"[EditCondition] '{attr.ConditionMember}' が {target.GetType().Name} 内に見つかりません。表示状態にフォールバックします。");
            return true;
        }

        /// <summary>SerializedPropertyが実際に属するオブジェクトインスタンスを反射で取得する（ネスト対応）</summary>
        private static object GetParentObject(SerializedProperty property)
        {
            var path = property.propertyPath.Replace(".Array.Data[", "[");
            object obj = property.serializedObject.targetObject;
            var elements = path.Split('.');

            for (int i = 0; i < elements.Length - 1; i++)
            {
                var element = elements[i];
                if (element.Contains("["))
                {
                    var elementName = element.Substring(0, element.IndexOf("["));
                    var indexStr = element.Substring(element.IndexOf("["))
                        .Replace("[", "").Replace("]", "");
                    obj = GetIndexedValue(obj, elementName, int.Parse(indexStr));
                }
                else
                {
                    obj = GetMemberValue(obj, element);
                }
            }

            return obj;
        }

        private static object GetMemberValue(object source, string name)
        {
            if (source == null)
            {
                return null;
            }

            var type = source.GetType();
            while (type != null)
            {
                var field = type.GetField(name, MemberFlags);
                if (field != null)
                {
                    return field.GetValue(source);
                }

                var prop = type.GetProperty(name, MemberFlags);
                if (prop != null)
                {
                    return prop.GetValue(source, null);
                }

                type = type.BaseType;
            }

            return null;
        }

        private static object GetIndexedValue(object source, string name, int index)
        {
            if (GetMemberValue(source, name) is not System.Collections.IEnumerable enumerable)
            {
                return null;
            }

            var enumerator = enumerable.GetEnumerator();
            for (int i = 0; i <= index; i++)
            {
                if (!enumerator.MoveNext())
                {
                    return null;
                }
            }

            return enumerator.Current;
        }
    }
}
