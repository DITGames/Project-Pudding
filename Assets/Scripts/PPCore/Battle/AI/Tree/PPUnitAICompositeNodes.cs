/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitAICompositeNodes.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ビヘイビアツリーの合成ノード（セレクタ・シーケンス・確率・サブツリー）
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;
using AttributeUtility;
using CustomConsole;

namespace PPCore
{
    // 子ノードを上から順に評価し、最初に行動が確定したものを採用するノード。並び順がそのまま優先度になる
    // 待機コミット中は、待ちを宣言した枝より下を評価しない（上の枝は評価されるので割り込める）
    [Serializable]
    [PPTypeMenuName("制御/優先度リスト")]
    public sealed class PPUnitAISelectorNode : PPUnitAINode
    {
        /* インスペクタ設定 */

        // 接続している子ノードの ID。並び順がそのまま優先度になる
        [Header("子ノード")]
        [Label("上から順に試す", true)]
        [SerializeField] private List<string> mChildIds = new();

        /* プロパティ */

        public override IReadOnlyList<PPUnitAINodePort> Ports
            => new[] { new PPUnitAINodePort("子ノード", mChildIds, true) };
        // 上から何番目まで並んでいるかを示す。並び順がそのまま優先度になる
        public override string Summary => $"{mChildIds.Count} 件を上から順に試す";
        protected override string DefaultNodeName => "優先度リスト";

        /* 初期化・接続 */

        // 子ノードを末尾へ繋ぐ。既に繋がっている場合は何もしない
        public override void ConnectChild(int aPortIndex, string aChildId)
        {
            if (string.IsNullOrEmpty(aChildId) || mChildIds.Contains(aChildId)) return;

            mChildIds.Add(aChildId);
        }

        // 子ノードとの接続を外す
        public override void DisconnectChild(int aPortIndex, string aChildId) => mChildIds.Remove(aChildId);

        /* 操作 */

        // 接続先の子ノード ID を対応表に従って置き換える
        // aMap : 対応表
        public override void RemapChildIds(IReadOnlyDictionary<string, string> aMap) => RemapChildIds(mChildIds, aMap);

        // 子ノードの並び順を指定どおりに揃える
        // エディタ上の配置（上にあるものほど優先）をそのまま優先度へ反映するために使う
        public override void ReorderChildren(int aPortIndex, IReadOnlyList<string> aOrderedChildIds)
        {
            mChildIds.Clear();
            foreach (var id in aOrderedChildIds)
            {
                if (!string.IsNullOrEmpty(id)) mChildIds.Add(id);
            }
        }

        /* 評価・判定 */

        // 子を上から評価し、最初に確定した結果をそのまま返す
        // 確定した場合は道順（PPUnitAIEvalContext.Path）に自分の子の添字を残す
        protected override PPUnitAINodeResult EvaluateCore(PPUnitAIEvalContext aContext)
        {
            // 自分が木のどの深さに居るかは、ここまでに積まれた道順の長さでわかる
            int depth = aContext.Path.Count;
            // 引き返した枝の通過記録を残さないため、子を試す前の長さを覚えておく
            int visitedMark = aContext.VisitedNodeIds.Count;
            bool isConstrained = aContext.IsOnCommitPath(depth);
            int limit = isConstrained ? aContext.CommitChildIndex(depth) : int.MaxValue;

            for (int i = 0; i < mChildIds.Count; i++)
            {
                var child = aContext.ResolveNode(mChildIds[i]);
                if (child == null) continue;
                // コミット中は、待ちを宣言した枝より下は見ない（割り込み指定のある枝だけは例外）
                if (i > limit && !child.IsInterrupt) continue;

                aContext.Path.Add(i);
                var result = child.Evaluate(aContext);
                if (result.IsDecided) return result;

                // 確定しなかった枝の道順と通過記録は残さない
                aContext.Path.RemoveRange(depth, aContext.Path.Count - depth);
                aContext.TrimVisited(visitedMark);
            }
            return PPUnitAINodeResult.Failed;
        }
    }

    // 同じティックの中で、子を上から順に 1 手ずつ消化していくノード（「1 手目バフ → 2 手目攻撃」など）
    // 採用済みの記録はストラテジストが 1 回の思考分だけ持ち、子を使い切ると不成立になる
    [Serializable]
    [PPTypeMenuName("制御/連携")]
    public sealed class PPUnitAISequenceNode : PPUnitAINode
    {
        /* 定数 */

