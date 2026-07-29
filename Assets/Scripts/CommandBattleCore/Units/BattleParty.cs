/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file BattleParty.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief バトルパーティインスタンス
 * 入れ替え前提で作ってる
 * =====================================*/
using System;
using System.Collections.Generic;
using System.Linq;

namespace CommandBattleCore
{
    /// <summary>
    /// 片陣営分のユニットをまとめて保持するパーティ。
    /// <para>
    /// 戦場に出ているアクティブメンバーと控えのリザーブメンバーを分けて持ち、
    /// 両者の入れ替え・全滅判定・パーティ単位の Tick を担う。
    /// パーティ共有のリソース（コイン等）を持たせたい場合は、これを継承した PPBattleParty 側で拡張する。
    /// </para>
    /// </summary>
    public class BattleParty
    {
        /// <summary>戦場に出ているメンバー。全滅判定や生存確認はこちらを見る。</summary>
        public List<BattleUnit> ActiveMembers { get; } = new();
        /// <summary>控えのメンバー。入れ替えでアクティブと交代する。</summary>
        public List<BattleUnit> ReserveMembers { get; } = new();

        /// <summary>入れ替えデリゲート（退場ユニット、参戦ユニット）</summary>
        public Action<BattleUnit, BattleUnit> OnSwapped { get; set; }

        /// <summary>
        /// メンバーを受け取ってパーティを構築する。同時に各ユニットへ陣営を設定する。
        /// </summary>
        /// <param name="aSide">このパーティの陣営。</param>
        /// <param name="aActiveMembers">戦場に出すメンバー。</param>
        /// <param name="aReserveMembers">控えメンバー。不要なら null。</param>
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

        /// <summary>
        /// アクティブメンバーとリザーブメンバーを双方向に入れ替える。
        /// 並び順を保つため、それぞれのリストの同じ位置に相手を差し込む。
        /// </summary>
        /// <param name="aActiveUnit">退場させるアクティブメンバー。</param>
        /// <param name="aReserveUnit">参戦させるリザーブメンバー。</param>
        /// <returns>入れ替えた場合 true。どちらかが該当リストに居なければ false。</returns>
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

        /// <summary>
        /// 生きているアクティブメンバーを取得する。
        /// </summary>
        /// <returns>生存中のアクティブメンバーのリスト。</returns>
        public List<BattleUnit> GetAliveActiveMembers() => ActiveMembers.Where(u => u.IsAlive).ToList();

        /// <summary>
        /// 全滅判定。アクティブメンバー全員が戦闘不能なら全滅とみなす（控えは数に入れない）。
        /// </summary>
        /// <returns>全滅していれば true。</returns>
        public virtual bool IsWiped() => ActiveMembers.All(u => !u.IsAlive);

        /// <summary>
        /// パーティ単位の 1 ターン更新。アクティブメンバー全員の <see cref="BattleUnit.UnitTick"/> を回す。
        /// パーティ共有リソースの更新を足す場合はここをオーバーライドする。
        /// </summary>
        /// <param name="aContext">バトルコンテキスト。</param>
        public virtual void PartyTick(BattleContext aContext)
        {
            foreach (var unit in ActiveMembers)
            {
                unit.UnitTick(aContext);
            }
        }
    }
}
