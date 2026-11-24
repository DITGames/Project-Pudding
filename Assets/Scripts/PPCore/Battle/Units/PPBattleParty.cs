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
        [InspectorName("味方")]
        Ally,
        // 敵側
        [InspectorName("敵")]
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

    // 旧方式のパーティ内の配置。編成はリストになったため、今は使っていない
    public enum PPPartyPosition
    {
        [InspectorName("中央")]
        Center,
        [InspectorName("右")]
        Right,
        [InspectorName("左")]
        Left,
    }

    // 本作のバトルパーティ。PPPartyFactory が生成する
    public class PPBattleParty : BattleParty
    {
        public PPBattleParty(BattleSide aSide, IEnumerable<BattleUnit> aActiveMembers,
            IEnumerable<BattleUnit> aReserveMembers = null)
            : base(aSide, aActiveMembers, aReserveMembers)
        {
        }
    }

    // メンバー枠の並びから PPBattleParty を組み立てるファクトリ
    public static class PPPartyFactory
    {
        /* 定数 */

        // 1 つのパーティに編成できるユニットの数
        public const int MaxMembers = 10;

        /* 操作 */

        // メンバー枠の並びからランタイムパーティを生成する
        // 枠ごとにユニットを生成し、それぞれへ専用の乱数供給元を割り当てる。空の枠は飛ばし、上限より後ろの枠は使わない
        public static PPBattleParty CreateFromMembers(IReadOnlyList<PPPartyMemberEntry> aMembers, BattleSide aSide)
        {
            var units = new List<BattleUnit>();
            for (int i = 0; aMembers != null && i < aMembers.Count && i < MaxMembers; i++)
            {
                var entry = aMembers[i];
                if (entry == null || entry.Unit == null) continue;

                var unit = (PPBattleUnit)entry.Unit.CreateRuntimeUnit(entry.Level);
                unit.Random = CreateRandom(entry, aSide, i);
                units.Add(unit);
            }

            return new PPBattleParty(aSide, units);
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
