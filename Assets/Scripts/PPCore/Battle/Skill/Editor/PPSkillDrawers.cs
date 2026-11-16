/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPSkillDrawers.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief スキルタグ・スキル効果定義の PropertyDrawer
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // PPSkillTagDefinition の参照フィールド・リスト要素を、タグ名を表示するボタンとして描画する
    // 押すとツリーポップアップで選ばせる
    [CustomPropertyDrawer(typeof(PPSkillTagDefinition))]
    public class PPSkillTagDrawer : PropertyDrawer
    {
        // 解除ボタンの幅
        private const float ClearButtonWidth = 22f;

        public override void OnGUI(Rect aPosition, SerializedProperty aProperty, GUIContent aLabel)
        {
            var tag = aProperty.objectReferenceValue as PPSkillTagDefinition;

            var contentRect = EditorGUI.PrefixLabel(aPosition, aLabel);
            var buttonRect = new Rect(contentRect.x, contentRect.y,
                contentRect.width - ClearButtonWidth - 2f, EditorGUIUtility.singleLineHeight);
            var clearRect = new Rect(contentRect.xMax - ClearButtonWidth, contentRect.y,
                ClearButtonWidth, EditorGUIUtility.singleLineHeight);

            if (GUI.Button(buttonRect, tag != null ? tag.MenuPath : "(タグ未選択)", EditorStyles.popup))
            {
                // ポップアップのコールバックは非同期(フレームをまたぐ)ため、プロパティをコピーして保持する
                var propertyCopy = aProperty.Copy();
                PPAssetTreePickerPopup.ShowAssets<PPSkillTagDefinition>(buttonRect,
                    t => (t.CategoryPath, t.TagName), "(スキルタグアセットが見つかりません)", picked =>
                {
                    propertyCopy.objectReferenceValue = picked;
                    propertyCopy.serializedObject.ApplyModifiedProperties();
                });
            }

            // 選択済みのときだけ解除ボタンを操作可能にする
            using (new EditorGUI.DisabledScope(tag == null))
            {
                if (GUI.Button(clearRect, "×"))
                {
                    aProperty.objectReferenceValue = null;
                }
            }
        }

        public override float GetPropertyHeight(SerializedProperty aProperty, GUIContent aLabel)
            => EditorGUIUtility.singleLineHeight;
    }

    // PPSkillEffectDefinition 型のフィールド・リスト要素を、
    // 型未選択ならツリーポップアップを開く選択ボタン、選択済みなら BuildString() をラベルにしてフィールド展開する
    [CustomPropertyDrawer(typeof(PPSkillEffectDefinition), true)]
    public class PPSkillEffectDefinitionDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect aPosition, SerializedProperty aProperty, GUIContent aLabel)
        {
            if (string.IsNullOrEmpty(aProperty.managedReferenceFullTypename))
            {
                var buttonRect = new Rect(aPosition.x, aPosition.y, aPosition.width, EditorGUIUtility.singleLineHeight);
                if (GUI.Button(buttonRect, $"+ {aLabel.text} を選択"))
                {
                    // ポップアップのコールバックは非同期(フレームをまたぐ)ため、プロパティをコピーして保持する
                    var propertyCopy = aProperty.Copy();
                    PPTypeTreePickerPopup.Show(buttonRect, CollectCandidateTypes(), "(エフェクトが見つかりません)", type =>
                    {
                        propertyCopy.managedReferenceValue = CreateInstance(type);
                        propertyCopy.serializedObject.ApplyModifiedProperties();
                    });
                }
                return;
            }

            PPManagedReferencePickerUtility.DrawAssignedField(aPosition, aProperty, aLabel);
        }

        public override float GetPropertyHeight(SerializedProperty aProperty, GUIContent aLabel)
            => PPManagedReferencePickerUtility.GetPropertyHeight(aProperty);

        // PPSkillEffectDefinition の具象派生型を候補として集める
        // return : 候補として並べる型のリスト
        private static List<Type> CollectCandidateTypes()
            => PPTypeTreePickerTreeView.CollectDerived<PPSkillEffectDefinition>(true);

        // 選ばれた型からインスタンスを組み立てる
        private static PPSkillEffectDefinition CreateInstance(Type aType)
            => (PPSkillEffectDefinition)Activator.CreateInstance(aType);
    }
}
