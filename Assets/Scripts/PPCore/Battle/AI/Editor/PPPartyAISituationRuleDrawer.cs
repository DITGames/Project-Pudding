/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyAISituationRuleDrawer.cs
 * @author hqrse
 * @date 2026/07/23
 * @brief PPPartyAISituationRule.Conditionsの+をツリーピッカーに差し替える
 * =====================================*/
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace PPCore
{
    [CustomPropertyDrawer(typeof(PPPartyAISituationRule))]
    public class PPPartyAISituationRuleDrawer : PropertyDrawer
    {
        private readonly Dictionary<string, ReorderableList> mListCache = new();
        private readonly Dictionary<string, Rect> mAddButtonRectCache = new();

        public override void OnGUI(Rect aPosition, SerializedProperty aProperty, GUIContent aLabel)
        {
            var nameProp = aProperty.FindPropertyRelative("Name");
            var conditionsProp = aProperty.FindPropertyRelative("Conditions");
            var scoreProp = aProperty.FindPropertyRelative("Score");
            string key = aProperty.propertyPath;

            var list = GetOrCreateList(key, conditionsProp);

            EditorGUI.BeginProperty(aPosition, aLabel, aProperty);

            var r = new Rect(aPosition.x, aPosition.y, aPosition.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(r, nameProp);
            r.y += EditorGUIUtility.singleLineHeight + 2f;

            var listRect = new Rect(r.x, r.y, r.width, list.GetHeight());
            // フッターの「＋」ボタン位置を概算し、ポップアップのアンカーに使う
            mAddButtonRectCache[key] = new Rect(listRect.xMax - 30f, listRect.yMax - list.footerHeight, 26f, list.footerHeight);
            list.DoList(listRect);
            r.y += listRect.height + 4f;

            var coeffHeight = EditorGUI.GetPropertyHeight(scoreProp, true);
            EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, coeffHeight), scoreProp, true);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty aProperty, GUIContent aLabel)
        {
            var conditionsProp = aProperty.FindPropertyRelative("Conditions");
            var scoreProp = aProperty.FindPropertyRelative("Score");
            var list = GetOrCreateList(aProperty.propertyPath, conditionsProp);

            return EditorGUIUtility.singleLineHeight + 2f
                 + list.GetHeight() + 4f
                 + EditorGUI.GetPropertyHeight(scoreProp, true);
        }

        private ReorderableList GetOrCreateList(string aKey, SerializedProperty aConditionsProp)
        {
            if (mListCache.TryGetValue(aKey, out var cached))
            {
                cached.serializedProperty = aConditionsProp;
                return cached;
            }

            var list = new ReorderableList(aConditionsProp.serializedObject, aConditionsProp,
                true, true, true, true);

            list.drawHeaderCallback = r => EditorGUI.LabelField(r, "条件(すべて満たすと成立)");

            list.elementHeightCallback = i =>
            {
                var elem = aConditionsProp.GetArrayElementAtIndex(i);
                return EditorGUI.GetPropertyHeight(elem, true) + 4f;
            };

            list.drawElementCallback = (r, i, active, focused) =>
            {
                var elem = aConditionsProp.GetArrayElementAtIndex(i);
                r.y += 2f;
                r.height = EditorGUI.GetPropertyHeight(elem, true);
                // PPPartyConditionValidatorDrawer(既存)がそのまま効き、Descriptionが表示される
                EditorGUI.PropertyField(r, elem, GUIContent.none, true);
            };

            list.onAddCallback = rl =>
            {
                var anchor = mAddButtonRectCache.TryGetValue(aKey, out var rect) ? rect : new Rect(0, 0, 1, 1);

                PPConditionPickerPopup.Show(anchor, selectedType =>
                {
                    var asset = PPConditionAssetFactory.CreateAndSave(selectedType);
                    if (asset == null) return;

                    int index = aConditionsProp.arraySize;
                    aConditionsProp.arraySize++;
                    aConditionsProp.GetArrayElementAtIndex(index).objectReferenceValue = asset;
                    aConditionsProp.serializedObject.ApplyModifiedProperties();
                });
            };

            mListCache[aKey] = list;
            return list;
        }
    }
}