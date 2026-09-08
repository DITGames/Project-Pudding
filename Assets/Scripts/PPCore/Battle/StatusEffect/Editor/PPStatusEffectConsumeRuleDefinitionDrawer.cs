/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStatusEffectConsumeRuleDefinitionDrawer.cs
 * @author hqrse
 * @date 2026/09/04
 * @brief PPStatusEffectConsumeRuleDefinition の [SerializeReference] フィールド用インスペクタ拡張
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // PPStatusEffectConsumeRuleDefinition 型のフィールドを、
    // 型未選択ならツリーポップアップを開く選択ボタン、選択済みなら BuildString() をラベルにしてフィールド展開する
    // 単一フィールドは「要素を消して選び直す」ができないため、選択済みでも種類を切り替えるボタンを出す
    [CustomPropertyDrawer(typeof(PPStatusEffectConsumeRuleDefinition), true)]
    public class PPStatusEffectConsumeRuleDefinitionDrawer : PropertyDrawer
    {
        // 種類切り替えボタンの幅
        private const float SwitchButtonWidth = 80f;
        // 表示名の既定値。フィールド側に [PPFieldLabel] があればそちらを優先する
        private const string DefaultDisplayLabel = "スタック消費ルール";

        public override void OnGUI(Rect aPosition, SerializedProperty aProperty, GUIContent aLabel)
        {
            string displayLabel = ResolveDisplayLabel();
            var label = new GUIContent(displayLabel, aLabel.image, aLabel.tooltip);

            if (string.IsNullOrEmpty(aProperty.managedReferenceFullTypename))
            {
                var buttonRect = new Rect(aPosition.x, aPosition.y, aPosition.width, EditorGUIUtility.singleLineHeight);
                if (GUI.Button(buttonRect, $"+ {label.text} を選択"))
                {
                    ShowTypePicker(buttonRect, aProperty);
                }
                return;
            }

            // 展開しなくても設定内容が分かるよう要約を添える
            if (aProperty.managedReferenceValue is PPStatusEffectConsumeRuleDefinition assigned)
            {
                label.text = $"{displayLabel}：{assigned.BuildString()}";
            }

            var fieldRect = new Rect(aPosition.x, aPosition.y, aPosition.width - SwitchButtonWidth, aPosition.height);
            var switchRect = new Rect(aPosition.xMax - SwitchButtonWidth, aPosition.y,
                SwitchButtonWidth, EditorGUIUtility.singleLineHeight);

            PPManagedReferencePickerUtility.DrawAssignedField(fieldRect, aProperty, label);
            if (GUI.Button(switchRect, "種類を変更"))
            {
                ShowTypePicker(switchRect, aProperty);
            }
        }

        public override float GetPropertyHeight(SerializedProperty aProperty, GUIContent aLabel)
            => PPManagedReferencePickerUtility.GetPropertyHeight(aProperty);

        // 描画に使う表示名を解決する
        // return : 表示名
        private string ResolveDisplayLabel()
            => fieldInfo?.GetCustomAttribute<PPFieldLabelAttribute>()?.Text ?? DefaultDisplayLabel;

        // 型選択ツリーを開き、選ばれた型のインスタンスをプロパティへ設定する
        // aActivatorRect : ポップアップを出す基準の矩形
        // aProperty : 設定先の SerializeReference プロパティ
        private static void ShowTypePicker(Rect aActivatorRect, SerializedProperty aProperty)
        {
            // ポップアップのコールバックは非同期(フレームをまたぐ)ため、プロパティをコピーして保持する
            var propertyCopy = aProperty.Copy();
            PPTypeTreePickerPopup.Show(aActivatorRect, CollectCandidateTypes(), "(消費ルールが見つかりません)", type =>
            {
                propertyCopy.managedReferenceValue = CreateInstance(type);
                propertyCopy.serializedObject.ApplyModifiedProperties();
            });
        }

        // PPStatusEffectConsumeRuleDefinition の具象派生型を候補として集める
        // return : 候補として並べる型のリスト
        private static List<Type> CollectCandidateTypes()
            => PPTypeTreePickerTreeView.CollectDerived<PPStatusEffectConsumeRuleDefinition>(true);

        // 選ばれた型からインスタンスを組み立てる
        // aType : 選ばれた型
        // return : フィールドへ設定するインスタンス
        private static PPStatusEffectConsumeRuleDefinition CreateInstance(Type aType)
            => (PPStatusEffectConsumeRuleDefinition)Activator.CreateInstance(aType);
    }
}
