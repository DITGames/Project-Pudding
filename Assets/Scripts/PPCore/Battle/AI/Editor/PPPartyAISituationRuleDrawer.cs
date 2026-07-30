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
    // 状況ルールを「ルール名 → 条件リスト → 成立時スコア」の順で描画する PropertyDrawer
    // 主目的は条件リストの「＋」ボタンの差し替え。標準の挙動では空要素が追加されるだけで、
    // 別途アセットを作って割り当てる必要がある。ここを PPConditionPickerPopup に繋ぎ替えることで、
    // 押した流れのまま条件を選んで（必要ならアセット生成まで済ませて）追加できる
    // ReorderableList は生成コストが高く状態も持つため、
    // プロパティパスをキーにキャッシュして描画のたびに作り直さないようにしている
    [CustomPropertyDrawer(typeof(PPPartyAISituationRule))]
    public class PPPartyAISituationRuleDrawer : PropertyDrawer
    {
        // プロパティパスごとのリストのキャッシュ
        private readonly Dictionary<string, ReorderableList> mListCache = new();
        // ポップアップを出す基準となる「＋」ボタンの矩形のキャッシュ
        private readonly Dictionary<string, Rect> mAddButtonRectCache = new();

        // ルール名・条件リスト・成立時スコアを縦に並べて描画する
        // aPosition : 描画領域
        // aProperty : 対象プロパティ
        // aLabel : ラベル
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

        // 3 つの要素の高さを合算して返す。条件リストは件数で高さが変わるため実測値を使う
        // aProperty : 対象プロパティ
        // aLabel : ラベル
        // return : 描画に必要な高さ
        public override float GetPropertyHeight(SerializedProperty aProperty, GUIContent aLabel)
        {
            var conditionsProp = aProperty.FindPropertyRelative("Conditions");
            var scoreProp = aProperty.FindPropertyRelative("Score");
            var list = GetOrCreateList(aProperty.propertyPath, conditionsProp);

            return EditorGUIUtility.singleLineHeight + 2f
                 + list.GetHeight() + 4f
                 + EditorGUI.GetPropertyHeight(scoreProp, true);
        }

        // 条件リスト用の ReorderableList を取得する
        // キャッシュがあれば対象プロパティだけ差し替えて使い回す
        // （SerializedProperty は描画のたびに作り直されるため、参照の更新が要る）
        // aKey : キャッシュのキーとなるプロパティパス
        // aConditionsProp : 条件リストのプロパティ
        // return : 設定済みのリスト
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

            // 標準の「空要素を追加」ではなく、条件ピッカーを開いて選ばせる
            list.onAddCallback = rl =>
            {
                var anchor = mAddButtonRectCache.TryGetValue(aKey, out var rect) ? rect : new Rect(0, 0, 1, 1);

                PPConditionPickerPopup.Show(anchor, asset =>
                {
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
