/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPResourceCost.cs
 * @author hqrse
 * @date 2026/07/18
 * @brief リソースコスト
 * =====================================*/

using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // インスペクタで「どの属性をいくつ消費するか」を 1 件設定するための構造体
    // スキル定義はこれを配列で持ち、実行時に PPResourceCost へ変換する
    [Serializable]
    public struct PPResourceAmount
    {
        [Label("リソース種別")]
        public PPTypeAttribute Type;
        [Label("必要量")]
        public float Amount;
    }

    // スキル 1 つ分の消費リソースを表す不変オブジェクト
    // 属性ごとの必要量を固定長配列で保持する。添字は PPTypeAttribute と対応し、
    // 生成時に合計値を求めておくことで無コスト判定とコスト効率計算を軽くしている
    // 生成はファクトリメソッド経由のみ
    public sealed class PPResourceCost
    {
        // 属性ごとの必要量。添字は PPTypeAttribute と対応する
        private readonly float[] mAmounts;
        // 全属性の必要量の合計。AI のコスト効率計算に使う
        public float Total { get; }
        // コストを必要としないか
        public bool IsFree => Total == 0;

        // aAmounts : 属性ごとの必要量
        // aTotal : 必要量の合計
        private PPResourceCost(float[] aAmounts, float aTotal)
        {
            mAmounts = aAmounts;
            Total = aTotal;
        }

        // 添字を指定して必要量を取得する
        // aIndex : 属性のインデックス
        public float Get(int aIndex) => mAmounts[aIndex];
        // 属性を指定して必要量を取得する
        // a : 対象の属性
        public float Get(PPTypeAttribute a) => mAmounts[(int)a];

        // 手持ちが aAmount あるとき、この属性の必要量を支払えるかを返す
        // 必要量 0 の属性は手持ちが 0 でも支払えるものとして true になる
        // a : 対象の属性
        // aAmount : 支払いに使える手持ちの量
        // return : 支払える場合 true
        public bool CanPay(PPTypeAttribute a, float aAmount)
        {
            return mAmounts[(int)a] <= aAmount;
        }

        // 実際に消費が発生する属性だけを列挙する
        // UI でコスト表示を作る際に、必要量 0 の属性を省くために使う
        // return : 必要量が 1 以上ある属性のリスト
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

        // インスペクタ設定用のエントリ配列からコストを構築する
        // 同じ属性が複数回指定された場合は合算し、負値は 0 として扱う
        // aEntries : 属性と必要量の組。null なら無コストになる
        // return : 構築されたコスト
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

        // 単一属性のみを消費するコストを作る
        // a : 消費する属性
        // aAmount : 必要量
        public static PPResourceCost Single(PPTypeAttribute a, float aAmount)
        => From(new[]{new PPResourceAmount{Type = a, Amount = aAmount}});

        // 基準リソース（ノーマル）のみを消費するコストを作る。通常攻撃のコストに使う
        // aAmount : 必要量
        public static PPResourceCost BaseCost(float aAmount)
        => Single(PPTypeAttribute.Normal, aAmount);

        // 無コストを表す共有インスタンス
        public static readonly PPResourceCost Free = new PPResourceCost(new float[PPTypeAttributeDefinition.TypeCount], 0f);
    }
}
