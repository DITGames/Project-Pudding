/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPResourceBudget.cs
 * @author hqrse
 * @date 2026/07/16
 * @brief 共有プールを運用管理するためのクラス
 * =====================================*/
using UnityEngine;

namespace PPCore
{
    /// <summary>
    /// AI の思考中だけ使う、リソースプールの仮想残量。
    /// <para>
    /// パーティのリソースはユニット間で共有されるため、
    /// 「A が使ったら B は使えない」を思考の段階で反映する必要がある。
    /// 生成時にプールの現在値を写し取り、採用が決まるたびに
    /// <see cref="TrySpend"/> で仮想的に減らしていくことでこれを表現する。
    /// 実際のプールには一切手を触れない。
    /// </para>
    /// <para>
    /// 生成時に基準リソースの取り置き量を指定すると、その分は最初から使えない扱いになる。
    /// </para>
    /// </summary>
    public sealed class PPResourceBudget
    {
        /// <summary>属性ごとの仮想残量。</summary>
        private readonly float[] mRemaining;
        /// <summary>属性ごとの上限値。充足率の算出に使う。</summary>
        private readonly float[] mMax;

        /// <summary>
        /// プールの現在値を写し取って予算を作る。
        /// 取り置き量は基準リソース（ノーマル）にのみ適用される。
        /// </summary>
        /// <param name="aPool">写し元のリソースプール。</param>
        /// <param name="aBaseReserve">基準リソースの取り置き量。使わずに残しておきたい分。</param>
        public PPResourceBudget(PPBattleResourcePool aPool, float aBaseReserve = 0f)
        {
            mRemaining = new float[PPTypeAttributeDefinition.TypeCount];
            mMax = new float[PPTypeAttributeDefinition.TypeCount];
            for (int i = 0; i < PPTypeAttributeDefinition.TypeCount; i++)
            {
                var t = (PPTypeAttribute)i;
                float reserve = (i == PPTypeAttributeDefinition.BaseIndex) ? Mathf.Max(0f, aBaseReserve) : 0f;
                mRemaining[i] = Mathf.Max(0f, aPool.Current(t) - reserve);
                mMax[i] = aPool.Max(t);
            }
        }

        /// <summary>指定属性の仮想残量を取得する。</summary>
        /// <param name="a">対象の属性。</param>
        public float Remaining(PPTypeAttribute a)
        => mRemaining[(int)a];

        /// <summary>指定属性の充足率（残量 / 上限）を 0～1 で取得する。上限が 0 なら 0。</summary>
        /// <param name="a">対象の属性。</param>
        public float Fill(PPTypeAttribute a)
        => mMax[(int)a] > 0f ? mRemaining[(int)a] / mMax[(int)a] : 0f;

        /// <summary>
        /// 現在の仮想残量でコストを支払えるかを判定する。
        /// 全属性について必要量が残量以下であることを確認する。
        /// 浮動小数の誤差で「ちょうど足りている」が不足扱いにならないよう、微小値を足して比較する。
        /// </summary>
        /// <param name="aCost">判定するコスト。null または無コストなら常に true。</param>
        /// <returns>支払える場合 true。</returns>
        public bool CanAfford(PPResourceCost aCost)
        {
            if(aCost == null || aCost.IsFree)
                return true;
            for (int i = 0; i < PPTypeAttributeDefinition.TypeCount; i++)
            {
                if (!aCost.CanPay((PPTypeAttribute)i, mRemaining[i] + 0.0001f))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// コストを仮想残量から差し引く。割り当てに成功した場合のみ減算する。
        /// 実プールは変化しないため、実際の消費はコマンド実行時に別途行われる。
        /// </summary>
        /// <param name="aCost">支払うコスト。null または無コストなら何もせず true。</param>
        /// <returns>確保できた場合 true。足りなければ何も減らさず false。</returns>
        public bool TrySpend(PPResourceCost aCost)
        {
            if(aCost == null || aCost.IsFree)
                return true;
            if(!CanAfford(aCost))
                return false;
            for (int i = 0; i < PPTypeAttributeDefinition.TypeCount; i++)
            {
                mRemaining[i] = Mathf.Max(0f, mRemaining[i] - aCost.Get(i));
            }
            return true;
        }
    }
}
