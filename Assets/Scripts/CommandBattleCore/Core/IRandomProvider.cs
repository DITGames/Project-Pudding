/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IRandomProvider.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief バトル中の全乱数の供給コア
 * =====================================*/

using System;

namespace CommandBattleCore
{
    // バトル中の乱数供給元
    // 命中判定・AI の思考・代替ターゲット選択など、バトル中の乱数は全てここを経由させる
    // 供給元を 1 箇所に集約しておくことで、シードを固定した再現テストやリプレイが可能になる
    // UnityEngine.Random を直接呼ぶとこの前提が崩れるので使わないこと
    public interface IRandomProvider
    {
        // 0 以上 aMaxExclusive 未満の整数を返す
        // aMaxExclusive : 上限（この値自体は含まない）
        int NextInt(int aMaxExclusive);
        // 指定範囲の整数を返す
        // aMinInclusive : 下限（含む）
        // aMaxExclusive : 上限（含まない）
        int NextInt(int aMinInclusive, int aMaxExclusive);
        // 0 以上 1 未満の実数を返す
        float NextFloat();
        // 指定範囲の実数を返す
        // aMinInclusive : 下限（含む）
        // aMaxExclusive : 上限（含まない）
        float NextFloat(float aMinInclusive, float aMaxExclusive);
        // 指定確率で true を返す
        // aTrueChance : true になる確率（0～1）
        bool NextBool(float aTrueChance);
    }

    // Random を用いた標準の乱数供給実装。シードを与えれば同じ乱数列を再現できる
    public class DefaultRandomProvider : IRandomProvider
    {
        // 乱数生成器の実体
        protected readonly Random mRng;

        // aSeed : 固定シード。null なら時刻由来のシードで初期化する
        public DefaultRandomProvider(int? aSeed = null)
            => mRng = aSeed.HasValue ? new Random(aSeed.Value) : new Random();

        // 0 以上 aMaxExclusive 未満の整数を返す
        public int NextInt(int aMaxExclusive) => mRng.Next(aMaxExclusive);
        // 指定範囲の整数を返す
        public int NextInt(int aMinInclusive, int aMaxExclusive)  => mRng.Next(aMinInclusive, aMaxExclusive);
        // 0 以上 1 未満の実数を返す
        public float NextFloat() => (float)mRng.NextDouble();
        // 指定範囲の実数を返す
        public float NextFloat(float aMinInclusive, float aMaxExclusive) => aMinInclusive + (float)mRng.NextDouble() * (aMaxExclusive - aMinInclusive);
        // 指定確率で true を返す
        public bool NextBool(float aTrueChance) => mRng.NextDouble() < aTrueChance;
    }
}
