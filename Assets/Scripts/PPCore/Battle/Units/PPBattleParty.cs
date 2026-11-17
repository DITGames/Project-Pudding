/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleParty.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief バトルパーティ（陣営・配置・パーティ・生成・セーブ供給元）
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PPCore
{
    // ユニットが属する陣営。ターゲット解決で「味方」「敵」を判断する基準になる
    public enum BattleSide
    {
        // プレイヤー側
        Ally,
        // 敵側
        Enemy,
    }

    // 片陣営分のユニットをまとめて保持するパーティ
    // アクティブメンバーと控えを分けて持ち、入れ替え・全滅判定を担う
    public class BattleParty
    {
        /* メンバー */

        // 戦場に出ているメンバー。全滅判定や生存確認はこちらを見る
        public List<BattleUnit> ActiveMembers { get; } = new();
        // 控えのメンバー。入れ替えでアクティブと交代する
        public List<BattleUnit> ReserveMembers { get; } = new();

        /* 通知 */

        // 入れ替えデリゲート（退場ユニット、参戦ユニット）
        public Action<BattleUnit, BattleUnit> OnSwapped { get; set; }

        /* 生成 */

        // メンバーを受け取ってパーティを構築する。同時に各ユニットへ陣営を設定する
        public BattleParty(BattleSide aSide, IEnumerable<BattleUnit> aActiveMembers,
            IEnumerable<BattleUnit> aReserveMembers = null)
        {
            foreach (var unit in aActiveMembers)
            {
                unit.Side = aSide;
                ActiveMembers.Add(unit);
            }

            if (aReserveMembers != null)
            {
                foreach (var unit in aReserveMembers)
                {
                    unit.Side = aSide;
                    ReserveMembers.Add(unit);
                }
            }
        }

        /* メンバー操作 */

        // アクティブメンバーとリザーブメンバーを双方向に入れ替える
        // 並び順を保つため、それぞれのリストの同じ位置に相手を差し込む
        public bool SwapMember(BattleUnit aActiveUnit, BattleUnit aReserveUnit)
        {
            int activeIdx = ActiveMembers.IndexOf(aActiveUnit);
            int reserveIdx = ReserveMembers.IndexOf(aReserveUnit);
            if (activeIdx < 0 || reserveIdx < 0) return false;

            ActiveMembers[activeIdx] = aReserveUnit;
            ReserveMembers[reserveIdx] = aActiveUnit;
            OnSwapped?.Invoke(aActiveUnit, aReserveUnit);
            return true;
        }

        /* 状態の問い合わせ */

        // 生きているアクティブメンバーを取得する
        // return : 生存中のアクティブメンバーのリスト
        public List<BattleUnit> GetAliveActiveMembers() => ActiveMembers.Where(u => u.IsAlive).ToList();

        // 全滅判定。アクティブメンバー全員が戦闘不能なら全滅とみなす（控えは数に入れない）
        // return : 全滅していれば true
        public virtual bool IsWiped() => ActiveMembers.All(u => !u.IsAlive);
    }

    // パーティ内の配置。定義順（Center → Right → Left）がコインを配る順番になる
    public enum PPPartyPosition
    {
        [InspectorName("中央")]
        Center,
        [InspectorName("右")]
        Right,
        [InspectorName("左")]
        Left,
    }

    // コイン変換係数を持ち、コインをメンバーへ順番に配るバトルパーティ
    // 行動リソースはユニットが専有するため、パーティ側は保持しない
    public class PPBattleParty : BattleParty
    {
        /* 状態 */

        // コイン 1 枚あたりのゲージ変換係数。PPCoinResourceBridge が参照する
        public Parameter CoinConversionRate { get; }
        // コインを配る順に並べたアクティブメンバー。配るたびに組み直して使い回す
        private readonly List<PPBattleUnit> mCoinOrder = new();
        // 直前にコインを受け取ったユニット。次のコインはこの次の順番から配る。null なら先頭から
        private PPBattleUnit mLastCoinReceiver;

        /* イベント */

        // コインを 1 枚以上配り終えたとき
        public event Action OnCoinsDistributed;

        /* 生成 */

        public PPBattleParty(float aBaseCoinRate, BattleSide aSide, IEnumerable<BattleUnit> aActiveMembers,
            IEnumerable<BattleUnit> aReserveMembers = null)
            : base(aSide, aActiveMembers, aReserveMembers)
        {
            CoinConversionRate = new Parameter(aBaseCoinRate);
        }

        /* 操作 */

        // コインを 1 枚ずつ、配置の順に生存ユニットのコインゲージへ配り、配れた枚数を返す
        // 前回の続きの順番から配り、倒れているユニットは飛ばす
        public int DistributeCoins(int aCoinCount, float aGaugePerCoin)
        {
            if (aCoinCount <= 0 || aGaugePerCoin <= 0f) return 0;

            RebuildCoinOrder();

            int given = 0;
            for (int i = 0; i < aCoinCount; i++)
            {
                var receiver = ResolveNextCoinReceiver();
                if (receiver == null) break;

                receiver.ExtraParameters.CoinGauge.Recover(aGaugePerCoin);
                mLastCoinReceiver = receiver;
                given++;
            }

            if (given > 0) OnCoinsDistributed?.Invoke();
            return given;
        }

        // アクティブメンバーを配置の順に並べ直す。同じ配置が重なった場合は編成順を保つ
        private void RebuildCoinOrder()
        {
            mCoinOrder.Clear();
            foreach (var member in ActiveMembers)
            {
                if (member is not PPBattleUnit unit) continue;

                int index = mCoinOrder.Count;
                while (index > 0 && mCoinOrder[index - 1].Position > unit.Position) index--;
                mCoinOrder.Insert(index, unit);
            }
        }

        // 直前の受け取り手の次から順に探し、最初に見つかった生存ユニットを返す。全員倒れていれば null
        private PPBattleUnit ResolveNextCoinReceiver()
        {
            int count = mCoinOrder.Count;
            int start = mLastCoinReceiver != null ? mCoinOrder.IndexOf(mLastCoinReceiver) : -1;
            for (int step = 1; step <= count; step++)
            {
                var unit = mCoinOrder[(start + step) % count];
                if (unit.IsAlive) return unit;
            }
            return null;
        }
    }

    // PPPartyDefinition などの生成元から PPBattleParty を組み立てるファクトリ
    public static class PPPartyFactory
    {
        // 配置の一覧。編成とコイン配布の順番（Center → Right → Left）で並べる
        private static readonly PPPartyPosition[] Positions =
            { PPPartyPosition.Center, PPPartyPosition.Right, PPPartyPosition.Left };

        // PPPartyDefinition からランタイムパーティを生成する
        // 配置の枠ごとにユニットを生成して編成し、それぞれへ専用の乱数供給元を割り当てる。空の枠は飛ばす
        public static PPBattleParty CreateFromDefinition(PPPartyDefinition aDefinition, BattleSide aSide)
        {
            var units = new List<BattleUnit>();
            foreach (var position in Positions)
            {
                var entry = aDefinition.GetMember(position);
                if(entry == null || entry.Unit == null) continue;

                var unit = (PPBattleUnit)entry.Unit.CreateRuntimeUnit(entry.Level);
                unit.Random = CreateRandom(entry, aSide, (int)position);
                unit.Position = position;
                units.Add(unit);
            }

            return new PPBattleParty(aDefinition.BaseResourceConversionRate, aSide, units);
        }

        // 編成 1 体分の乱数供給元を作る。単体の挙動を追えるよう、ユニットごとに別の乱数列を割り当てる
        // シードを固定する場合は、編成位置と陣営をシードへ混ぜる
        private static IRandomProvider CreateRandom(PPPartyMemberEntry aEntry, BattleSide aSide, int aIndex)
        {
            if (!aEntry.IsFixedSeed) return new DefaultRandomProvider();

            int seed = aEntry.Seed;
            seed = seed * 397 + aIndex;
            seed = seed * 397 + (int)aSide;
            return new DefaultRandomProvider(seed);
        }
    }

    // ユニット所持データ（セーブデータ）からパーティを生成するための抽象
    // 所持ユニットとレベルの一覧だけを定義する
    public interface IPPPartySaveSource
    {
        // 所持しているユニットとそのレベルの一覧を返す
        // return : 所持ユニットの一覧
        IReadOnlyList<PPPartySaveUnitEntry> GetOwnedUnits();
    }

    // セーブデータ側が返す、ユニット 1 体分の所持情報
    public readonly struct PPPartySaveUnitEntry
    {
        public readonly PPUnitDefinition Unit;
        public readonly int Level;

        // aUnit : 所持しているユニットの定義
        // aLevel : そのユニットの現在レベル
        public PPPartySaveUnitEntry(PPUnitDefinition aUnit, int aLevel)
        {
            Unit = aUnit;
            Level = aLevel;
        }
    }
}
