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

namespace AttributeUtility
{
    // EditConditionAttribute の描画を担う PropertyDrawer
    // 条件メンバーの値をリフレクションで読み取り、条件を満たさない場合は
    // 属性の設定に応じてグレーアウトさせるか完全に隠す
    // 条件メンバーは SerializedProperty からは辿れないため、
    // プロパティパスを解析して実インスタンスを取得している
    [CustomPropertyDrawer(typeof(EditConditionAttribute))]
    public class EditConditionDrawer : PropertyDrawer
    {
        // 条件メンバーの探索に使うバインディングフラグ。private メンバーも対象にする
        private const BindingFlags MemberFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // 描画に必要な高さを返す。非表示条件を満たす場合は高さを消す
        // property : 対象プロパティ
        // label : ラベル
        // return : 描画に必要な高さ。非表示時は負値
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

        // プロパティを描画する。条件を満たさない場合、Hides なら描画自体を行わず、
        // そうでなければ GUI を無効化してグレーアウト表示にする
        // position : 描画領域
        // property : 対象プロパティ
        // label : ラベル
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var editConditionAttr = (EditConditionAttribute)attribute;
            bool conditionMet = EvaluateCondition(editConditionAttr, property);

            if (!conditionMet && editConditionAttr.Hides)
            {
                return;
            }

            // 他のプロパティ描画へ影響させないよう、変更前の状態へ必ず戻す
            bool prevEnabled = GUI.enabled;
            if (!conditionMet)
            {
                GUI.enabled = false;
            }

            EditorGUI.PropertyField(position, property, label, true);

            GUI.enabled = prevEnabled;
        }

        // 条件メンバーを評価する
        // フィールド → プロパティ → 引数なしメソッドの順に探し、基底クラスへも遡る
        // 見つからない場合は警告を出したうえで「表示する」側へフォールバックする
        // （条件名の打ち間違いでプロパティが消えて気付けなくなるのを避けるため）
        // attr : 評価する属性
        // property : 対象プロパティ
        // return : 条件を満たす場合 true
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

        // SerializedPropertyが実際に属するオブジェクトインスタンスを反射で取得する（ネスト対応）
        // プロパティパスを "." で分割し、最後の 1 要素（プロパティ自身）を除いた分だけ辿ることで
        // 「そのプロパティを保持しているオブジェクト」に到達する
        // 配列要素は "xxx.Array.data[n]" という形式になるため、事前に "xxx[n]" へ正規化している
        // property : 対象プロパティ
        // return : プロパティを保持するオブジェクト。辿れない場合は null
        private static object GetParentObject(SerializedProperty property)
        {
            var path = property.propertyPath.Replace(".Array.data[", "[");
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

        // 名前でフィールドまたはプロパティの値をリフレクションで取得する。基底クラスへも遡る
        // source : 取得元オブジェクト
        // name : メンバー名
        // return : 取得した値。見つからない場合は null
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

        // コレクション型メンバーの指定インデックスの要素を取得する
        // 添字アクセスを持たない System.Collections.IEnumerable にも対応するため、
        // 列挙子を目的の位置まで進める方式にしている
        // source : 取得元オブジェクト
        // name : コレクションのメンバー名
        // index : 取得したい要素の位置
        // return : 該当要素。範囲外またはコレクションでない場合は null
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
