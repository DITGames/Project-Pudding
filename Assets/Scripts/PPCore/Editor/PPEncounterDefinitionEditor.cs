/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPEncounterDefinitionEditor.cs
 * @author hqrse
 * @date 2026/11/23
 * @brief ステージ定義に、ウェーブに出てくるユニットから合計した難易度ポイントを表示するインスペクタ
 * =====================================*/

using UnityEditor;

namespace PPCore
{
    // PPEncounterDefinition のインスペクタ。既定の項目の下に、難易度ポイントの合計とユニットの種類ごとの内訳を編集不可で出す
    [CustomEditor(typeof(PPEncounterDefinition), true)]
    public class PPEncounterDefinitionEditor : UnityEditor.Editor
    {
        /* Unity イベント */

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var encounter = (PPEncounterDefinition)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("難易度", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("難易度ポイント", encounter.DifficultyPoints);
                EditorGUI.indentLevel++;
                var units = encounter.Wave.CollectUnits();
                if (units.Count == 0) EditorGUILayout.LabelField("ウェーブに出す敵がありません");
                foreach (var unit in units)
                {
                    int level = encounter.Wave.GetMaxLevel(unit);
                    EditorGUILayout.TextField(unit.DisplayName, $"Lv{level} : {unit.EvaluateDifficultyPoint(level)}");
                }
                EditorGUI.indentLevel--;
            }
        }
    }
}
