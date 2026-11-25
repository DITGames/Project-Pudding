/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPWaveClipboard.cs
 * @author hqrse
 * @date 2026/11/26
 * @brief ウェーブのノードのコピーの中身(ノードと、選んだノードどうしの線)の文字列化と、貼り付けの書き込み
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // ノード図のコピーと貼り付けの中身。ノードは型名と中身の JSON で持ち、貼り付けるときに新しい識別子を振る
    public static class PPWaveClipboard
    {
        /* 内部型 */

        [Serializable]
        private class ClipboardNode
        {
            public string TypeName;
            public string Json;
        }

        [Serializable]
        private class ClipboardData
        {
            public List<ClipboardNode> Nodes = new();
            public List<PPWaveLink> Links = new();
        }

        /* 操作 */

        // 識別子のノードの中身と、そのノードどうしの線を文字列にする
        public static string Serialize(PPWaveDefinition aWave, IEnumerable<string> aIds)
        {
            var data = new ClipboardData();
            var ids = new HashSet<string>();
            foreach (string id in aIds)
            {
                var node = aWave.FindNode(id);
                if (node == null || !ids.Add(node.Id)) continue;
                // ユニットなどアセットへの参照を保ったまま写すため、EditorJsonUtility を使う
                data.Nodes.Add(new ClipboardNode { TypeName = node.GetType().AssemblyQualifiedName, Json = EditorJsonUtility.ToJson(node) });
            }
            foreach (var link in aWave.Links)
            {
                if (link != null && ids.Contains(link.FromId) && ids.Contains(link.ToId)) data.Links.Add(new PPWaveLink(link.FromId, link.FromPort, link.ToId));
            }
            return JsonUtility.ToJson(data);
        }

        // ノード図のコピーの中身で、ノードが 1 つ以上あるか
        public static bool CanPaste(string aData) => TryParse(aData, out _);

        // コピーしたノードを、新しい識別子で aOffset だけずらして一覧に足す。aIdMap に元の識別子から新しい識別子への対応を入れ、足したノードの識別子を返す
        public static List<string> WriteNodes(SerializedObject aSerializedObject, string aData, Vector2 aOffset, Dictionary<string, string> aIdMap)
        {
            var pastedIds = new List<string>();
            if (!TryParse(aData, out var data)) return pastedIds;
            foreach (var entry in data.Nodes)
            {
                var type = Type.GetType(entry.TypeName);
                if (type == null || type.IsAbstract || !typeof(PPWaveNode).IsAssignableFrom(type)) continue;
                var node = (PPWaveNode)Activator.CreateInstance(type);
                EditorJsonUtility.FromJsonOverwrite(entry.Json, node);
                string oldId = node.Id;
                node.RegenerateId();
                node.Position += aOffset;
                aIdMap[oldId] = node.Id;
                PPWaveSerializedAccess.AddNode(aSerializedObject, node);
                pastedIds.Add(node.Id);
            }
            return pastedIds;
        }

        // 足したノードの全滅トリガーの対象と、コピーした線を、aIdMap で新しい識別子へ付け替えて書く。WriteNodes を反映してから呼ぶ
        public static void WriteLinksAndTargets(SerializedObject aSerializedObject, string aData, IReadOnlyDictionary<string, string> aIdMap)
        {
            if (!TryParse(aData, out var data)) return;
            foreach (string newId in aIdMap.Values)
            {
                var target = PPWaveSerializedAccess.FindNodeProperty(aSerializedObject, newId)?.FindPropertyRelative("mTargetId");
                if (target != null && aIdMap.TryGetValue(target.stringValue ?? string.Empty, out string newTarget)) target.stringValue = newTarget;
            }
            foreach (var link in data.Links)
            {
                if (aIdMap.TryGetValue(link.FromId ?? string.Empty, out string fromId) && aIdMap.TryGetValue(link.ToId ?? string.Empty, out string toId))
                {
                    PPWaveSerializedAccess.AddLink(aSerializedObject, fromId, link.FromPort, toId);
                }
            }
        }

        /* ヘルパー */

        private static bool TryParse(string aData, out ClipboardData aResult)
        {
            aResult = null;
            if (string.IsNullOrEmpty(aData)) return false;
            try
            {
                aResult = JsonUtility.FromJson<ClipboardData>(aData);
            }
            catch (ArgumentException)
            {
                // ノード図のコピー以外の文字列は、貼り付けられないものとして扱う
                return false;
            }
            return aResult?.Nodes != null && aResult.Nodes.Count > 0;
        }
    }
}
