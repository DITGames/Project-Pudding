/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPWaveSerializedAccess.cs
 * @author hqrse
 * @date 2026/11/26
 * @brief バトル定義のウェーブのノードと線を、SerializedObject で探す・書き換える補助
 * =====================================*/

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // ノードエディタが使う、バトル定義の SerializedObject のウェーブのノードと線の読み書き。書いた値の反映(Apply)は呼ぶ側が行う
    public static class PPWaveSerializedAccess
    {
        /* 定数 */

        public const string NodesPath = "mWave.mNodes";
        public const string LinksPath = "mWave.mLinks";

        /* 問い合わせ */

        // 識別子のノードの一覧での番号。識別子が重なっていれば最初のもの(ノード図に出ているもの)。無ければ -1
        public static int FindNodeIndex(SerializedObject aSerializedObject, string aId)
        {
            if (aSerializedObject == null || string.IsNullOrEmpty(aId)) return -1;
            var nodes = aSerializedObject.FindProperty(NodesPath);
            for (int i = 0; i < nodes.arraySize; i++)
            {
                if (GetNodeId(nodes.GetArrayElementAtIndex(i)) == aId) return i;
            }
            return -1;
        }

        // 識別子のノードの SerializedProperty。無ければ null
        public static SerializedProperty FindNodeProperty(SerializedObject aSerializedObject, string aId)
        {
            int index = FindNodeIndex(aSerializedObject, aId);
            return index >= 0 ? aSerializedObject.FindProperty(NodesPath).GetArrayElementAtIndex(index) : null;
        }

        // ノードの識別子から、そのノードの SerializedProperty を引く表。識別子が重なっていれば最初のもの
        public static Dictionary<string, SerializedProperty> CollectNodeProperties(SerializedObject aSerializedObject)
        {
            var result = new Dictionary<string, SerializedProperty>();
            var nodes = aSerializedObject.FindProperty(NodesPath);
            for (int i = 0; i < nodes.arraySize; i++)
            {
                var element = nodes.GetArrayElementAtIndex(i);
                string id = GetNodeId(element);
                if (!string.IsNullOrEmpty(id)) result.TryAdd(id, element);
            }
            return result;
        }

        // 一致する線の番号。無ければ -1
        public static int FindLinkIndex(SerializedObject aSerializedObject, string aFromId, PPWaveNodePort aPort, string aToId)
        {
            var links = aSerializedObject.FindProperty(LinksPath);
            for (int i = 0; i < links.arraySize; i++)
            {
                var link = links.GetArrayElementAtIndex(i);
                if (link.FindPropertyRelative("mFromId").stringValue == aFromId
                    && link.FindPropertyRelative("mFromPort").intValue == (int)aPort
                    && link.FindPropertyRelative("mToId").stringValue == aToId) return i;
            }
            return -1;
        }

        /* 操作 */

        // ノードを一覧の最後に足す
        public static void AddNode(SerializedObject aSerializedObject, PPWaveNode aNode)
        {
            var nodes = aSerializedObject.FindProperty(NodesPath);
            int index = nodes.arraySize;
            nodes.arraySize = index + 1;
            // 増やした要素は前の要素の参照を写しているため、新しいノードを入れ直す
            nodes.GetArrayElementAtIndex(index).managedReferenceValue = aNode;
        }

        // 識別子が一致する最初のノード(ノード図に出ているもの)を消し、その識別子から出る線と入る線も消す
        public static void RemoveNode(SerializedObject aSerializedObject, string aId)
        {
            int index = FindNodeIndex(aSerializedObject, aId);
            if (index >= 0) DeleteArrayElement(aSerializedObject.FindProperty(NodesPath), index);
            var links = aSerializedObject.FindProperty(LinksPath);
            for (int i = links.arraySize - 1; i >= 0; i--)
            {
                var link = links.GetArrayElementAtIndex(i);
                if (link.FindPropertyRelative("mFromId").stringValue == aId || link.FindPropertyRelative("mToId").stringValue == aId) DeleteArrayElement(links, i);
            }
        }

        public static void SetNodePosition(SerializedObject aSerializedObject, string aId, Vector2 aPosition)
        {
            var property = FindNodeProperty(aSerializedObject, aId);
            if (property != null) property.FindPropertyRelative("mPosition").vector2Value = aPosition;
        }

        public static void AddLink(SerializedObject aSerializedObject, string aFromId, PPWaveNodePort aPort, string aToId)
        {
            var links = aSerializedObject.FindProperty(LinksPath);
            int index = links.arraySize;
            links.arraySize = index + 1;
            var link = links.GetArrayElementAtIndex(index);
            link.FindPropertyRelative("mFromId").stringValue = aFromId;
            link.FindPropertyRelative("mFromPort").intValue = (int)aPort;
            link.FindPropertyRelative("mToId").stringValue = aToId;
        }

        // 一致する最初の線を消す
        public static void RemoveLink(SerializedObject aSerializedObject, string aFromId, PPWaveNodePort aPort, string aToId)
        {
            int index = FindLinkIndex(aSerializedObject, aFromId, aPort, aToId);
            if (index >= 0) DeleteArrayElement(aSerializedObject.FindProperty(LinksPath), index);
        }

        /* ヘルパー */

        private static string GetNodeId(SerializedProperty aElement) => aElement.FindPropertyRelative("mId")?.stringValue;

        // 要素を消す。1 回で消えない種類の要素(参照を空にするだけのもの)は、もう 1 回消す
        private static void DeleteArrayElement(SerializedProperty aArray, int aIndex)
        {
            int size = aArray.arraySize;
            aArray.DeleteArrayElementAtIndex(aIndex);
            if (aArray.arraySize == size) aArray.DeleteArrayElementAtIndex(aIndex);
        }
    }
}