        // 採用済みの記録に使うキーの接尾辞。ノード自身の通過記録と区別するために付ける
        private const string AdoptedKeySuffix = "#seq:";

        /* インスペクタ設定 */

        // 接続している子ノードの ID。並び順がそのまま消化する順番になる
        [Header("子ノード")]
        [Label("上から順に消化する", true)]
        [SerializeField] private List<string> mChildIds = new();

        /* プロパティ */

        // 接続している子ノードの ID。エディタのサマリ表示から参照する
        public IReadOnlyList<string> ChildIds => mChildIds;
        public override IReadOnlyList<PPUnitAINodePort> Ports
            => new[] { new PPUnitAINodePort("子ノード", mChildIds, true) };
        // 何手ぶんの連携かを示す
        public override string Summary => $"{mChildIds.Count} 件を同じティック内で順に消化";
        protected override string DefaultNodeName => "連携";

        /* 初期化・接続 */

        // 子ノードを末尾へ繋ぐ。既に繋がっている場合は何もしない
        public override void ConnectChild(int aPortIndex, string aChildId)
        {
            if (string.IsNullOrEmpty(aChildId) || mChildIds.Contains(aChildId)) return;

            mChildIds.Add(aChildId);
        }

        // 子ノードとの接続を外す
        public override void DisconnectChild(int aPortIndex, string aChildId) => mChildIds.Remove(aChildId);

        /* 操作 */

        // 接続先の子ノード ID を対応表に従って置き換える
        // aMap : 対応表
        public override void RemapChildIds(IReadOnlyDictionary<string, string> aMap) => RemapChildIds(mChildIds, aMap);

        // 子ノードの並び順を指定どおりに揃える
        // エディタ上の配置（上にあるものほど先に消化する）をそのまま順番へ反映するために使う
        public override void ReorderChildren(int aPortIndex, IReadOnlyList<string> aOrderedChildIds)
        {
            mChildIds.Clear();
            foreach (var id in aOrderedChildIds)
            {
                if (!string.IsNullOrEmpty(id)) mChildIds.Add(id);
            }
        }

        // 指定した添字の枝が採用済みかを表すキー
        private string AdoptedKey(int aIndex) => mNodeId + AdoptedKeySuffix + aIndex;

        /* 評価・判定 */

        // まだ採用していない子を上から順に評価し、最初に確定した結果を返す
        protected override PPUnitAINodeResult EvaluateCore(PPUnitAIEvalContext aContext)
        {
            int depth = aContext.Path.Count;
            // 引き返した枝の通過記録を残さないため、子を試す前の長さを覚えておく
            int visitedMark = aContext.VisitedNodeIds.Count;

            for (int i = 0; i < mChildIds.Count; i++)
            {
                // この思考で既に採った枝は飛ばす。これが「連携」の本体になる
                if (aContext.IsAdopted(AdoptedKey(i))) continue;

                var child = aContext.ResolveNode(mChildIds[i]);
                if (child == null) continue;

                aContext.Path.Add(i);
                var result = child.Evaluate(aContext);
                if (result.IsDecided)
                {
                    // 確定した枝だけを採用済みとして積む。次の手ではここが飛ばされる
                    aContext.PushVisited(AdoptedKey(i));
                    return result;
                }

                aContext.Path.RemoveRange(depth, aContext.Path.Count - depth);
                aContext.TrimVisited(visitedMark);
            }
            return PPUnitAINodeResult.Failed;
        }
    }

    // 指定した確率でだけ子へ進むノード。外れた場合は不成立となり、親の次の候補へ渡る
    // 乱数は行動するユニット自身の供給元を経由する
    [Serializable]
    [PPTypeMenuName("制御/確率ゲート")]
    public sealed class PPUnitAIProbabilityNode : PPUnitAINode
    {
        /* インスペクタ設定 */

        [Header("確率")]
        // 子へ進む確率。0 なら決して通さず、1 なら常に通す
        [Label("通す確率")]
        [Range(0f, 1f)]
        [SerializeField] private float mProbability = 0.5f;

        [Header("枝")]
        [Label("通ったとき")]
        [SerializeField] private string mChildId = "";

        /* プロパティ */

