/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPEncounterDefinitionEditor.cs
 * @author hqrse
 * @date 2026/11/23
 * @brief 遭遇の定義に、編成から合計した難易度ポイントを表示するインスペクタ
 * =====================================*/

using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // PPEncounterDefinition のインスペクタ。既定の項目の下に、難易度ポイントの合計と配置ごとの内訳を編集不可で出す
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
                foreach (var position in PPEncounterDefinition.Positions)
                {
                    EditorGUILayout.TextField(GetPositionName(position), DescribeMember(encounter, position));
                }
                EditorGUI.indentLevel--;
            }
        }

        /* 表示 */

        // 配置にいるユニットの名前・レベル・ポイントを 1 行にする。いなければ「なし」
        private static string DescribeMember(PPEncounterDefinition aEncounter, PPPartyPosition aPosition)
        {
            var entry = aEncounter.Party != null ? aEncounter.Party.GetMember(aPosition) : null;
            if (entry == null || entry.Unit == null) return "なし";
            return $"{entry.Unit.DisplayName} Lv{entry.Level} : {aEncounter.GetDifficultyPoint(aPosition)}";
        }

        private static string GetPositionName(PPPartyPosition aPosition) => aPosition switch
        {
            PPPartyPosition.Right => "右",
            PPPartyPosition.Left => "左",
            _ => "中央",
        };
    }
}
