/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitAITreeValidator.cs
 * @author hqrse
 * @date 2026/08/27
 * @brief 判断ツリーの構造上の問題を検出する
 * =====================================*/

using System.Collections.Generic;

namespace PPCore
{
    // 診断 1 件分
    // 警告欄への表示とグラフ上のグレー表示の両方から使うため、対象ノードと文言を組で持つ
    public readonly struct PPUnitAITreeIssue
    {
        // 対象ノードの ID。ツリー全体に関わる問題なら空
        public string NodeId { get; }
        // 警告欄へ出す文言
        public string Message { get; }

        // aNodeId : 対象ノードの ID
        // aMessage : 警告欄へ出す文言
        public PPUnitAITreeIssue(string aNodeId, string aMessage)
        {
            NodeId = aNodeId;
            Message = aMessage;
        }
    }

    // 判断ツリーの構造上の問題を検出する。対象は「そのままでは絶対に行動へ到達しない」形だけ
    // 条件分岐の不成立側やターゲット検索の「なし」が未接続なのは正常な書き方として扱う
    public static class PPUnitAITreeValidator
    {
        // ツリーを検査して問題を列挙する
        public static List<PPUnitAITreeIssue> Validate(PPUnitBehaviourTree aTree)
        {
            var issues = new List<PPUnitAITreeIssue>();
            if (aTree == null) return issues;

            var reachable = CollectReachable(aTree);
            if (aTree.Root == null)
            {
                issues.Add(new PPUnitAITreeIssue("", "ルートノードが設定されていません。"));
            }
            else if (aTree.Root.IsMuted)
            {
                // ミュートはノードを引く経路で効くが、ルートだけは別経路で取得するため効かない
                // 意図せず「外したつもり」になるのを防ぐため、明示的に知らせる
                issues.Add(new PPUnitAITreeIssue(aTree.Root.NodeId,
                    $"ルートノード「{aTree.Root.NodeName}」は評価から外せません。"));
            }

            foreach (var node in aTree.Nodes)
            {
                if (node == null) continue;

                if (!reachable.Contains(node.NodeId))
                {
                    issues.Add(new PPUnitAITreeIssue(node.NodeId,
                        $"「{node.NodeName}」はルートから辿り着けません。"));
                }

                CollectNodeIssues(node, issues);
            }

            CollectCycleIssues(aTree, issues);
            return issues;
        }

        // サブツリー参照が循環している箇所を検出する
        // 循環した枝はランタイムで必ず不成立になるため、組んだ時点で気付けるようにする
        private static void CollectCycleIssues(PPUnitBehaviourTree aTree,
            List<PPUnitAITreeIssue> aIssues)
        {
            foreach (var node in aTree.Nodes)
            {
                if (node is not PPUnitAISubTreeNode subTreeNode || subTreeNode.SubTree == null) continue;

                // 参照先から辿って、検査中のツリーへ戻ってくるかを見る
                // 自己参照（参照先が自分自身）もこの判定で捕まる
                var visited = new HashSet<PPUnitBehaviourTree> { aTree };
                if (!ReachesTree(subTreeNode.SubTree, aTree, visited)) continue;

                aIssues.Add(new PPUnitAITreeIssue(node.NodeId,
                    $"「{node.NodeName}」の参照先「{subTreeNode.SubTree.name}」が循環参照しています。"));
            }
        }

        // 指定したツリーから辿って、目的のツリーへ到達するかを調べる
        private static bool ReachesTree(PPUnitBehaviourTree aTree,
            PPUnitBehaviourTree aTarget, HashSet<PPUnitBehaviourTree> aVisited)
        {
            if (aTree == aTarget) return true;
            if (!aVisited.Add(aTree)) return false;

            foreach (var node in aTree.Nodes)
            {
                if (node is not PPUnitAISubTreeNode subTreeNode || subTreeNode.SubTree == null) continue;
                if (ReachesTree(subTreeNode.SubTree, aTarget, aVisited)) return true;
            }
            return false;
        }

