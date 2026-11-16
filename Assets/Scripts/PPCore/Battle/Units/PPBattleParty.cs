/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleParty.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief バトルパーティ（陣営・パーティ・生成・セーブ供給元）
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Linq;

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
    // アクティブメンバーと控えを分けて持ち、入れ替え・全滅判定・パーティ単位の Tick を担う
    public class BattleParty
    {
        // 戦場に出ているメンバー。全滅判定や生存確認はこちらを見る
        public List<BattleUnit> ActiveMembers { get; } = new();
        // 控えのメンバー。入れ替えでアクティブと交代する
        public List<BattleUnit> ReserveMembers { get; } = new();

        // 入れ替えデリゲート（退場ユニット、参戦ユニット）
        public Action<BattleUnit, BattleUnit> OnSwapped { get; set; }

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

        // 生きているアクティブメンバーを取得する
        // return : 生存中のアクティブメンバーのリスト
        public List<BattleUnit> GetAliveActiveMembers() => ActiveMembers.Where(u => u.IsAlive).ToList();

        // 全滅判定。アクティブメンバー全員が戦闘不能なら全滅とみなす（控えは数に入れない）
        // return : 全滅していれば true
        public virtual bool IsWiped() => ActiveMembers.All(u => !u.IsAlive);

        // パーティ単位の 1 ターン更新。アクティブメンバー全員の UnitTick を回す
        // パーティ共有リソースの更新を足す場合はここをオーバーライドする
        public virtual void PartyTick(BattleContext aContext)
        {
            foreach (var unit in ActiveMembers)
            {
                unit.UnitTick(aContext);
            }
        }
    }

    // コイン変換係数と、パーティ単位で行動を組み立てる AI ストラテジストを持つバトルパーティ
    // 行動リソースはユニットが専有するため、パーティ側は保持しない
    public class PPBattleParty : BattleParty
    {
        // コイン 1 枚あたりのゲージ変換係数。PPCoinResourceBridge が参照する
        public Parameter CoinConversionRate { get; }

        // このパーティの行動計画を立てる AI。プレイヤー操作パーティなら null のまま
        public IPPPartyCommandStrategist Strategist { get; set; }

        public PPBattleParty(float aBaseCoinRate, BattleSide aSide, IEnumerable<BattleUnit> aActiveMembers,
            IEnumerable<BattleUnit> aReserveMembers = null)
            : base(aSide, aActiveMembers, aReserveMembers)
        {
            CoinConversionRate = new Parameter(aBaseCoinRate);
        }
    }

    // PPPartyDefinition などの生成元から PPBattleParty を組み立てるファクトリ
    public static class PPPartyFactory
    {
        // PPPartyDefinition からランタイムパーティを生成する
        // メンバーごとにユニットを生成して編成し、それぞれへ専用の乱数供給元を割り当てる
        public static PPBattleParty CreateFromDefinition(PPPartyDefinition aDefinition, BattleSide aSide)
        {
            var units = new List<BattleUnit>();
            for (int i = 0; i < aDefinition.Members.Count; i++)
            {
                var entry = aDefinition.Members[i];
                if(entry == null || entry.Unit == null) continue;

                var unit = (PPBattleUnit)entry.Unit.CreateRuntimeUnit(entry.Level);
                unit.Random = CreateRandom(entry, aSide, i);
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
