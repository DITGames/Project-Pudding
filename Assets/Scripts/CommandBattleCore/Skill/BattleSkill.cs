/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file BattleSkill.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief スキルのインスタンス
 * =====================================*/
using System;
using System.Collections.Generic;

namespace CommandBattleCore
{
    /// <summary>
    /// スキルのランタイムインスタンス。
    /// <para>
    /// <see cref="SkillDefinition"/>（ScriptableObject）から <c>CreateRuntimeSkill()</c> で生成され、
    /// 効果本体をデリゲート <see cref="Effect"/> として保持する。
    /// 加えて、戦闘中に変化する使用制限（クールダウン・1 戦闘あたりの使用回数）を管理するのがこのクラスの役目。
    /// </para>
    /// </summary>
    public class BattleSkill
    {
        /// <summary>スキルID。</summary>
        public string SkillId { get; }
        /// <summary>UIへの表示名。</summary>
        public string DisplayName { get; }
        /// <summary>既定のターゲット解決インターフェース。コマンド側で上書きしない場合これを使う。</summary>
        public ITargetResolver DefaultTargetResolver { get; }

        /// <summary>スキルの効果本体（行動ユニット, 対象リスト, コンテキスト）。</summary>
        public Action<BattleUnit, List<BattleUnit>, BattleContext> Effect { get; }

        /// <param name="aSkillId">スキルID。</param>
        /// <param name="aDisplayName">UI表示名。</param>
        /// <param name="aDefaultResolver">既定のターゲットリゾルバ。</param>
        /// <param name="aEffect">効果本体のデリゲート。</param>
        public BattleSkill(string aSkillId, string aDisplayName, ITargetResolver aDefaultResolver,
            Action<BattleUnit, List<BattleUnit>, BattleContext> aEffect)
        {
            SkillId = aSkillId;
            DisplayName = aDisplayName;
            DefaultTargetResolver = aDefaultResolver;
            Effect = aEffect;
        }

        /// <summary>
        /// 生成元の定義アセットへの参照。
        /// AI が <c>SourceDefinition is PPSkillDefinition</c> で判定するため、生成時に必ず設定する。
        /// </summary>
        public object SourceDefinition { get; set; }

        /// <summary>
        /// スキル効果を実行する。効果が未設定なら何もしない。
        /// </summary>
        /// <param name="aSource">行動ユニット。</param>
        /// <param name="aTargets">解決済みの対象リスト。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        public void Execute(BattleUnit aSource, List<BattleUnit> aTargets, BattleContext aContext)
            => Effect?.Invoke(aSource, aTargets, aContext);

        /// <summary>クールダウンのターン数。0 ならクールダウンなし。</summary>
        public int MaxCooldown { get; set; } = 0; // クールダウンなし
        /// <summary>残りクールダウンターン数。0 で再使用可能。</summary>
        public int RemainingCooldown { get; protected internal set; } = 0;

        /// <summary>1 戦闘あたりの最大使用可能回数。0 なら無制限。</summary>
        public int MaxUsesPerBattle { get; set; } = 0; // 無制限
        /// <summary>この戦闘で残っている使用回数。</summary>
        public int UsesRemaining { get; protected internal set; } = 0;

        /// <summary>クールダウンと使用回数をまとめて見た、今このスキルを撃てるかの判定。</summary>
        public bool IsReady =>
            RemainingCooldown <= 0 && (MaxUsesPerBattle == 0 || UsesRemaining > 0);

        /// <summary>使用回数制限を持つスキルかどうか。</summary>
        public bool IsLimit => MaxUsesPerBattle > 0 && UsesRemaining <= MaxUsesPerBattle;

        /// <summary>クールダウン中かどうか。</summary>
        public bool IsCooldown => RemainingCooldown > 0;

        /// <summary>
        /// 戦闘開始状態へリセットする。クールダウンを解除し、使用回数を上限まで戻す。
        /// <see cref="BattleManager.StartBattle"/> から全スキルに対して呼ばれる。
        /// </summary>
        public void ResetForBattle()
        {
            RemainingCooldown = 0;
            UsesRemaining = MaxUsesPerBattle;
        }

        /// <summary>
        /// スキル使用を記録し、クールダウン開始と使用回数の消費を行う。
        /// </summary>
        public void NotifyUsed()
        {
            // 使用後にTick走るのでRemainingCooldownはMaxCooldown + 1にすべき
            if (MaxCooldown > 0) RemainingCooldown = MaxCooldown + 1;
            if (MaxUsesPerBattle > 0 && UsesRemaining > 0) UsesRemaining--;
        }

        /// <summary>
        /// クールダウンを 1 ターン分進める。<see cref="BattleUnit.UnitTick"/> から毎ターン呼ばれる。
        /// </summary>
        public void TickCooldown()
        {
            if (RemainingCooldown > 0) RemainingCooldown--;
        }
    }
}
