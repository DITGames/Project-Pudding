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
    // 子ノードを上から順に評価し、最初に行動が確定したものを採用するノード
    // 判断ツリーの背骨にあたる。並び順がそのまま優先度になる
    // どの子も確定しなければ自身も不成立となり、さらに上の階層へ戻る
    //
    // 待機コミット中は「待ちを宣言した枝より下」を評価しない
    // 溜めると決めたのに次のティックで下位の枝へ流れてしまうと、待ちが成立しないため
    // 逆に、宣言した枝より上（＝優先度が高い）はそのまま評価されるので、
    // 緊急度の高い行動を上に置いておけば自然に割り込める
    [Serializable]
    [PPTypeMenuName("制御/優先度リスト")]
    public sealed class PPUnitAISelectorNode : PPUnitAINode
    {
        // 接続している子ノードの ID。並び順がそのまま優先度になる
        [Header("子ノード")]
        [Label("上から順に試す", true)]
        [SerializeField] private List<string> mChildIds = new();

        protected override string DefaultNodeName => "優先度リスト";

        public override IReadOnlyList<PPUnitAINodePort> Ports
            => new[] { new PPUnitAINodePort("子ノード", mChildIds, true) };

        // 上から何番目まで並んでいるかを示す。並び順がそのまま優先度になる
        public override string Summary => $"{mChildIds.Count} 件を上から順に試す";

        // 子を上から評価し、最初に確定した結果をそのまま返す
        // 確定した場合は道順（PPUnitAIEvalContext.Path）に自分の子の添字を残す
        // aContext : 評価 1 回分の入力
        // return : 確定した行動。どの子も確定しなければ Failed
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

        // 子ノードを末尾へ繋ぐ。既に繋がっている場合は何もしない
        // aPortIndex : 接続口の番号。このノードは 1 口のみ
        // aChildId : 繋ぐ子ノードの ID
        public override void ConnectChild(int aPortIndex, string aChildId)
        {
            if (string.IsNullOrEmpty(aChildId) || mChildIds.Contains(aChildId)) return;

            mChildIds.Add(aChildId);
        }

        // 接続先の子ノード ID を対応表に従って置き換える
        // aMap : 対応表
        public override void RemapChildIds(IReadOnlyDictionary<string, string> aMap) => RemapChildIds(mChildIds, aMap);

        // 子ノードとの接続を外す
        // aPortIndex : 接続口の番号
        // aChildId : 外す子ノードの ID
        public override void DisconnectChild(int aPortIndex, string aChildId) => mChildIds.Remove(aChildId);

        // 子ノードの並び順を指定どおりに揃える
        // エディタ上の配置（上にあるものほど優先）をそのまま優先度へ反映するために使う
        // aPortIndex : 接続口の番号
        // aOrderedChildIds : 並べ替え後の子ノード ID
        public override void ReorderChildren(int aPortIndex, IReadOnlyList<string> aOrderedChildIds)
        {
            mChildIds.Clear();
            foreach (var id in aOrderedChildIds)
            {
                if (!string.IsNullOrEmpty(id)) mChildIds.Add(id);
            }
        }
    }

    // 同じティックの中で、子を上から順に 1 手ずつ消化していくノード
    //
    // 1 ティックに複数回行動できるユニットは、2 手目も根から評価し直すため同じ枝を再び引きうる
    // そのままでは「1 手目バフ → 2 手目攻撃」を意図して書けないため、
    // 一度採用した子を同じティックの中では飛ばす、という並び方をこのノードで表現する
    //
    // ティックが変われば先頭へ戻る。行動回数が 1 のユニットでは常に先頭の子だけが使われる
    // 子をすべて採用し終えた状態でまだ行動回数が残っている場合は不成立となり、親の次の候補へ処理が渡る
    //
    // 採用済みかどうかの記録はストラテジストが 1 回の思考分だけ持つ（仮押さえ台帳と同じ寿命）
    // ノードの評価がバトルの状態を変えない、という約束を守るため、
    // 採用の記録はここでは行わず、行動が確定した時点でストラテジストが書き込む
    [Serializable]
    [PPTypeMenuName("制御/連携")]
    public sealed class PPUnitAISequenceNode : PPUnitAINode
    {
        // 採用済みの記録に使うキーの接尾辞。ノード自身の通過記録と区別するために付ける
        private const string AdoptedKeySuffix = "#seq:";

        // 接続している子ノードの ID。並び順がそのまま消化する順番になる
        [Header("子ノード")]
        [Label("上から順に消化する", true)]
        [SerializeField] private List<string> mChildIds = new();

        protected override string DefaultNodeName => "連携";

        // 接続している子ノードの ID。エディタのサマリ表示から参照する
        public IReadOnlyList<string> ChildIds => mChildIds;

        public override IReadOnlyList<PPUnitAINodePort> Ports
            => new[] { new PPUnitAINodePort("子ノード", mChildIds, true) };

        // 何手ぶんの連携かを示す
        public override string Summary => $"{mChildIds.Count} 件を同じティック内で順に消化";

        // まだ採用していない子を上から順に評価し、最初に確定した結果を返す
        // aContext : 評価 1 回分の入力
        // return : 確定した行動。採用できる子が無ければ Failed
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

        // 子ノードを末尾へ繋ぐ。既に繋がっている場合は何もしない
        // aPortIndex : 接続口の番号。このノードは 1 口のみ
        // aChildId : 繋ぐ子ノードの ID
        public override void ConnectChild(int aPortIndex, string aChildId)
        {
            if (string.IsNullOrEmpty(aChildId) || mChildIds.Contains(aChildId)) return;

            mChildIds.Add(aChildId);
        }

        // 接続先の子ノード ID を対応表に従って置き換える
        // aMap : 対応表
        public override void RemapChildIds(IReadOnlyDictionary<string, string> aMap) => RemapChildIds(mChildIds, aMap);

        // 子ノードとの接続を外す
        // aPortIndex : 接続口の番号
        // aChildId : 外す子ノードの ID
        public override void DisconnectChild(int aPortIndex, string aChildId) => mChildIds.Remove(aChildId);

        // 子ノードの並び順を指定どおりに揃える
        // エディタ上の配置（上にあるものほど先に消化する）をそのまま順番へ反映するために使う
        // aPortIndex : 接続口の番号
        // aOrderedChildIds : 並べ替え後の子ノード ID
        public override void ReorderChildren(int aPortIndex, IReadOnlyList<string> aOrderedChildIds)
        {
            mChildIds.Clear();
            foreach (var id in aOrderedChildIds)
            {
                if (!string.IsNullOrEmpty(id)) mChildIds.Add(id);
            }
        }

        // 指定した添字の枝が採用済みかを表すキー
        // aIndex : 対象の枝の添字
        // return : 採用済みの記録に使うキー
        private string AdoptedKey(int aIndex) => mNodeId + AdoptedKeySuffix + aIndex;
    }

    // 指定した確率でだけ子へ進むノード
    //
    // 抽選ノードが「複数の枝からどれか 1 つを引く」のに対して、こちらは 1 本の枝を通すかどうかだけを決める
    // 「たまに挑発してくる」「3 割の確率で溜める」のような、揺らぎを足すためのもの
    // 外れた場合は不成立となり、親の次の候補へ処理が渡る
    //
    // 乱数はシード管理・再現性のため、行動するユニット自身の供給元を経由する
    [Serializable]
    [PPTypeMenuName("制御/確率ゲート")]
    public sealed class PPUnitAIProbabilityNode : PPUnitAINode
    {
        [Header("確率")]
        // 子へ進む確率。0 なら決して通さず、1 なら常に通す
        [Label("通す確率")]
        [Range(0f, 1f)]
        [SerializeField] private float mProbability = 0.5f;

        [Header("枝")]
        [Label("通ったとき")]
        [SerializeField] private string mChildId = "";

        protected override string DefaultNodeName => "確率ゲート";

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

        // 抽選に通った場合だけ子を評価する
        // aContext : 評価 1 回分の入力
        // return : 子の評価結果。外れた場合と枝が無い場合は Failed
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

        // 指定した接続口へ子ノードを繋ぐ。既に繋がっていた場合は置き換える
        // aPortIndex : 接続口の番号。このノードは 1 口のみ
        // aChildId : 繋ぐ子ノードの ID
        public override void ConnectChild(int aPortIndex, string aChildId) => mChildId = aChildId;

        // 接続先の子ノード ID を対応表に従って置き換える
        // aMap : 対応表
        public override void RemapChildIds(IReadOnlyDictionary<string, string> aMap)
            => mChildId = RemapChildId(mChildId, aMap);

        // 指定した接続口の接続を外す
        // aPortIndex : 接続口の番号
        // aChildId : 外す子ノードの ID
        public override void DisconnectChild(int aPortIndex, string aChildId)
        {
            if (mChildId == aChildId) mChildId = "";
        }
    }

    // 別の判断ツリー（PPUnitBehaviourTree）へ評価をそのまま委ねるノード
    //
    // 「瀕死の味方を回復する」「弱点を突く」といった定番の枝を 1 つのアセットへ切り出し、
    // 複数のユニットのツリーから同じものを指して使い回すためのもの
    //
    // 評価は参照先のルートへ渡り、その結果をそのまま自分の結果として返す
    // 参照先で行動が確定しなければ自分も不成立となり、親の優先度リストが次の候補へ進む
    //
    // 参照先を評価している間は、子ノードの ID 解決先も参照先のツリーへ切り替わる
    // ノード ID はツリーごとに閉じているため、切り替えないと参照先の子を引けないまま不成立になる
    //
    // 循環参照（自分自身や、辿ると自分へ戻ってくるツリー）を踏むと評価が無限に潜るため、
    // 評価中のツリーを追跡して検出したらその場で打ち切る
    // エディタ側の診断でも同じ形を警告するが、アセットは実行時に差し替えられるためランタイム側にも歯止めを置く
    [Serializable]
    [PPTypeMenuName("制御/サブツリー参照")]
    public sealed class PPUnitAISubTreeNode : PPUnitAINode
    {
        [Header("参照先")]
        [Label("判断ツリー")]
        [SerializeField] private PPUnitBehaviourTree mSubTree;

        // 循環参照の警告を既に出したか
        // 評価は思考のたびに走るため、抑止しないと同じ警告でコンソールが埋まり他のトレースが読めなくなる
        // 診断のためだけのフラグでバトルの状態は変えないため、「ノードは状態を変えない」約束とは衝突しない
        [NonSerialized] private bool mIsWarnedCycle;

        protected override string DefaultNodeName
            => mSubTree != null ? $"参照 : {mSubTree.name}" : "サブツリー参照";

        // 参照先が設定されているか。エディタの診断から参照する
        public bool HasSubTree => mSubTree != null;

        // 参照先の判断ツリー。エディタの診断が循環参照を辿るために参照する
        public PPUnitBehaviourTree SubTree => mSubTree;

        // 参照先のアセット名を示す
        public override string Summary => mSubTree != null ? mSubTree.name : "参照先が未設定";

        // 参照先のルートへ評価を渡し、その結果をそのまま返す
        // aContext : 評価 1 回分の入力
        // return : 参照先が確定させた行動。未設定・循環・参照先が不成立なら Failed
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
