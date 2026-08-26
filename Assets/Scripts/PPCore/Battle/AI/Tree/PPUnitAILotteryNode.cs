/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAILotteryNode.cs
 * @author hqrse
 * @date 2026/08/25
 * @brief 子ノードを重み付き抽選で選ぶノード
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // 抽選ノードが持つ枝 1 本分の設定
    // 「どの子ノードを」「どれくらいの当たりやすさで」引くかの組
    // 重みを親側に集めることで、配分をひとつの画面で見比べられるようにしている
    [Serializable]
    public sealed class PPUnitAILotteryEntry
    {
        // 抽選対象の子ノード ID。接続操作から設定されるため手で編集しない
        [HideInInspector]
        [SerializeField] private string mChildId = "";
        // 当たりやすさ。0 以下にするとその枝は引かれなくなる
        [SerializeField] private float mWeight = 1f;

        public string ChildId => mChildId;
        // 抽選の重み。負値は 0 として扱い、抽選対象から外す
        public float Weight => Mathf.Max(0f, mWeight);

        // aChildId : 抽選対象の子ノード ID
        public PPUnitAILotteryEntry(string aChildId) => mChildId = aChildId;

        // 抽選の重みを設定する。正規化など、まとめて書き換える処理から使う
        // aWeight : 設定する重み。負値は 0 に丸める
        public void SetWeight(float aWeight) => mWeight = Mathf.Max(0f, aWeight);
    }

    // 子ノードを重み付きの抽選で 1 つ選んで評価するノード
    //
    // 上から順に試す優先度リストに対して、こちらは毎回くじを引く
    // 同じ状況でも行動がばらけるため、読まれにくい相手を作るのに使う
    // 当たりやすさはこのノードが枝ごとに持ち、重み 0 の枝は引かれない
    //
    // 選んだ枝が実行できなかった場合は、既定では残りの枝から引き直す
    // 1 回のくじ引きで空振りしてそのティックを丸ごと捨てるより、
    // 「候補の中から実行できるものを重み付きで選ぶ」ほうが扱いやすいため
    [Serializable]
    [PPTypeMenuName("制御/抽選")]
    public sealed class PPUnitAILotteryNode : PPUnitAINode
    {
        [Header("子ノード")]
        // 抽選対象の枝。接続すると自動で追加され、外すと取り除かれる
        [Label("抽選対象", true)]
        [SerializeField] private List<PPUnitAILotteryEntry> mEntries = new();
        // 引いた枝が実行できなかったとき、残りの枝から引き直すか
        [Label("外れたら引き直す")]
        [SerializeField] private bool mIsRetryOnFail = true;

        // 接続口へ渡す子ノード ID の並び。エディタからの問い合わせのたびに組み直す
        private readonly List<string> mChildIdCache = new();

        protected override string DefaultNodeName => "抽選";

        public override IReadOnlyList<PPUnitAINodePort> Ports
        {
            get
            {
                mChildIdCache.Clear();
                foreach (var entry in mEntries)
                {
                    if (entry != null) mChildIdCache.Add(entry.ChildId);
                }
                return new[] { new PPUnitAINodePort("抽選対象", mChildIdCache, true) };
            }
        }

        // 子を重み付き抽選で選んで評価する
        // 確定した場合は道順（PPUnitAIEvalContext.Path）に選ばれた子の添字を残す
        // aContext : 評価 1 回分の入力
        // return : 確定した行動。どの枝も確定しなければ Failed
        public override PPUnitAINodeResult Evaluate(PPUnitAIEvalContext aContext)
        {
            int depth = aContext.Path.Count;

            // 待機コミット中はくじを引き直さず、前回選ばれた枝をそのまま辿る
            // 引き直すと維持するはずの判断が毎ティック変わってしまう
            if (aContext.IsOnCommitPath(depth))
            {
                var interrupted = EvaluateInterruptChildren(aContext, depth);
                if (interrupted.IsDecided) return interrupted;

                return EvaluateChild(aContext, depth, aContext.CommitChildIndex(depth));
            }

            // 引いた枝を候補から外しながら、実行できるものに当たるまで繰り返す
            var remaining = new List<int>();
            for (int i = 0; i < mEntries.Count; i++)
            {
                if (ResolveWeight(i) > 0f) remaining.Add(i);
            }

            while (remaining.Count > 0)
            {
                int picked = DrawIndex(aContext, remaining);
                if (picked < 0) break;

                var result = EvaluateChild(aContext, depth, remaining[picked]);
                if (result.IsDecided) return result;

                if (!mIsRetryOnFail) break;

                remaining.RemoveAt(picked);
            }
            return PPUnitAINodeResult.Failed;
        }

        // 割り込み指定のある子だけを順に評価する
        // 待機コミット中でも緊急度の高い枝へ抜けられるようにするためのもの
        // aContext : 評価 1 回分の入力
        // aDepth : このノードの深さ
        // return : 確定した行動。無ければ Failed
        private PPUnitAINodeResult EvaluateInterruptChildren(PPUnitAIEvalContext aContext, int aDepth)
        {
            for (int i = 0; i < mEntries.Count; i++)
            {
                var child = ResolveChild(aContext, i);
                if (child == null || !child.IsInterrupt) continue;

                var result = EvaluateChild(aContext, aDepth, i);
                if (result.IsDecided) return result;
            }
            return PPUnitAINodeResult.Failed;
        }

        // 指定した添字の子を評価する。確定しなかった場合は積んだ道順を戻す
        // aContext : 評価 1 回分の入力
        // aDepth : このノードの深さ
        // aIndex : 評価する子の添字
        // return : 子の評価結果
        private PPUnitAINodeResult EvaluateChild(PPUnitAIEvalContext aContext, int aDepth, int aIndex)
        {
            var child = ResolveChild(aContext, aIndex);
            if (child == null) return PPUnitAINodeResult.Failed;

            aContext.Path.Add(aIndex);
            var result = child.Evaluate(aContext);
            if (result.IsDecided) return result;

            aContext.Path.RemoveRange(aDepth, aContext.Path.Count - aDepth);
            return PPUnitAINodeResult.Failed;
        }

        // 残り候補から重みに応じて 1 つ引く
        // 乱数はシード管理・再現性のため、行動するユニット自身の供給元を経由する
        // aContext : 評価 1 回分の入力
        // aRemaining : 残っている候補の添字
        // return : aRemaining 内での位置。引けなければ -1
        private int DrawIndex(PPUnitAIEvalContext aContext, IReadOnlyList<int> aRemaining)
        {
            float total = 0f;
            foreach (int index in aRemaining)
            {
                total += ResolveWeight(index);
            }
            if (total <= 0f) return -1;

            float point = aContext.Unit.ResolveRandom(aContext.Battle).NextFloat() * total;
            for (int i = 0; i < aRemaining.Count; i++)
            {
                point -= ResolveWeight(aRemaining[i]);
                if (point <= 0f) return i;
            }
            // 浮動小数の誤差で最後まで引き切れなかった場合は末尾を返す
            return aRemaining.Count - 1;
        }

        // 指定した添字の枝が持つ抽選の重みを引く
        // aIndex : 対象の枝の添字
        // return : 抽選の重み。枝が無ければ 0
        private float ResolveWeight(int aIndex)
            => aIndex >= 0 && aIndex < mEntries.Count && mEntries[aIndex] != null ? mEntries[aIndex].Weight : 0f;

        // 指定した添字の枝が指す子ノードを引く
        // aContext : 評価 1 回分の入力
        // aIndex : 対象の枝の添字
        // return : 子ノード。解決できなければ null
        private PPUnitAINode ResolveChild(PPUnitAIEvalContext aContext, int aIndex)
        {
            if (aIndex < 0 || aIndex >= mEntries.Count || mEntries[aIndex] == null) return null;

            return aContext.ResolveNode(mEntries[aIndex].ChildId);
        }

        // 子ノードを末尾へ繋ぐ。既に繋がっている場合は何もしない
        // 重みの既定は 1 で、繋いだあとインスペクタから調整する
        // aPortIndex : 接続口の番号。このノードは 1 口のみ
        // aChildId : 繋ぐ子ノードの ID
        public override void ConnectChild(int aPortIndex, string aChildId)
        {
            if (string.IsNullOrEmpty(aChildId) || IndexOfChild(aChildId) >= 0) return;

            mEntries.Add(new PPUnitAILotteryEntry(aChildId));
        }

        // 子ノードとの接続を外す。枝ごと取り除くため重みも一緒に消える
        // aPortIndex : 接続口の番号
        // aChildId : 外す子ノードの ID
        public override void DisconnectChild(int aPortIndex, string aChildId)
        {
            int index = IndexOfChild(aChildId);
            if (index < 0) return;

            mEntries.RemoveAt(index);
        }

        // 枝の並び順を指定どおりに揃える
        // 抽選結果には影響しないが、エディタ上の並びと保存内容を合わせておく
        // 重みは枝に付いているため、並べ替えても配分は変わらない
        // aPortIndex : 接続口の番号
        // aOrderedChildIds : 並べ替え後の子ノード ID
        public override void ReorderChildren(int aPortIndex, IReadOnlyList<string> aOrderedChildIds)
        {
            var ordered = new List<PPUnitAILotteryEntry>(mEntries.Count);
            foreach (var id in aOrderedChildIds)
            {
                int index = IndexOfChild(id);
                if (index < 0) continue;

                ordered.Add(mEntries[index]);
            }

            // 並べ替え指定に含まれなかった枝は落とさず末尾へ残す
            foreach (var entry in mEntries)
            {
                if (entry != null && !ordered.Contains(entry)) ordered.Add(entry);
            }

            mEntries.Clear();
            mEntries.AddRange(ordered);
        }

        // 重みの合計が 1 になるよう割り直す
        //
        // 抽選は合計値に対する比率で行うため、正規化しても挙動は変わらない
        // 「3 : 1」を「0.75 : 0.25」に置き換えて、実際の確率を数字のまま読めるようにするためのもの
        // 端数は小数第 3 位で丸める。合計が厳密に 1 にならないことがあるが、抽選は毎回合計を取り直すため影響しない
        // 重みが全て 0 の場合は均等割りにする（0 のまま割ると全滅して引けなくなるため）
        public void NormalizeWeights()
        {
            int count = 0;
            float total = 0f;
            foreach (var entry in mEntries)
            {
                if (entry == null) continue;

                count++;
                total += entry.Weight;
            }
            if (count == 0) return;

            float average = 1f / count;
            foreach (var entry in mEntries)
            {
                if (entry == null) continue;

                float weight = total > 0f ? entry.Weight / total : average;
                entry.SetWeight(Mathf.Round(weight * 1000f) / 1000f);
            }
        }

        // 指定した子ノード ID を持つ枝の位置を探す
        // aChildId : 探す子ノードの ID
        // return : 枝の位置。見つからなければ -1
        private int IndexOfChild(string aChildId)
        {
            for (int i = 0; i < mEntries.Count; i++)
            {
                if (mEntries[i] != null && mEntries[i].ChildId == aChildId) return i;
            }
            return -1;
        }
    }
}
