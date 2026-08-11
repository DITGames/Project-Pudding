/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillEffectDefinitionDrawer.cs
 * @author hqrse
 * @date 2026/08/06
 * @brief PPSkillEffectDefinition の [SerializeReference] フィールド用インスペクタ拡張
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PPCore
{
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

        // PPSkillEffectDefinition 派生と PPEffectDefinition 派生をまとめて 1 本のツリーに並べる
        // 2 つの異なる型階層を 1 回の選択で扱えるようにするための橋渡し
        // 属性の無い型（PPStatusApplySkillEffectDefinition のような内部用ラッパー）はツリーに出さない
        // return : 候補として並べる型のリスト
        private static List<Type> CollectCandidateTypes()
        {
            var list = PPTypeTreePickerTreeView.CollectDerived<PPSkillEffectDefinition>(true);
            PPTypeTreePickerTreeView.AppendDerived<PPEffectDefinition>(list, true);
            return list;
        }

        // 選ばれた型からインスタンスを組み立てる
        // PPEffectDefinition 派生（毒・パラメータ変動など）は単体では SkillEffect として扱えないため、
        // PPStatusApplySkillEffectDefinition でラップして「付与型 SkillEffect ＋ 中身」を一度に作る
        // aType : 選ばれた型
        // return : フィールドへ設定するインスタンス
        private static PPSkillEffectDefinition CreateInstance(Type aType)
            => typeof(PPEffectDefinition).IsAssignableFrom(aType)
                ? new PPStatusApplySkillEffectDefinition((PPEffectDefinition)Activator.CreateInstance(aType))
                : (PPSkillEffectDefinition)Activator.CreateInstance(aType);
    }
}
