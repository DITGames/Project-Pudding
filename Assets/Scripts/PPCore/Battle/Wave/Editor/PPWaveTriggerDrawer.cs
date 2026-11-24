/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPWaveTriggerDrawer.cs
 * @author hqrse
 * @date 2026/11/24
 * @brief ウェーブの段階が始まる条件を、インスペクタで種類を選んで足せるようにする
 * =====================================*/

using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // 条件が未選択なら種類を選ぶボタン、選択済みならその条件の項目を出す
    [CustomPropertyDrawer(typeof(PPWaveTrigger), true)]
    public class PPWaveTriggerDrawer : PropertyDrawer
    {
        /* 描画 */

        public override void OnGUI(Rect aPosition, SerializedProperty aProperty, GUIContent aLabel)
            => PPManagedReferencePickerUtility.OnGUI(aPosition, aProperty, GetLabel(aProperty, aLabel), typeof(PPWaveTrigger));

        public override float GetPropertyHeight(SerializedProperty aProperty, GUIContent aLabel)
            => PPManagedReferencePickerUtility.GetPropertyHeight(aProperty);

        /* ヘルパー */

        // 選んだ条件はその種類の名前を見出しにする
        private static GUIContent GetLabel(SerializedProperty aProperty, GUIContent aLabel)
        {
            var value = aProperty.managedReferenceValue;
            if (value == null) return new GUIContent("条件");
            var menuName = (PPTypeMenuNameAttribute)System.Attribute.GetCustomAttribute(value.GetType(), typeof(PPTypeMenuNameAttribute));
            return new GUIContent(menuName != null ? menuName.Path : aLabel.text);
        }
    }
}
