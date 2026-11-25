/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPWaveDefinitionDrawers.cs
 * @author hqrse
 * @date 2026/11/25
 * @brief ウェーブのノードの種類選び・線のノードと出口の選び方を、インスペクタで扱いやすくする
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // ノードが未選択なら種類を選ぶボタン、選択済みなら「種類「メモ」」を見出しにして項目を出す
    // 複製で識別子が他のノードと重なったら、別のノードとして作り直すボタンを出す
    [CustomPropertyDrawer(typeof(PPWaveNode), true)]
    public class PPWaveNodeDrawer : PropertyDrawer
    {
        /* 定数 */

        private const float ButtonHeight = 20f;

        /* 描画 */

        public override void OnGUI(Rect aPosition, SerializedProperty aProperty, GUIContent aLabel)
        {
            var node = aProperty.managedReferenceValue as PPWaveNode;
            if (node != null && IsDuplicated(aProperty, node))
            {
                var buttonRect = new Rect(aPosition.x, aPosition.y, aPosition.width, ButtonHeight);
                if (GUI.Button(buttonRect, "識別子が他のノードと重なっています。押すと別のノードとして識別子を振り直します")) Separate(aProperty, node);
                aPosition.yMin += ButtonHeight + EditorGUIUtility.standardVerticalSpacing;
            }
            var label = node != null ? new GUIContent(PPWaveNodeEditorUtility.GetNodeLabel(node)) : new GUIContent("ノード");
            PPManagedReferencePickerUtility.OnGUI(aPosition, aProperty, label, typeof(PPWaveNode));
        }

        public override float GetPropertyHeight(SerializedProperty aProperty, GUIContent aLabel)
        {
            float height = PPManagedReferencePickerUtility.GetPropertyHeight(aProperty);
            var node = aProperty.managedReferenceValue as PPWaveNode;
            if (node != null && IsDuplicated(aProperty, node)) height += ButtonHeight + EditorGUIUtility.standardVerticalSpacing;
            return height;
        }

        /* ヘルパー */

        // 同じ一覧の前のほうに、同じ識別子のノード(複製で同じものを指している場合を含む)があるか
        private static bool IsDuplicated(SerializedProperty aProperty, PPWaveNode aNode)
        {
            string path = aProperty.propertyPath;
            int arrayIndex = path.LastIndexOf(".Array.data[", StringComparison.Ordinal);
            if (arrayIndex < 0) return false;
            var list = aProperty.serializedObject.FindProperty(path.Substring(0, arrayIndex));
            if (list == null || !list.isArray) return false;
            for (int i = 0; i < list.arraySize; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                if (element.propertyPath == path) return false;
                if (element.managedReferenceValue is PPWaveNode other && (ReferenceEquals(other, aNode) || other.Id == aNode.Id)) return true;
            }
            return false;
        }

        // 中身を写した別のノードに置き換え、新しい識別子を振る
        private static void Separate(SerializedProperty aProperty, PPWaveNode aNode)
        {
            var copy = (PPWaveNode)Activator.CreateInstance(aNode.GetType());
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(aNode), copy);
            copy.RegenerateId();
            aProperty.managedReferenceValue = copy;
            aProperty.serializedObject.ApplyModifiedProperties();
        }
    }

    // ノードの識別子を持つ文字列を、同じウェーブのノードから選ぶドロップダウンにする
    [CustomPropertyDrawer(typeof(PPWaveNodeReferenceAttribute))]
    public class PPWaveNodeReferenceDrawer : PropertyDrawer
    {
        /* 描画 */

        public override void OnGUI(Rect aPosition, SerializedProperty aProperty, GUIContent aLabel)
        {
            var reference = (PPWaveNodeReferenceAttribute)attribute;
            PPWaveNodeEditorUtility.DrawNodePopup(aPosition, aProperty, new GUIContent(reference.Text), reference.IsSpawnNodeOnly);
        }

        public override float GetPropertyHeight(SerializedProperty aProperty, GUIContent aLabel) => EditorGUIUtility.singleLineHeight;
    }

    // 線を「出口のノード・出口・行き先のノード」の 3 行で出す。出口は、出口のノードの種類が持つものだけを選べる
    [CustomPropertyDrawer(typeof(PPWaveLink))]
    public class PPWaveLinkDrawer : PropertyDrawer
    {
        /* 定数 */

        private const int LineCount = 3;

        /* 描画 */

        public override void OnGUI(Rect aPosition, SerializedProperty aProperty, GUIContent aLabel)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            var fromId = aProperty.FindPropertyRelative("mFromId");
            var fromPort = aProperty.FindPropertyRelative("mFromPort");
            var toId = aProperty.FindPropertyRelative("mToId");

            var rect = new Rect(aPosition.x, aPosition.y, aPosition.width, line);
            PPWaveNodeEditorUtility.DrawNodePopup(rect, fromId, new GUIContent("出口のノード"), false);
            rect.y += line + spacing;
            DrawPortPopup(rect, fromPort, PPWaveNodeEditorUtility.FindNode(aProperty, fromId.stringValue));
            rect.y += line + spacing;
            PPWaveNodeEditorUtility.DrawNodePopup(rect, toId, new GUIContent("行き先のノード"), false);
        }

        public override float GetPropertyHeight(SerializedProperty aProperty, GUIContent aLabel)
            => EditorGUIUtility.singleLineHeight * LineCount + EditorGUIUtility.standardVerticalSpacing * (LineCount - 1);

        /* ヘルパー */

        // 出口のノードが持つ出口だけを並べる。今の値がそのノードに無ければ、それも並べて誤りと分かるようにする
        private static void DrawPortPopup(Rect aPosition, SerializedProperty aProperty, PPWaveNode aNode)
        {
            var current = (PPWaveNodePort)aProperty.enumValueIndex;
            var ports = new List<PPWaveNodePort>();
            if (aNode != null) ports.AddRange(aNode.Outputs);
            else ports.AddRange((PPWaveNodePort[])Enum.GetValues(typeof(PPWaveNodePort)));
            if (!ports.Contains(current)) ports.Add(current);

            var labels = new GUIContent[ports.Count];
            for (int i = 0; i < ports.Count; i++)
            {
                bool isValid = aNode == null || aNode.HasOutput(ports[i]);
                labels[i] = new GUIContent(isValid ? PPWaveDefinition.GetPortName(ports[i]) : $"(このノードには無い) {PPWaveDefinition.GetPortName(ports[i])}");
            }
            int selected = EditorGUI.Popup(aPosition, new GUIContent("出口"), ports.IndexOf(current), labels);
            if (selected >= 0 && ports[selected] != current) aProperty.enumValueIndex = (int)ports[selected];
        }
    }

    // ウェーブのドロワーが共通で使う、同じウェーブのノードの探し方と選び方
    public static class PPWaveNodeEditorUtility
    {
        /* 表示 */

        // 「種類「メモ」」の形の見出し
        public static string GetNodeLabel(PPWaveNode aNode)
            => string.IsNullOrEmpty(aNode.Memo) ? aNode.TypeName : $"{aNode.TypeName}「{aNode.Memo}」";

        // aProperty(識別子の文字列)を、同じウェーブのノードから選ぶドロップダウンで描く
        public static void DrawNodePopup(Rect aPosition, SerializedProperty aProperty, GUIContent aLabel, bool aIsSpawnNodeOnly)
        {
            var ids = new List<string> { string.Empty };
            var labels = new List<GUIContent> { new("(未選択)") };
            var nodes = FindNodesProperty(aProperty);
            if (nodes != null)
            {
                for (int i = 0; i < nodes.arraySize; i++)
                {
                    if (nodes.GetArrayElementAtIndex(i).managedReferenceValue is not PPWaveNode node) continue;
                    if (aIsSpawnNodeOnly && node is not PPWaveSpawnNode) continue;
                    ids.Add(node.Id);
                    // "/" はドロップダウンの階層の区切りになるため、見た目の近い文字に置き換える
                    labels.Add(new GUIContent($"{i + 1}. {GetNodeLabel(node).Replace('/', '／')}"));
                }
            }
            string current = aProperty.stringValue ?? string.Empty;
            int index = ids.IndexOf(current);
            if (index < 0)
            {
                ids.Add(current);
                labels.Add(new GUIContent($"(見つからない) {current}"));
                index = ids.Count - 1;
            }

            int selected = EditorGUI.Popup(aPosition, aLabel, index, labels.ToArray());
            if (selected != index) aProperty.stringValue = ids[selected];
        }

        /* 問い合わせ */

        // aProperty と同じウェーブの、識別子 aId のノード。見つからなければ null
        public static PPWaveNode FindNode(SerializedProperty aProperty, string aId)
        {
            var nodes = FindNodesProperty(aProperty);
            if (nodes == null || string.IsNullOrEmpty(aId)) return null;
            for (int i = 0; i < nodes.arraySize; i++)
            {
                if (nodes.GetArrayElementAtIndex(i).managedReferenceValue is PPWaveNode node && node.Id == aId) return node;
            }
            return null;
        }

        // ノードか線の中のプロパティから、同じウェーブのノードの一覧のプロパティを探す
        private static SerializedProperty FindNodesProperty(SerializedProperty aProperty)
        {
            string path = aProperty.propertyPath;
            foreach (var listName in new[] { "mNodes", "mLinks" })
            {
                int index = path.LastIndexOf(listName + ".Array.data[", StringComparison.Ordinal);
                if (index < 0) continue;
                // 一覧の名前が、ウェーブのフィールドの先頭か「.」の直後にあるときだけ使う
                if (index > 0 && path[index - 1] != '.') continue;
                return aProperty.serializedObject.FindProperty(path.Substring(0, index) + "mNodes");
            }
            return null;
        }
    }
}
