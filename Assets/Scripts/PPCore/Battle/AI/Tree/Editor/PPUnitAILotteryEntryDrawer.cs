/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAILotteryEntryDrawer.cs
 * @author hqrse
 * @date 2026/08/26
 * @brief 抽選ノードの枝を「子ノード名 : 重み」で表示するインスペクタ拡張
 * =====================================*/

using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // 抽選ノードの枝 1 本を「子ノード名 ： 重み」の 1 行で描く
    //
    // 枝は接続先をノード ID で持つが、ID は自動採番の GUID なのでそのまま出しても読めない
    // プロファイル本体から ID を引き直してノード名を表示することで、
    // どの枝に何割振っているかをインスペクタ上で見比べられるようにしている
    [CustomPropertyDrawer(typeof(PPUnitAILotteryEntry))]
    public class PPUnitAILotteryEntryDrawer : PropertyDrawer
    {
        // 重み入力欄の幅
        private const float WeightWidth = 60f;
        // ノード名と重み入力欄のあいだの余白
        private const float Spacing = 6f;

        public override void OnGUI(Rect aPosition, SerializedProperty aProperty, GUIContent aLabel)
        {
            var childIdProperty = aProperty.FindPropertyRelative("mChildId");
            var weightProperty = aProperty.FindPropertyRelative("mWeight");
            if (childIdProperty == null || weightProperty == null)
            {
                EditorGUI.PropertyField(aPosition, aProperty, aLabel, true);
                return;
            }

            var nameRect = new Rect(aPosition.x, aPosition.y,
                aPosition.width - WeightWidth - Spacing, EditorGUIUtility.singleLineHeight);
            var weightRect = new Rect(aPosition.xMax - WeightWidth, aPosition.y,
                WeightWidth, EditorGUIUtility.singleLineHeight);

            EditorGUI.LabelField(nameRect, ResolveChildName(aProperty, childIdProperty.stringValue));

            // 重みだけを編集させる。接続先はグラフ上の接続で決まるため、ここでは触らせない
            EditorGUI.BeginChangeCheck();
            float weight = EditorGUI.FloatField(weightRect, weightProperty.floatValue);
            if (EditorGUI.EndChangeCheck())
            {
                weightProperty.floatValue = Mathf.Max(0f, weight);
            }
        }

        public override float GetPropertyHeight(SerializedProperty aProperty, GUIContent aLabel)
            => EditorGUIUtility.singleLineHeight;

        // 接続先の子ノード名を引く
        // 未接続や、削除済みノードを指している場合は分かる表記へ落とす
        // aProperty : 対象の枝プロパティ
        // aChildId : 接続先のノード ID
        // return : 表示するノード名
        private static string ResolveChildName(SerializedProperty aProperty, string aChildId)
        {
            if (string.IsNullOrEmpty(aChildId)) return "(未接続)";

            if (aProperty.serializedObject.targetObject is not PPUnitAIProfileDefinition profile)
            {
                return aChildId;
            }

            var node = profile.FindNode(aChildId);
            return node != null ? node.NodeName : "(削除されたノード)";
        }
    }
}
