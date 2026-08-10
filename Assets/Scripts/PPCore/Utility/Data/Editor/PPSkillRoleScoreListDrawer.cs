/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillRoleScoreListDrawer.cs
 * @author hqrse
 * @date 2026/08/07
 * @brief PPSkillRoleScoreList 用インスペクタ拡張。フラグに連動してスコア入力欄を出し分ける
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // PPSkillDefinition.mBattleSkillRole でチェックされているロールの分だけ、
    // 対応するスコア入力欄を動的に表示する PropertyDrawer
    // ロールは Enum.GetValues で走査するため、PPBattleSkillRole にロールが増えても
    // このクラスのコード修正は不要
    [CustomPropertyDrawer(typeof(PPSkillRoleScoreList))]
    public class PPSkillRoleScoreListDrawer : PropertyDrawer
    {
        private const string BattleSkillRoleFieldName = "mBattleSkillRole";

        // 立っているロールの分だけ、ラベル行 + スコア入力欄を縦に並べて描画する
        // aPosition : 描画領域
        // aProperty : 対象プロパティ（PPSkillRoleScoreList）
        // aLabel : ラベル
        public override void OnGUI(Rect aPosition, SerializedProperty aProperty, GUIContent aLabel)
        {
            EditorGUI.BeginProperty(aPosition, aLabel, aProperty);

            var lineHeight = EditorGUIUtility.singleLineHeight + 2f;
            var r = new Rect(aPosition.x, aPosition.y, aPosition.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(r, aLabel);
            r.y += lineHeight;

            var roles = VisibleRoles(aProperty);
            var entriesProp = aProperty.FindPropertyRelative("mEntries");

            if (roles.Count == 0)
            {
                EditorGUI.LabelField(r, " ", "(ロール未選択。カテゴリで先にロールをチェックしてください)");
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;
            foreach (var role in roles)
            {
                int index = FindOrAppendIndex(entriesProp, role);
                var valueProp = entriesProp.GetArrayElementAtIndex(index).FindPropertyRelative("Value");
                EditorGUI.PropertyField(r, valueProp, new GUIContent(ObjectNames.NicifyVariableName(role.ToString())));
                r.y += lineHeight;
            }
            EditorGUI.indentLevel--;

            EditorGUI.EndProperty();
        }

        // 描画に必要な高さを返す。表示行数（立っているロール数 + ラベル行）から求める
        // aProperty : 対象プロパティ
        // aLabel : ラベル
        public override float GetPropertyHeight(SerializedProperty aProperty, GUIContent aLabel)
        {
            var lineHeight = EditorGUIUtility.singleLineHeight + 2f;
            int rows = Mathf.Max(1, VisibleRoles(aProperty).Count);
            return lineHeight * (rows + 1);
        }

        // 対象スキルの mBattleSkillRole を読み、立っているフラグ（None を除く）を宣言順で返す
        // aProperty : PPSkillRoleScoreList のプロパティ。同じオブジェクトの兄弟フィールドとして
        //             mBattleSkillRole を持つ PPSkillDefinition から辿る
        private static List<PPBattleSkillRole> VisibleRoles(SerializedProperty aProperty)
        {
            var result = new List<PPBattleSkillRole>();
            var roleProp = aProperty.serializedObject.FindProperty(BattleSkillRoleFieldName);
            if (roleProp == null)
                return result;

            // Flags enum のビット値は enumValueIndex ではなく intValue で読み書きする
            var flags = (PPBattleSkillRole)roleProp.intValue;
            foreach (PPBattleSkillRole role in Enum.GetValues(typeof(PPBattleSkillRole)))
            {
                if (role == PPBattleSkillRole.None) continue;
                if ((flags & role) != 0) result.Add(role);
            }
            return result;
        }

        // 指定ロールのエントリを探す。無ければ末尾に追加してそのインデックスを返す
        // フラグを一時的に外して戻しても、既存の入力値が消えないようにするため
        // 該当が無い場合以外はエントリを削除しない
        // aEntriesProp : mEntries 配列のプロパティ
        // aRole : 探すロール
        private static int FindOrAppendIndex(SerializedProperty aEntriesProp, PPBattleSkillRole aRole)
        {
            for (int i = 0; i < aEntriesProp.arraySize; i++)
            {
                var roleProp = aEntriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("Role");
                if ((PPBattleSkillRole)roleProp.intValue == aRole)
                    return i;
            }

            int newIndex = aEntriesProp.arraySize;
            aEntriesProp.arraySize++;
            var newEntry = aEntriesProp.GetArrayElementAtIndex(newIndex);
            newEntry.FindPropertyRelative("Role").intValue = (int)aRole;
            newEntry.FindPropertyRelative("Value").floatValue = 0f;
            return newIndex;
        }
    }
}