        // ノード 1 つ分の設定漏れを検出する
        private static void CollectNodeIssues(PPUnitAINode aNode, List<PPUnitAITreeIssue> aIssues)
        {
            switch (aNode)
            {
                case PPUnitAIActionNode actionNode when !actionNode.HasAction:
                    aIssues.Add(new PPUnitAITreeIssue(aNode.NodeId,
                        $"「{aNode.NodeName}」に実行する行動が設定されていません。"));
                    break;

                // 成立側が未接続だと、条件を満たしても進む先が無く必ず不成立になる
                case PPUnitAIConditionNode conditionNode when !conditionNode.HasMatchedBranch:
                    aIssues.Add(new PPUnitAITreeIssue(aNode.NodeId,
                        $"「{aNode.NodeName}」の「成立したとき」が未接続です。"));
                    break;

                // 同上。一度きり・ラッチも成立側へ進めなければ設定した意味が無い
                case PPUnitAILatchNode latchNode when !latchNode.HasMatchedBranch:
                    aIssues.Add(new PPUnitAITreeIssue(aNode.NodeId,
                        $"「{aNode.NodeName}」の「成立したとき」が未接続です。"));
                    break;

                // 同上。見つかっても進む先が無ければ検索した意味が無い
                case PPUnitAISearchNode searchNode when !searchNode.HasFoundBranch:
                    aIssues.Add(new PPUnitAITreeIssue(aNode.NodeId,
                        $"「{aNode.NodeName}」の「見つかったとき」が未接続です。"));
                    break;

                // 参照先が無ければ、このノードは必ず不成立になる
                case PPUnitAISubTreeNode subTreeNode when !subTreeNode.HasSubTree:
                    aIssues.Add(new PPUnitAITreeIssue(aNode.NodeId,
                        $"「{aNode.NodeName}」の参照先の判断ツリーが未設定です。"));
                    break;

                // 通す確率が 0 だと、繋いだ枝へ決して進まない
                case PPUnitAIProbabilityNode probabilityNode when probabilityNode.Probability <= 0f:
                    aIssues.Add(new PPUnitAITreeIssue(aNode.NodeId,
                        $"「{aNode.NodeName}」の通す確率が 0 のため、枝へ進みません。"));
                    break;

                // 消化する子が無ければ、このノードは必ず不成立になる
                case PPUnitAISequenceNode sequenceNode when sequenceNode.ChildIds.Count == 0:
                    aIssues.Add(new PPUnitAITreeIssue(aNode.NodeId,
                        $"「{aNode.NodeName}」に消化する子ノードが繋がっていません。"));
                    break;
            }

            CollectSkillFilterIssues(aNode, aIssues);
        }

        // スキルを直接指定しているのに定義が未設定な箇所を検出する
        // その状態ではどのスキルにも合致せず、枝が必ず不成立になるため設定漏れとして扱う
        private static void CollectSkillFilterIssues(PPUnitAINode aNode, List<PPUnitAITreeIssue> aIssues)
        {
            switch (aNode)
            {
                case PPUnitAIActionNode actionNode:
                    AddIfUnsetDirectSkill(aNode, actionNode.Action as IPPUnitAISkillFilterOwner, aIssues);
                    break;

                case PPUnitAIConditionNode conditionNode:
                    AddIfUnsetDirectSkill(aNode, conditionNode.UnitConditions, aIssues);
                    break;

                case PPUnitAILatchNode latchNode:
                    AddIfUnsetDirectSkill(aNode, latchNode.UnitConditions, aIssues);
                    break;

                case PPUnitAISearchNode searchNode:
                    AddIfUnsetDirectSkill(aNode, searchNode.Conditions, aIssues);
                    break;
            }
        }

        // 条件リストの中から、直接指定なのに未設定なものを探す
        private static void AddIfUnsetDirectSkill(PPUnitAINode aNode,
            IReadOnlyList<PPUnitConditionValidator> aConditions, List<PPUnitAITreeIssue> aIssues)
        {
            foreach (var condition in aConditions)
            {
                AddIfUnsetDirectSkill(aNode, condition as IPPUnitAISkillFilterOwner, aIssues);
            }
        }

        // 絞り込みが直接指定なのにスキル定義が未設定なら問題として積む
        private static void AddIfUnsetDirectSkill(PPUnitAINode aNode, IPPUnitAISkillFilterOwner aOwner,
            List<PPUnitAITreeIssue> aIssues)
        {
            if (aOwner?.Filter == null) return;
            if (aOwner.Filter.Mode != PPUnitAISkillFilterMode.Direct) return;
            if (aOwner.Filter.SkillDefinition != null) return;

            aIssues.Add(new PPUnitAITreeIssue(aNode.NodeId,
                $"「{aNode.NodeName}」でスキルが直接指定されていますが、スキル定義が未設定です。"));
        }

        // ルートから辿り着けるノードの ID を集める
        // ミュートされたノードも繋がっているものとして辿る
        private static HashSet<string> CollectReachable(PPUnitBehaviourTree aTree)
        {
            var reachable = new HashSet<string>();
            var root = aTree.Root;
            if (root == null) return reachable;

            var stack = new Stack<PPUnitAINode>();
            stack.Push(root);
            reachable.Add(root.NodeId);

            while (stack.Count > 0)
            {
                var node = stack.Pop();
                foreach (var port in node.Ports)
                {
                    foreach (var childId in port.ChildIds)
                    {
                        if (string.IsNullOrEmpty(childId) || !reachable.Add(childId)) continue;

                        var child = aTree.FindNode(childId);
                        if (child != null) stack.Push(child);
                    }
                }
            }
            return reachable;
        }
    }
}
