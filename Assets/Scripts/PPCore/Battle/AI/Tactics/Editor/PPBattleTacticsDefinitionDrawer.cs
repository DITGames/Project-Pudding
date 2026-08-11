/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleTacticsDefinitionDrawer.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief 戦術アセット参照フィールド用インスペクタ拡張
 * =====================================*/

using System;
using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // PPBattleTacticsDefinition の参照フィールド・リスト要素のラベルを戦術名に差し替える
    // 素の表示だと「Element 0」とアセット名しか出ず、
    // プロファイルの戦術リストを見ても何の戦術が並んでいるのか読み取れないため
    // リスト要素の場合は並び順（＝優先度）も併記する
    // 標準のオブジェクトピッカーもアセット名しか出せないため、
    // 「戦術名 : 説明」で選べる専用ピッカーを開くボタンを併設している
    // オブジェクトフィールド自体は残してあるので、プロジェクトウィンドウからのドラッグ＆ドロップも使える
    [CustomPropertyDrawer(typeof(PPBattleTacticsDefinition))]
    public class PPBattleTacticsDefinitionDrawer : PropertyDrawer
    {
        // 配列要素のプロパティパスに現れる添字部分の目印
        private const string ArrayElementMark = ".Array.data[";
        // 選択ボタンの幅
        private const float PickButtonWidth = 26f;
        // ピッカーの表示サイズ。説明文まで並ぶため標準より広く取る
        private static readonly Vector2 PickerSize = new(420f, 360f);

        public override void OnGUI(Rect aPosition, SerializedProperty aProperty, GUIContent aLabel)
        {
            var tactics = aProperty.objectReferenceValue as PPBattleTacticsDefinition;
            var label = new GUIContent(aLabel);

            if (tactics != null)
            {
                label.text = BuildLabel(aProperty, tactics);
                label.tooltip = tactics.Description;
            }

            var fieldRect = new Rect(aPosition.x, aPosition.y,
                aPosition.width - PickButtonWidth - 2f, EditorGUIUtility.singleLineHeight);
            var buttonRect = new Rect(aPosition.xMax - PickButtonWidth, aPosition.y,
                PickButtonWidth, EditorGUIUtility.singleLineHeight);

            // EditorGUI.PropertyField を使うとこのドロワー自身が再入して無限再帰になるため、
            // オブジェクトフィールドを直接描画する
            EditorGUI.BeginChangeCheck();
            var picked = EditorGUI.ObjectField(fieldRect, label, aProperty.objectReferenceValue,
                typeof(PPBattleTacticsDefinition), false);
            if (EditorGUI.EndChangeCheck())
            {
                aProperty.objectReferenceValue = picked;
            }

            if (GUI.Button(buttonRect, new GUIContent("≡", "戦術名と説明から選ぶ")))
            {
                // ポップアップのコールバックは非同期(フレームをまたぐ)ため、プロパティをコピーして保持する
                var propertyCopy = aProperty.Copy();
                PPAssetTreePickerPopup.ShowAssets<PPBattleTacticsDefinition>(buttonRect,
                    BuildPickerEntry, "(戦術アセットが見つかりません)", selected =>
                {
                    propertyCopy.objectReferenceValue = selected;
                    propertyCopy.serializedObject.ApplyModifiedProperties();
                }, PickerSize);
            }
        }

        // ピッカーに並べる 1 件分の表示を組み立てる
        // 階層は付けず、「戦術名 : 説明」の 1 行で中身が読めるようにする
        // aTactics : 対象の戦術
        // return : フォルダ階層（未使用）と表示名の組
        private static (string FolderPath, string Label) BuildPickerEntry(PPBattleTacticsDefinition aTactics)
        {
            string summary = BuildSummary(aTactics.Description);
            return ("", string.IsNullOrEmpty(summary) ? aTactics.TacticsName : $"{aTactics.TacticsName} : {summary}");
        }

        // 説明文をピッカーの 1 行に収まる長さへ整える
        // 複数行の説明は 1 行目だけを使い、長すぎる場合は末尾を省略する
        // aDescription : 元の説明文
        // return : 整形された説明文。説明が無ければ空文字
        private static string BuildSummary(string aDescription)
        {
            if (string.IsNullOrWhiteSpace(aDescription)) return "";

            const int maxLength = 40;
            string line = aDescription.Split('\n')[0].Trim();
            return line.Length <= maxLength ? line : $"{line[..maxLength]}…";
        }

        public override float GetPropertyHeight(SerializedProperty aProperty, GUIContent aLabel)
            => EditorGUIUtility.singleLineHeight;

        // 表示するラベル文字列を組み立てる
        // リスト要素なら優先度（並び順）を頭に付け、単独フィールドなら戦術名だけにする
        // aProperty : 描画対象のプロパティ
        // aTactics : 参照されている戦術
        // return : 表示するラベル文字列
        private static string BuildLabel(SerializedProperty aProperty, PPBattleTacticsDefinition aTactics)
        {
            int index = ResolveArrayIndex(aProperty);
            return index < 0 ? aTactics.TacticsName : $"[{index}] {aTactics.TacticsName}";
        }

        // プロパティパスから配列の添字を取り出す
        // パスは "mTactics.Array.data[0]" の形になるため、末尾の添字部分を読み取る
        // aProperty : 描画対象のプロパティ
        // return : 配列要素なら 0 始まりの添字。配列要素でなければ -1
        private static int ResolveArrayIndex(SerializedProperty aProperty)
        {
            string path = aProperty.propertyPath;
            int markIndex = path.LastIndexOf(ArrayElementMark, StringComparison.Ordinal);
            if (markIndex < 0) return -1;

            int start = markIndex + ArrayElementMark.Length;
            int end = path.IndexOf(']', start);
            if (end < 0) return -1;

            return int.TryParse(path[start..end], out int index) ? index : -1;
        }
    }
}
