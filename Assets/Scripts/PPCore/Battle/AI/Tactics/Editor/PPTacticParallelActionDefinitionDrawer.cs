/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPTacticParallelActionDefinitionDrawer.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief 並行アクションアセット参照フィールド用インスペクタ拡張
 * =====================================*/

using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // PPTacticParallelActionDefinition の参照フィールド・リスト要素のラベルをアクション名に差し替える
    // 素の表示だと「Element 0」とアセット名しか出ず、戦術に何が並んでいるのか読み取れないため
    // 標準のオブジェクトピッカーもアセット名しか出せないため、
    // 「アクション名 : 説明」で選べる専用ピッカーを開くボタンを併設している
    // オブジェクトフィールド自体は残してあるので、プロジェクトウィンドウからのドラッグ＆ドロップも使える
    [CustomPropertyDrawer(typeof(PPTacticParallelActionDefinition))]
    public class PPTacticParallelActionDefinitionDrawer : PropertyDrawer
    {
        // 選択ボタンの幅
        private const float PickButtonWidth = 26f;
        // 説明をラベルへ載せる際の最大文字数
        private const int SummaryMaxLength = 40;
        // ピッカーの表示サイズ。説明文まで並ぶため標準より広く取る
        private static readonly Vector2 PickerSize = new(420f, 360f);

        public override void OnGUI(Rect aPosition, SerializedProperty aProperty, GUIContent aLabel)
        {
            var action = aProperty.objectReferenceValue as PPTacticParallelActionDefinition;
            var label = new GUIContent(aLabel);

            if (action != null)
            {
                // 先行実行は実行順序が変わる重要な設定なので、ラベルからも読み取れるようにする
                label.text = action.IsBeforeSteps ? $"[先行] {action.ActionName}" : action.ActionName;
                label.tooltip = action.Description;
            }

            var fieldRect = new Rect(aPosition.x, aPosition.y,
                aPosition.width - PickButtonWidth - 2f, EditorGUIUtility.singleLineHeight);
            var buttonRect = new Rect(aPosition.xMax - PickButtonWidth, aPosition.y,
                PickButtonWidth, EditorGUIUtility.singleLineHeight);

            // EditorGUI.PropertyField を使うとこのドロワー自身が再入して無限再帰になるため、
            // オブジェクトフィールドを直接描画する
            EditorGUI.BeginChangeCheck();
            var picked = EditorGUI.ObjectField(fieldRect, label, aProperty.objectReferenceValue,
                typeof(PPTacticParallelActionDefinition), false);
            if (EditorGUI.EndChangeCheck())
            {
                aProperty.objectReferenceValue = picked;
            }

            if (GUI.Button(buttonRect, new GUIContent("≡", "アクション名と説明から選ぶ")))
            {
                // ポップアップのコールバックは非同期(フレームをまたぐ)ため、プロパティをコピーして保持する
                var propertyCopy = aProperty.Copy();
                PPAssetTreePickerPopup.ShowAssets<PPTacticParallelActionDefinition>(buttonRect,
                    BuildPickerEntry, "(並行アクションアセットが見つかりません)", selected =>
                {
                    propertyCopy.objectReferenceValue = selected;
                    propertyCopy.serializedObject.ApplyModifiedProperties();
                }, PickerSize);
            }
        }

        public override float GetPropertyHeight(SerializedProperty aProperty, GUIContent aLabel)
            => EditorGUIUtility.singleLineHeight;

        // ピッカーに並べる 1 件分の表示を組み立てる
        // 先行実行するものだけ別のフォルダへ分け、実行順序の違いが一目で分かるようにする
        // aAction : 対象の並行アクション
        // return : フォルダ階層と表示名の組
        private static (string FolderPath, string Label) BuildPickerEntry(PPTacticParallelActionDefinition aAction)
        {
            string summary = BuildSummary(aAction.Description);
            string label = string.IsNullOrEmpty(summary) ? aAction.ActionName : $"{aAction.ActionName} : {summary}";
            return (aAction.IsBeforeSteps ? "ステップより先に実行" : "ステップの後に実行", label);
        }

        // 説明文をピッカーの 1 行に収まる長さへ整える
        // 複数行の説明は 1 行目だけを使い、長すぎる場合は末尾を省略する
        // aDescription : 元の説明文
        // return : 整形された説明文。説明が無ければ空文字
        private static string BuildSummary(string aDescription)
        {
            if (string.IsNullOrWhiteSpace(aDescription)) return "";

            string line = aDescription.Split('\n')[0].Trim();
            return line.Length <= SummaryMaxLength ? line : $"{line[..SummaryMaxLength]}…";
        }
    }
}
