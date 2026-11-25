/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPWaveEditorPanels.cs
 * @author hqrse
 * @date 2026/11/25
 * @brief ウェーブのノードエディタの右の欄(選んだノードの中身の編集)と、下の欄(点検の誤りの一覧)
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PPCore
{
    // 選んだノードの中身を、インスペクタと同じドロワーで編集する欄
    public class PPWaveNodeInspectorPanel : VisualElement
    {
        /* 定数 */

        private const string NodesPath = "mWave.mNodes";

        /* 状態 */

        private readonly VisualElement mContent;
        private SerializedObject mSerializedObject;
        private string mNodeId;

        /* プロパティ */

        // 出しているノードの識別子。出していなければ null
        public string NodeId => mNodeId;

        /* 生成 */

        public PPWaveNodeInspectorPanel()
        {
            style.paddingLeft = 6;
            style.paddingRight = 6;
            style.paddingTop = 6;
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            Add(scroll);
            mContent = scroll.contentContainer;
            SetTarget(null, null);
        }

        /* 表示 */

        // 識別子のノードの中身を出す。ノードの並びが変わったときも、これで出し直す(項目の場所がずれるため)
        public void SetTarget(SerializedObject aSerializedObject, string aNodeId)
        {
            mSerializedObject = aSerializedObject;
            mContent.Clear();
            var property = FindNodeProperty(aSerializedObject, aNodeId);
            mNodeId = property != null ? aNodeId : null;
            if (property == null || property.managedReferenceValue is not PPWaveNode node)
            {
                mContent.Add(new Label("ノードを選ぶと、ここで中身を編集できます") { style = { whiteSpace = WhiteSpace.Normal } });
                return;
            }

            mContent.Add(new Label(node.TypeName) { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4 } });
            var iterator = property.Copy();
            var end = property.GetEndProperty();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                // 識別子は線と対象を指すのに使うため、ここでは変えさせない
                if (iterator.name == "mId") continue;
                var field = new PropertyField(iterator.Copy());
                field.Bind(aSerializedObject);
                mContent.Add(field);
            }
        }

        // 出しているノードを出し直す。ノードが無くなっていれば空にする
        public void Refresh() => SetTarget(mSerializedObject, mNodeId);

        /* ヘルパー */

        private static SerializedProperty FindNodeProperty(SerializedObject aSerializedObject, string aNodeId)
        {
            if (aSerializedObject == null || string.IsNullOrEmpty(aNodeId)) return null;
            var nodes = aSerializedObject.FindProperty(NodesPath);
            for (int i = 0; i < nodes.arraySize; i++)
            {
                var element = nodes.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative("mId")?.stringValue == aNodeId) return element;
            }
            return null;
        }
    }

    // 点検の誤りの一覧。行を押すと、そのノードを知らせる
    public class PPWaveProblemListView : VisualElement
    {
        /* 定数 */

        private const float MaxListHeight = 120f;
        private static readonly Color ProblemTextColor = new(1f, 0.55f, 0.5f);

        /* 状態 */

        private readonly Label mHeader;
        private readonly ScrollView mList;

        /* イベント */

        // 誤りの行が押されたとき(誤りのあるノードの識別子。ノードに結び付かなければ null)
        public event Action<string> OnProblemSelected;

        /* 生成 */

        public PPWaveProblemListView()
        {
            style.borderTopWidth = 1;
            style.borderTopColor = new Color(0f, 0f, 0f, 0.4f);
            style.paddingLeft = 6;
            style.paddingRight = 6;
            style.paddingTop = 3;
            style.paddingBottom = 3;
            mHeader = new Label { style = { unityFontStyleAndWeight = FontStyle.Bold } };
            Add(mHeader);
            mList = new ScrollView { style = { maxHeight = MaxListHeight } };
            Add(mList);
        }

        /* 表示 */

        public void SetProblems(IReadOnlyList<PPWaveProblem> aProblems)
        {
            mList.Clear();
            mHeader.text = aProblems.Count == 0 ? "点検: 誤りはありません" : $"点検: 誤り {aProblems.Count} 件(押すとそのノードへ移ります)";
            foreach (var problem in aProblems)
            {
                string nodeId = problem.NodeId;
                var row = new Label($"・{problem.Message}") { style = { whiteSpace = WhiteSpace.Normal, color = ProblemTextColor } };
                row.RegisterCallback<ClickEvent>(_ => OnProblemSelected?.Invoke(nodeId));
                mList.Add(row);
            }
        }
    }
}
