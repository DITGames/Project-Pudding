/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequenceDefinition.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief 複数VFXの再生をノードグラフとして定義するアセット
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace VFXUtility
{
    [CreateAssetMenu(fileName = "VFXSequenceDefinition", menuName = "VFXUtility/VFXSequenceDefinition")]
    public class VFXSequenceDefinition : ScriptableObject
    {
        [SerializeReference]
        [Label("ノード", true)]
        private List<VFXSequenceNodeBase> mNodes = new();

        public IReadOnlyList<VFXSequenceNodeBase> Nodes => mNodes;

        // ノードIDからノードを検索する
        // aNodeId : 検索するノードのID
        // 戻り値 : 見つかったノード。見つからない場合はnull
        public VFXSequenceNodeBase FindNode(string aNodeId)
        {
            return mNodes.Find(n => n.NodeId == aNodeId);
        }

        // グラフ内の全ルートノードを取得する(個数チェック用)
        public List<VFXSequenceRootNode> GetAllRootNodes()
        {
            var result = new List<VFXSequenceRootNode>();
            foreach (VFXSequenceNodeBase node in mNodes)
            {
                if (node is VFXSequenceRootNode rootNode)
                {
                    result.Add(rootNode);
                }
            }
            return result;
        }

        // ルートノードの個数がちょうど1個でない(Play()の開始点が正しく決まらない)かを判定する
        public bool HasInvalidRootNodeCount() => GetAllRootNodes().Count != 1;

        // Play()が実際に使うルートノードを取得する。0個ならnull、2個以上ならグラフ内で最初に見つかったものを返す
        public VFXSequenceRootNode GetPlayRootNodeOrNull()
        {
            foreach (VFXSequenceNodeBase node in mNodes)
            {
                if (node is VFXSequenceRootNode rootNode)
                {
                    return rootNode;
                }
            }
            return null;
        }

        // 指定したイベント名に一致するイベントノードを全て取得する
        // aEventName : 検索するイベント名
        public List<VFXSequenceEventNode> FindEventNodes(string aEventName)
        {
            var result = new List<VFXSequenceEventNode>();
            foreach (VFXSequenceNodeBase node in mNodes)
            {
                if (node is VFXSequenceEventNode eventNode && eventNode.EventName == aEventName)
                {
                    result.Add(eventNode);
                }
            }
            return result;
        }

        // グラフ内の全ノードが保持するパラメータを列挙する(公開名の収集・検証に使う)
        public IEnumerable<VFXSequenceNodeParameter> EnumerateAllParameters()
        {
            foreach (VFXSequenceNodeBase node in mNodes)
            {
                IReadOnlyList<VFXSequenceNodeParameter> parameters = node switch
                {
                    VFXSequencePlayableNodeBase playableNode => playableNode.Parameters,
                    VFXSequenceSetParameterNode setParameterNode => setParameterNode.Parameters,
                    _ => null,
                };

                if (parameters == null)
                {
                    continue;
                }

                foreach (VFXSequenceNodeParameter param in parameters)
                {
                    yield return param;
                }
            }
        }

        // グラフ内で使われている公開名の集合を取得する
        public HashSet<string> CollectExposedNames()
        {
            var result = new HashSet<string>();
            foreach (VFXSequenceNodeParameter param in EnumerateAllParameters())
            {
                if (!string.IsNullOrEmpty(param.ExposedName))
                {
                    result.Add(param.ExposedName);
                }
            }
            return result;
        }

        // 入射接続を持つゴールノードが1つも存在しないかを判定する
        // ゴールノード未配置・配置済みだが誰も接続していない(到達不能)の両方でtrueになる
        public bool HasNoReachableGoalNode()
        {
            var incomingNodeIds = new HashSet<string>();
            foreach (VFXSequenceNodeBase node in mNodes)
            {
                foreach (string nextNodeId in node.NextNodeIds)
                {
                    incomingNodeIds.Add(nextNodeId);
                }
            }

            foreach (VFXSequenceNodeBase node in mNodes)
            {
                if (node is VFXSequenceGoalNode && incomingNodeIds.Contains(node.NodeId))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
