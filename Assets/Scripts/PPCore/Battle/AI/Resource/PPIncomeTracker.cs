/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPIncomeTracker.cs
 * @author hqrse
 * @date 2026/08/10
 * @brief 属性別のリソース収入ペースを記録するトラッカー
 * =====================================*/

using System.Collections.Generic;
using UnityEngine;

namespace PPCore
{
    // 属性ごとの収入ペースを記録し、平均とばらつきを供給するトラッカー
    // 旧 PPIncomTrendTracker との違いは次の 3 点で、AI の溜め判断の精度を上げるためにある
    //   1. 残量の差分ではなく PPBattleResourcePool.OnResourceGained の収入を直接記録する
    //      差分観測だと、同じ区間で収入と消費が同時に起きたときに収入が見えず、推定が常に過小へ歪む
    //   2. 基準リソースだけでなく全属性を追う。属性別コストのスキルを狙って溜める判断に必要
    //   3. 平均に加えて標準偏差を持つ。プッシャー収入はコインが固まって落ちるバースト性があり、
    //      平均だけでは「あと何ティックで撃てるか」の見積もりが当てにならない
    // 区間の確定（CommitInterval）はティックに同期させる。思考間隔に同期させると、
    // 思考間隔を変えただけで溜め判断のスケールが変わり、調整値が持ち運べなくなる
    public sealed class PPIncomeTracker
    {
        // 属性ごとの区間収入の履歴。古いものから順に捨てられる
        private readonly Queue<float>[] mSamples;
        // 属性ごとの、現在の区間で得た収入の累積
        private readonly float[] mPending;
        // 購読中のリソースプール。二重購読を避けるため参照を保持する
        private PPBattleResourcePool mPool;

        public PPIncomeTracker()
        {
            mSamples = new Queue<float>[PPTypeAttributeDefinition.TypeCount];
            mPending = new float[PPTypeAttributeDefinition.TypeCount];
            for (int i = 0; i < mSamples.Length; i++)
            {
                mSamples[i] = new Queue<float>();
            }
        }

        // リソースプールの収入イベントを購読する
        // 既に別のプールを購読している場合は、先に解除してから繋ぎ直す
        // aPool : 購読対象のプール
        public void Bind(PPBattleResourcePool aPool)
        {
            if (ReferenceEquals(mPool, aPool))
                return;

            Unbind();
            mPool = aPool;
            if (mPool != null)
            {
                mPool.OnResourceGained += HandleResourceGained;
            }
        }

        // 購読を解除する。バトル終了時に呼び出して参照を残さないようにする
        public void Unbind()
        {
            if (mPool == null)
                return;

            mPool.OnResourceGained -= HandleResourceGained;
            mPool = null;
        }

        // 現在の区間を確定して履歴へ送り、次の区間の集計を始める
        // 収入が無かった区間も 0 として記録する。記録しないと「収入が途絶えた」ことが平均に反映されない
        // aSampleCount : 保持するサンプル数。最低 1 件は保持する
        public void CommitInterval(int aSampleCount)
        {
            int capacity = Mathf.Max(1, aSampleCount);
            for (int i = 0; i < mSamples.Length; i++)
            {
                mSamples[i].Enqueue(mPending[i]);
                mPending[i] = 0f;

                while (mSamples[i].Count > capacity)
                {
                    mSamples[i].Dequeue();
                }
            }
        }

        // 指定属性の 1 区間あたりの平均収入。サンプルが無ければ 0
        // a : 対象の属性
        public float AverageGain(PPTypeAttribute a)
        {
            var queue = mSamples[(int)a];
            if (queue.Count == 0)
                return 0f;

            float sum = 0f;
            foreach (var v in queue) sum += v;
            return sum / queue.Count;
        }

        // 指定属性の収入の標準偏差。サンプルが 2 件未満なら 0
        // a : 対象の属性
        public float StdDevGain(PPTypeAttribute a)
        {
            var queue = mSamples[(int)a];
            if (queue.Count < 2)
                return 0f;

            float average = AverageGain(a);
            float sumOfSquares = 0f;
            foreach (var v in queue)
            {
                float diff = v - average;
                sumOfSquares += diff * diff;
            }
            return Mathf.Sqrt(sumOfSquares / queue.Count);
        }

        // 下振れを見込んだ保守的な収入見積もりを返す
        // 警戒度が高いほど標準偏差を大きく割り引くため、収入がばらつく状況では
        // 「待てば撃てる」と楽観しにくくなる
        // a : 対象の属性
        // aCaution : 警戒度（0〜1）。0 なら平均をそのまま使い、1 なら標準偏差 1 つ分を引く
        // return : 0 以上の見積もり値
        public float ConservativeGain(PPTypeAttribute a, float aCaution)
            => Mathf.Max(0f, AverageGain(a) - StdDevGain(a) * Mathf.Clamp01(aCaution));

        // 収入イベントのハンドラ。現在の区間へ加算するだけに留める
        // aType : 増えた属性
        // aAmount : 実際に増えた量
        private void HandleResourceGained(PPTypeAttribute aType, float aAmount)
        {
            if (aAmount <= 0f)
                return;

            mPending[(int)aType] += aAmount;
        }
    }
}
