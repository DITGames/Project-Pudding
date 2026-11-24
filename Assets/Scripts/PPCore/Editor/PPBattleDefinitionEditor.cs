/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleDefinitionEditor.cs
 * @author hqrse
 * @date 2026/11/23
 * @brief バトル定義に、ウェーブに出てくるユニットから合計した難易度ポイントを表示するインスペクタ
 * =====================================*/

using UnityEditor;

namespace PPCore
{
    // PPBattleDefinition のインスペクタ。既定の項目の下に、難易度ポイントの合計とユニットの種類ごとの内訳を編集不可で出す
    [CustomEditor(typeof(PPBattleDefinition), true)]
    public class PPBattleDefinitionEditor : UnityEditor.Editor
    {
        /* Unity イベント */

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var encounter = (PPBattleDefinition)target;
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
