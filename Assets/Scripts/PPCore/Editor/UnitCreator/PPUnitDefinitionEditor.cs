/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitDefinitionEditor.cs
 * @author hqrse
 * @date 2026/09/26
 * @brief ユニット定義の基礎ステータスを本作の能力名で表示するインスペクタ
 * =====================================*/

using System.Collections.Generic;
using System.Reflection;
using AttributeUtility;
using CommandBattleCore;
using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // PPUnitDefinition のインスペクタ
    // 基底の UnitDefinition が持つ基礎ステータス（CommandBattleCore の StatBlock。Core 側のラベルは 最大HP / 攻撃力 / …）を、
    // 本作の基礎能力名（HP / ちから / まもり / はやさ）で表示する。それ以外のフィールドは既定の描画のまま
    // StatBlock 型の PropertyDrawer では実現できない点に注意：mBaseStatBlock には [Label] が付いており、
    // Unity は属性のドロワーを型のドロワーより優先するため、型のドロワーが一度も呼ばれない
    // このインスペクタは PPUnitDefinition（と派生）にだけ適用されるため、Core の UnitDefinition など他の型の表示は変わらない
    [CustomEditor(typeof(PPUnitDefinition), true)]
    [CanEditMultipleObjects]
    public class PPUnitDefinitionEditor : UnityEditor.Editor
    {
        // 能力名で表示し直す基礎ステータスのフィールド名（シリアライズ名）
        private const string BaseStatBlockPath = "mBaseStatBlock";
        // スクリプト参照の既定フィールド名。既定のインスペクタと同じく編集不可で表示する
        private const string ScriptPath = "m_Script";

        // StatBlock のフィールド名 → 本作の能力名
        private static readonly Dictionary<string, string> mAbilityNames = new()
        {
            { nameof(StatBlock.MaxHP), PPUnitAbilityDefinition.NameHp },
            { nameof(StatBlock.Attack), PPUnitAbilityDefinition.NameStrength },
            { nameof(StatBlock.Defense), PPUnitAbilityDefinition.NameGuard },
            { nameof(StatBlock.Speed), PPUnitAbilityDefinition.NameAgility },
        };

        // 基礎ステータスのフィールド定義。見出し（[Header]）と表示名（[Label]）を既定の描画と揃えるために読む
        private static readonly FieldInfo mBaseStatBlockField =
            typeof(UnitDefinition).GetField(BaseStatBlockPath, BindingFlags.Instance | BindingFlags.NonPublic);

        // 既定のインスペクタと同じ順にフィールドを並べ、基礎ステータスだけを能力名で描画する
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var property = serializedObject.GetIterator();
            for (var enter = true; property.NextVisible(enter); enter = false)
            {
                if (property.propertyPath == ScriptPath)
                {
                    using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(property, true);
                }
                else if (property.propertyPath == BaseStatBlockPath && mBaseStatBlockField != null)
                {
                    DrawBaseStatBlock(property);
                }
                else
                {
                    EditorGUILayout.PropertyField(property, true);
                }
            }
            serializedObject.ApplyModifiedProperties();
        }

        // 基礎ステータスを「見出し → 折りたたみ → 各能力」の順で描画する
        // 通常の PropertyField を通すと子の [Label]（Core 側の表示名）が優先されるため、見出しと折りたたみもここで描く
        // aProperty : mBaseStatBlock のプロパティ
        private static void DrawBaseStatBlock(SerializedProperty aProperty)
        {
            DrawDecorators();

            var labelAttribute = mBaseStatBlockField.GetCustomAttribute<LabelAttribute>(true);
            var label = new GUIContent(labelAttribute?.Text ?? aProperty.displayName, aProperty.tooltip);
            var rect = EditorGUILayout.GetControlRect();
            label = EditorGUI.BeginProperty(rect, label, aProperty);
            aProperty.isExpanded = EditorGUI.Foldout(rect, aProperty.isExpanded, label, true);
            EditorGUI.EndProperty();
            if (!aProperty.isExpanded) return;

            EditorGUI.indentLevel++;
            var end = aProperty.GetEndProperty();
            var child = aProperty.Copy();
            for (var enter = true; child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end); enter = false)
            {
                DrawAbility(child);
            }
            EditorGUI.indentLevel--;
        }

        // フィールドに付いた [Space] / [Header] を既定のデコレーターと同じ見た目で描画する
        private static void DrawDecorators()
        {
            foreach (var decorator in mBaseStatBlockField.GetCustomAttributes<PropertyAttribute>(true))
            {
                switch (decorator)
                {
                    case SpaceAttribute space:
                        EditorGUILayout.Space(space.height);
                        break;
                    case HeaderAttribute header:
                        var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 1.5f);
                        rect.yMin += EditorGUIUtility.singleLineHeight * 0.5f;
                        GUI.Label(EditorGUI.IndentedRect(rect), header.header, EditorStyles.boldLabel);
                        break;
                }
            }
        }

        // 能力 1 つ分を能力名のラベルで描画する
        // 対応表に無いフィールドや float 以外の型は、通常の描画（[Label] があればその表示名）に任せる
        // aProperty : StatBlock の子プロパティ
        private static void DrawAbility(SerializedProperty aProperty)
        {
            if (!mAbilityNames.TryGetValue(aProperty.name, out var abilityName)
                || aProperty.propertyType != SerializedPropertyType.Float)
            {
                EditorGUILayout.PropertyField(aProperty, true);
                return;
            }

            var rect = EditorGUILayout.GetControlRect();
            var label = EditorGUI.BeginProperty(rect, new GUIContent(abilityName, aProperty.tooltip), aProperty);
            EditorGUI.BeginChangeCheck();
            float value = EditorGUI.FloatField(rect, label, aProperty.floatValue);
            if (EditorGUI.EndChangeCheck())
            {
                aProperty.floatValue = value;
            }
            EditorGUI.EndProperty();
        }
    }
}
