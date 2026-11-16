/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitAITreeDrawers.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ビヘイビアツリーのノード・アクション・抽選エントリの PropertyDrawer
 * =====================================*/

using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // PPUnitAINode 型のフィールド・リスト要素を、
    // 型未選択ならツリーポップアップ（PPTypeTreePickerPopup）を開く選択ボタン、
    // 選択済みならノード名をラベルにしてフィールド展開する
    // PPPartyConditionValidatorDrawer と同じ形。選択済みの描画は PPManagedReferencePickerUtility に委譲する
    [CustomPropertyDrawer(typeof(PPUnitAINode), true)]
    public class PPUnitAINodeDrawer : PropertyDrawer
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
                    PPTypeTreePickerPopup.ShowDerived<PPUnitAINode>(buttonRect,
                        "(ノードクラスが見つかりません)", instance =>
                    {
                        propertyCopy.managedReferenceValue = instance;
                        propertyCopy.serializedObject.ApplyModifiedProperties();
                    });
                }
                return;
            }

            PPManagedReferencePickerUtility.DrawAssignedField(aPosition, aProperty, aLabel);
        }

        public override float GetPropertyHeight(SerializedProperty aProperty, GUIContent aLabel)
            => PPManagedReferencePickerUtility.GetPropertyHeight(aProperty);
    }

    // PPUnitAIActionBase 型のフィールドを、ノードと同じ作法で描画する
    [CustomPropertyDrawer(typeof(PPUnitAIActionBase), true)]
    public class PPUnitAIActionDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect aPosition, SerializedProperty aProperty, GUIContent aLabel)
        {
            if (string.IsNullOrEmpty(aProperty.managedReferenceFullTypename))
            {
                var buttonRect = new Rect(aPosition.x, aPosition.y, aPosition.width, EditorGUIUtility.singleLineHeight);
                if (GUI.Button(buttonRect, $"+ {aLabel.text} を選択"))
                {
                    var propertyCopy = aProperty.Copy();
                    PPTypeTreePickerPopup.ShowDerived<PPUnitAIActionBase>(buttonRect,
                        "(行動クラスが見つかりません)", instance =>
                    {
                        propertyCopy.managedReferenceValue = instance;
                        propertyCopy.serializedObject.ApplyModifiedProperties();
                    });
                }
                return;
            }

            PPManagedReferencePickerUtility.DrawAssignedField(aPosition, aProperty, aLabel);
        }

        public override float GetPropertyHeight(SerializedProperty aProperty, GUIContent aLabel)
            => PPManagedReferencePickerUtility.GetPropertyHeight(aProperty);
    }

    // 抽選ノードの枝 1 本を「子ノード名 ： 重み」の 1 行で描く
    //
    // 枝は接続先をノード ID で持つが、ID は自動採番の GUID なのでそのまま出しても読めない
    // ツリー本体から ID を引き直してノード名を表示することで、
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

            if (aProperty.serializedObject.targetObject is not PPUnitBehaviourTree tree)
            {
                return aChildId;
            }

            var node = tree.FindNode(aChildId);
            return node != null ? node.NodeName : "(削除されたノード)";
        }
    }
}
