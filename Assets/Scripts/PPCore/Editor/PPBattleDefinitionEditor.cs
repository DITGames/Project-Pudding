/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleDefinitionEditor.cs
 * @author hqrse
 * @date 2026/11/23
 * @brief バトル定義のインスペクタ。ウェーブエディタを開くボタンとウェーブの要約、ウェーブに出てくるユニットから合計した難易度ポイントを出す
 * =====================================*/

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // PPBattleDefinition のインスペクタ。ウェーブはウェーブエディタで編集するため、ここでは開くボタンと要約だけを出す
    // 既定の項目の下に、難易度ポイントの合計と内訳を編集不可で出す
    [CustomEditor(typeof(PPBattleDefinition), true)]
    public class PPBattleDefinitionEditor : UnityEditor.Editor
    {
        /* Unity イベント */

        public override void OnInspectorGUI()
        {
            var definition = (PPBattleDefinition)target;
            var wave = definition.Wave;
            if (GUILayout.Button("ウェーブエディタで開く")) PPBattleWaveEditorWindow.Open(definition);
            var problems = new List<string>();
            wave.Validate(problems);
            string infinite = wave.HasInfinite() ? "・無限あり" : string.Empty;
            EditorGUILayout.LabelField("ウェーブ", $"段階 {wave.Stages.Count}・敵 {wave.CountFiniteEnemies()} 体{infinite}・誤り {problems.Count}");
            EditorGUILayout.Space();

            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "mWave");
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("難易度", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("難易度ポイント", definition.DifficultyPoints);
                EditorGUI.indentLevel++;
                var units = wave.CollectUnits();
                if (units.Count == 0) EditorGUILayout.LabelField("ウェーブに出す敵がありません");
                foreach (var unit in units) EditorGUILayout.TextField(unit.DisplayName, $"Lv1 : {unit.EvaluateDifficultyPoint(1)}");
                EditorGUI.indentLevel--;
            }
        }
    }
}
