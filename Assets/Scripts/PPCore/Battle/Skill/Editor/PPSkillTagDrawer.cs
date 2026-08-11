/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillTagDrawer.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief スキルタグ参照フィールド用インスペクタ拡張
 * =====================================*/

using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // PPSkillTagDefinition の参照フィールド・リスト要素を、
    // タグ名を表示するボタンとして描画し、押すとツリーポップアップで選ばせる
    // 素のオブジェクトフィールドだと全アセットが平坦に並んで探しづらいため、
    // 条件クラスの選択と同じツリー操作に揃えている
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
}
