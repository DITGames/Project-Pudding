/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPResourceCost.cs
 * @author hqrse
 * @date 2026/07/18
 * @brief リソースコスト
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Linq;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    /// <summary>
    /// インスペクタで「どの属性をいくつ消費するか」を 1 件設定するための構造体。
    /// スキル定義はこれを配列で持ち、実行時に <see cref="PPResourceCost"/> へ変換する。
    /// </summary>
    [Serializable]
    public struct PPResourceAmount
    {
        /// <summary>消費するリソースの属性。</summary>
        [Label("リソース種別")]
        public PPTypeAttribute Type;
        /// <summary>必要量。</summary>
        [Label("必要量")]
        public float Amount;
    }

    /// <summary>
    /// スキル 1 つ分の消費リソースを表す不変オブジェクト。
    /// <para>
    /// 属性ごとの必要量を固定長配列で保持する。添字は <see cref="PPTypeAttribute"/> と対応し、
    /// 生成時に合計値を求めておくことで無コスト判定とコスト効率計算を軽くしている。
    /// 生成はファクトリメソッド経由のみ。
    /// </para>
    /// </summary>
    public sealed class PPResourceCost
    {
        /// <summary>属性ごとの必要量。添字は <see cref="PPTypeAttribute"/> と対応する。</summary>
        private readonly float[] mAmounts;
        /// <summary>全属性の必要量の合計。AI のコスト効率計算に使う。</summary>
        public float Total { get; }
        /// <summary>コストを必要としないか。</summary>
        public bool IsFree => Total == 0;

        /// <param name="aAmounts">属性ごとの必要量。</param>
        /// <param name="aTotal">必要量の合計。</param>
        private PPResourceCost(float[] aAmounts, float aTotal)
        {
            mAmounts = aAmounts;
            Total = aTotal;
        }

        /// <summary>添字を指定して必要量を取得する。</summary>
        /// <param name="aIndex">属性のインデックス。</param>
        public float Get(int aIndex) => mAmounts[aIndex];
        /// <summary>属性を指定して必要量を取得する。</summary>
        /// <param name="a">対象の属性。</param>
        public float Get(PPTypeAttribute a) => mAmounts[(int)a];

        /// <summary>
        /// 手持ちが <paramref name="aAmount"/> あるとき、この属性の必要量を支払えるかを返す。
        /// 必要量 0 の属性は手持ちが 0 でも支払えるものとして true になる。
        /// </summary>
        /// <param name="a">対象の属性。</param>
        /// <param name="aAmount">支払いに使える手持ちの量。</param>
        /// <returns>支払える場合 true。</returns>
        public bool CanPay(PPTypeAttribute a, float aAmount)
        {
            return mAmounts[(int)a] <= aAmount;
        }

        /// <summary>
        /// 実際に消費が発生する属性だけを列挙する。
        /// UI でコスト表示を作る際に、必要量 0 の属性を省くために使う。
        /// </summary>
        /// <returns>必要量が 1 以上ある属性のリスト。</returns>
        public IReadOnlyList<PPTypeAttribute> RelevantTypes()
        {
            List<PPTypeAttribute> res = new List<PPTypeAttribute>();
            for (int i = 0; i < mAmounts.Length; i++)
            {
                if (mAmounts[i] > 0)
                {
                    res.Add((PPTypeAttribute)i);
                }
            }
            return res;
        }

        /// <summary>
        /// インスペクタ設定用のエントリ配列からコストを構築する。
        /// 同じ属性が複数回指定された場合は合算し、負値は 0 として扱う。
        /// </summary>
        /// <param name="aEntries">属性と必要量の組。null なら無コストになる。</param>
        /// <returns>構築されたコスト。</returns>
        public static PPResourceCost From(IEnumerable<PPResourceAmount> aEntries)
        {
            var arr = new float[PPTypeAttributeDefinition.TypeCount];
            float total = 0;
            if (aEntries != null)
            {
                foreach (var e in aEntries)
                {
                    float v = Mathf.Max(0f, e.Amount);
                    arr[(int)e.Type] += v;
                    total += v;
                }
            }
            return new PPResourceCost(arr, total);
        }

        /// <summary>単一属性のみを消費するコストを作る。</summary>
        /// <param name="a">消費する属性。</param>
        /// <param name="aAmount">必要量。</param>
        public static PPResourceCost Single(PPTypeAttribute a, float aAmount)
        => From(new[]{new PPResourceAmount{Type = a, Amount = aAmount}});

        /// <summary>基準リソース（ノーマル）のみを消費するコストを作る。通常攻撃のコストに使う。</summary>
        /// <param name="aAmount">必要量。</param>
        public static PPResourceCost BaseCost(float aAmount)
        => Single(PPTypeAttribute.Normal, aAmount);

        /// <summary>無コストを表す共有インスタンス。</summary>
        public static readonly PPResourceCost Free = new PPResourceCost(new float[PPTypeAttributeDefinition.TypeCount], 0f);
    }
}