        // 子へ進む確率。エディタのサマリ表示から参照する
        public float Probability => mProbability;
        // 通す確率を百分率で示す
        public override string Summary => $"{Mathf.RoundToInt(mProbability * 100f)}% で通す";

        public override IReadOnlyList<PPUnitAINodePort> Ports
            => new[]
            {
                new PPUnitAINodePort("通過", string.IsNullOrEmpty(mChildId)
                    ? Array.Empty<string>()
                    : new[] { mChildId }, false),
            };

        protected override string DefaultNodeName => "確率ゲート";

        /* 初期化・接続 */

        // 指定した接続口へ子ノードを繋ぐ。既に繋がっていた場合は置き換える
        public override void ConnectChild(int aPortIndex, string aChildId) => mChildId = aChildId;

        // 指定した接続口の接続を外す
        public override void DisconnectChild(int aPortIndex, string aChildId)
        {
            if (mChildId == aChildId) mChildId = "";
        }

        /* 操作 */

        // 接続先の子ノード ID を対応表に従って置き換える
        // aMap : 対応表
        public override void RemapChildIds(IReadOnlyDictionary<string, string> aMap)
            => mChildId = RemapChildId(mChildId, aMap);

        /* 評価・判定 */

        // 抽選に通った場合だけ子を評価する
        protected override PPUnitAINodeResult EvaluateCore(PPUnitAIEvalContext aContext)
        {
            var child = aContext.ResolveNode(mChildId);
            if (child == null) return PPUnitAINodeResult.Failed;

            // 確率が振り切れている設定では乱数を消費しない
            // 消費するとシードの進み方が設定値に左右され、他の抽選の出目まで変わってしまうため
            if (mProbability <= 0f) return PPUnitAINodeResult.Failed;
            if (mProbability < 1f && aContext.Unit.ResolveRandom(aContext.Battle).NextFloat() >= mProbability)
                return PPUnitAINodeResult.Failed;

            return child.Evaluate(aContext);
        }
    }

    // 別の判断ツリー（PPUnitBehaviourTree）へ評価をそのまま委ねるノード。定番の枝を複数のツリーで使い回すためのもの
    // 評価中は子ノードの ID 解決先を参照先へ切り替え、循環参照を検出したら打ち切る
    [Serializable]
    [PPTypeMenuName("制御/サブツリー参照")]
    public sealed class PPUnitAISubTreeNode : PPUnitAINode
    {
        /* インスペクタ設定 */

        [Header("参照先")]
        [Label("判断ツリー")]
        [SerializeField] private PPUnitBehaviourTree mSubTree;

        /* 状態 */

        // 循環参照の警告を既に出したか
        // 評価は思考のたびに走るため、同じ警告でコンソールが埋まらないよう抑止する
        [NonSerialized] private bool mIsWarnedCycle;

        /* プロパティ */

        // 参照先が設定されているか。エディタの診断から参照する
        public bool HasSubTree => mSubTree != null;
        // 参照先の判断ツリー。エディタの診断が循環参照を辿るために参照する
        public PPUnitBehaviourTree SubTree => mSubTree;
        // 参照先のアセット名を示す
        public override string Summary => mSubTree != null ? mSubTree.name : "参照先が未設定";
        protected override string DefaultNodeName
            => mSubTree != null ? $"参照 : {mSubTree.name}" : "サブツリー参照";

        /* 評価・判定 */

        // 参照先のルートへ評価を渡し、その結果をそのまま返す
        protected override PPUnitAINodeResult EvaluateCore(PPUnitAIEvalContext aContext)
        {
            if (mSubTree == null) return PPUnitAINodeResult.Failed;

            if (!aContext.TryPushTree(mSubTree))
            {
                if (!mIsWarnedCycle)
                {
                    mIsWarnedCycle = true;
                    CustomConsoleLog.Warning("AI",
                        $"「{NodeName}」で判断ツリー「{mSubTree.name}」が循環参照しているため、この枝を打ち切りました。");
                }
                return PPUnitAINodeResult.Failed;
            }

            try
            {
                return mSubTree.Evaluate(aContext);
            }
            finally
            {
                // 参照先が不成立で終わっても必ず元のツリーへ戻す
                // 戻し忘れると、以降の兄弟ノードが参照先のツリーから子を引こうとして全て不成立になる
                aContext.PopTree();
            }
        }
    }
}
