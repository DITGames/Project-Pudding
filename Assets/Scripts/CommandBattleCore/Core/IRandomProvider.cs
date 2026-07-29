/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IRandomProvider.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief バトル中の全乱数の供給コア
 * =====================================*/
using UnityEngine;

namespace CommandBattleCore
{
    /// <summary>
    /// バトル中の乱数供給元。
    /// <para>
    /// 命中判定・AI の思考・代替ターゲット選択など、バトル中の乱数は全てここを経由させる。
    /// 供給元を 1 箇所に集約しておくことで、シードを固定した再現テストやリプレイが可能になる。
    /// <c>UnityEngine.Random</c> を直接呼ぶとこの前提が崩れるので使わないこと。
    /// </para>
    /// </summary>
    public interface IRandomProvider
    {
        /// <summary>0 以上 <paramref name="aMaxExclusive"/> 未満の整数を返す。</summary>
        /// <param name="aMaxExclusive">上限（この値自体は含まない）。</param>
        int NextInt(int aMaxExclusive);
        /// <summary>指定範囲の整数を返す。</summary>
        /// <param name="aMinInclusive">下限（含む）。</param>
        /// <param name="aMaxExclusive">上限（含まない）。</param>
        int NextInt(int aMinInclusive, int aMaxExclusive);
        /// <summary>0 以上 1 未満の実数を返す。</summary>
        float NextFloat();
        /// <summary>指定範囲の実数を返す。</summary>
        /// <param name="aMinInclusive">下限（含む）。</param>
        /// <param name="aMaxExclusive">上限（含まない）。</param>
        float NextFloat(float aMinInclusive, float aMaxExclusive);
        /// <summary>指定確率で true を返す。</summary>
        /// <param name="aTrueChance">true になる確率（0～1）。</param>
        bool NextBool(float aTrueChance);
    }

    /// <summary>
    /// <see cref="System.Random"/> を用いた標準の乱数供給実装。
    /// シードを与えれば同じ乱数列を再現できる。
    /// </summary>
    public class DefaultRandomProvider : IRandomProvider
    {
        /// <summary>乱数生成器の実体。</summary>
        protected readonly System.Random mRng;

        /// <param name="aSeed">固定シード。null なら時刻由来のシードで初期化する。</param>
        public DefaultRandomProvider(int? aSeed = null)
            => mRng = aSeed.HasValue ? new System.Random(aSeed.Value) : new System.Random();

        /// <summary>0 以上 <paramref name="aMaxExclusive"/> 未満の整数を返す。</summary>
        public int NextInt(int aMaxExclusive) => mRng.Next(aMaxExclusive);
        /// <summary>指定範囲の整数を返す。</summary>
        public int NextInt(int aMinInclusive, int aMaxExclusive)  => mRng.Next(aMinInclusive, aMaxExclusive);
        /// <summary>0 以上 1 未満の実数を返す。</summary>
        public float NextFloat() => (float)mRng.NextDouble();
        /// <summary>指定範囲の実数を返す。</summary>
        public float NextFloat(float aMinInclusive, float aMaxExclusive) => aMinInclusive + (float)mRng.NextDouble() * (aMaxExclusive - aMinInclusive);
        /// <summary>指定確率で true を返す。</summary>
        public bool NextBool(float aTrueChance) => mRng.NextDouble() < aTrueChance;
    }
}
