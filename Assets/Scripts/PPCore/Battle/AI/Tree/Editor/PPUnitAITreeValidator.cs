/* =====================================
 * Copyright hqrse. All rights reserved.
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

    // 判断ツリーの構造上の問題を検出する
    //
    // 検出するのは「そのままでは絶対に行動へ到達しない」形だけに絞る
    // 条件分岐の「成立しなかったとき」とターゲット検索の「なし」が未接続なのは、
    // 次の候補へ流すという正常な書き方なので警告の対象にしない
    public static class PPUnitAITreeValidator
    {
        // ツリーを検査して問題を列挙する
        // aProfile : 検査する判断ツリー
        // return : 見つかった問題の一覧。問題が無ければ空
        public static List<PPUnitAITreeIssue> Validate(PPUnitAIProfileDefinition aProfile)
        {
            var issues = new List<PPUnitAITreeIssue>();
            if (aProfile == null) return issues;

            var reachable = CollectReachable(aProfile);
            if (aProfile.Root == null)
            {
                issues.Add(new PPUnitAITreeIssue("", "ルートノードが設定されていません。"));
            }
            else if (aProfile.Root.IsMuted)
            {
                // ミュートはノードを引く経路で効くが、ルートだけは別経路で取得するため効かない
                // 意図せず「外したつもり」になるのを防ぐため、明示的に知らせる
                issues.Add(new PPUnitAITreeIssue(aProfile.Root.NodeId,
                    $"ルートノード「{aProfile.Root.NodeName}」は評価から外せません。"));
            }

            foreach (var node in aProfile.Nodes)
            {
                if (node == null) continue;

                if (!reachable.Contains(node.NodeId))
                {
                    issues.Add(new PPUnitAITreeIssue(node.NodeId,
                        $"「{node.NodeName}」はルートから辿り着けません。"));
                }

                CollectNodeIssues(node, issues);
            }

            CollectCycleIssues(aProfile, issues);
            return issues;
        }

        // サブツリー参照が循環している箇所を検出する
        //
        // 循環はランタイム側でも打ち切られるが、その場合その枝は必ず不成立になる
        // 意図した行動が「なぜか一度も出ない」形で表面化するため、組んだ時点で気付けるようにする
        //
        // aProfile : 検査する判断ツリー
        // aIssues : 見つかった問題の追加先
        private static void CollectCycleIssues(PPUnitAIProfileDefinition aProfile,
            List<PPUnitAITreeIssue> aIssues)
        {
            foreach (var node in aProfile.Nodes)
            {
                if (node is not PPUnitAISubTreeNode subTreeNode || subTreeNode.SubTree == null) continue;

                // 参照先から辿って、検査中のツリーへ戻ってくるかを見る
                // 自己参照（参照先が自分自身）もこの判定で捕まる
                var visited = new HashSet<PPUnitAIProfileDefinition> { aProfile };
                if (!ReachesProfile(subTreeNode.SubTree, aProfile, visited)) continue;

                aIssues.Add(new PPUnitAITreeIssue(node.NodeId,
                    $"「{node.NodeName}」の参照先「{subTreeNode.SubTree.name}」が循環参照しています。"));
            }
        }

        // 指定したツリーから辿って、目的のツリーへ到達するかを調べる
        // aProfile : 辿り始めるツリー
        // aTarget : 到達を調べる対象のツリー
        // aVisited : 既に辿ったツリー。同じツリーを二度辿らないための記録
        // return : 到達するなら true
        private static bool ReachesProfile(PPUnitAIProfileDefinition aProfile,
            PPUnitAIProfileDefinition aTarget, HashSet<PPUnitAIProfileDefinition> aVisited)
        {
            if (aProfile == aTarget) return true;
            if (!aVisited.Add(aProfile)) return false;

            foreach (var node in aProfile.Nodes)
            {
                if (node is not PPUnitAISubTreeNode subTreeNode || subTreeNode.SubTree == null) continue;
                if (ReachesProfile(subTreeNode.SubTree, aTarget, aVisited)) return true;
            }
            return false;
        }

        // ノード 1 つ分の設定漏れを検出する
        // aNode : 検査するノード
        // aIssues : 見つかった問題の追加先
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
        // aNode : 検査するノード
        // aIssues : 見つかった問題の追加先
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
        // aNode : 検査元のノード
        // aConditions : 走査する条件リスト
        // aIssues : 見つかった問題の追加先
        private static void AddIfUnsetDirectSkill(PPUnitAINode aNode,
            IReadOnlyList<PPUnitConditionValidator> aConditions, List<PPUnitAITreeIssue> aIssues)
        {
            foreach (var condition in aConditions)
            {
                AddIfUnsetDirectSkill(aNode, condition as IPPUnitAISkillFilterOwner, aIssues);
            }
        }

        // 絞り込みが直接指定なのにスキル定義が未設定なら問題として積む
        // aNode : 検査元のノード
        // aOwner : 絞り込みの保持者。null なら何もしない
        // aIssues : 見つかった問題の追加先
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
        // ミュートされたノードも「構造としては繋がっている」ものとして辿る
        // 一時的に外しただけのノードが到達不能として警告されるのを避けるため
        // aProfile : 検査する判断ツリー
        // return : 到達できるノードの ID
        private static HashSet<string> CollectReachable(PPUnitAIProfileDefinition aProfile)
        {
            var reachable = new HashSet<string>();
            var root = aProfile.Root;
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

                        var child = aProfile.FindNode(childId);
                        if (child != null) stack.Push(child);
                    }
                }
            }
            return reachable;
        }
    }
}
