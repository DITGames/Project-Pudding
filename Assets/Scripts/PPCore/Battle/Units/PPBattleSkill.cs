/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleSkill.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief バトルスキルのランタイムインスタンス
 * =====================================*/

using System;
using System.Collections.Generic;

namespace PPCore
{
    // スキルのランタイムインスタンス。SkillDefinition の CreateRuntimeSkill() で生成される
    // 効果本体をデリゲート Effect として保持し、クールタイム（秒）と使用回数を管理する
    public class BattleSkill
    {
        /* 状態 */

        // スキルID
        public string SkillId { get; }
        // UIへの表示名
        public string DisplayName { get; }
        // 既定のターゲット解決インターフェース。コマンド側で上書きしない場合これを使う
        public ITargetResolver DefaultTargetResolver { get; }
        // スキルの効果本体（行動ユニット, 対象リスト, コンテキスト）
        public Action<BattleUnit, List<BattleUnit>, BattleContext> Effect { get; }
        // 生成元の定義アセットへの参照
        // AI が定義型で判定するため、生成時に必ず設定する
        public object SourceDefinition { get; set; }
        // クールタイムの長さ（秒）。0 ならクールタイムなし
        public float MaxCooldown { get; set; } = 0f;
        // 1 戦闘あたりの最大使用可能回数。0 なら無制限
        public int MaxUsesPerBattle { get; set; } = 0; // 無制限
        // この戦闘で残っている使用回数
        public int UsesRemaining { get; protected internal set; } = 0;
        // クールタイムを計る時計。使用時のバトル専用の時計を覚えておく。未使用なら null
        private PPBattleClock mCooldownClock;
        // クールタイムが明ける時刻（バトル専用の時計の秒）
        private float mCooldownEndTime;

        /* プロパティ */

        // 残りクールタイム（秒）。0 で再使用可能。バトル専用の時計で進むため、時計の停止中は減らない
        public float RemainingCooldown
            => mCooldownClock == null ? 0f : Math.Max(0f, mCooldownEndTime - mCooldownClock.Now);
        // クールダウンと使用回数をまとめて見た、今このスキルを撃てるかの判定
        public bool IsReady =>
            RemainingCooldown <= 0 && (MaxUsesPerBattle == 0 || UsesRemaining > 0);
        // 使用回数制限を持つスキルかどうか
        public bool IsLimit => MaxUsesPerBattle > 0 && UsesRemaining <= MaxUsesPerBattle;
        // クールダウン中かどうか
        public bool IsCooldown => RemainingCooldown > 0;

        /* 生成 */

        public BattleSkill(string aSkillId, string aDisplayName, ITargetResolver aDefaultResolver,
            Action<BattleUnit, List<BattleUnit>, BattleContext> aEffect)
        {
            SkillId = aSkillId;
            DisplayName = aDisplayName;
            DefaultTargetResolver = aDefaultResolver;
            Effect = aEffect;
        }

        /* 操作 */

        // スキル効果を実行する。効果が未設定なら何もしない
        public void Execute(BattleUnit aSource, List<BattleUnit> aTargets, BattleContext aContext)
            => Effect?.Invoke(aSource, aTargets, aContext);

        // 戦闘開始状態へリセットする。クールダウンを解除し、使用回数を上限まで戻す
        // BattleManager.StartBattle から全スキルに対して呼ばれる
        public void ResetForBattle()
        {
            mCooldownClock = null;
            mCooldownEndTime = 0f;
            UsesRemaining = MaxUsesPerBattle;
        }

        // スキル使用を記録し、クールタイムの開始と使用回数の消費を行う
        public void NotifyUsed(BattleContext aContext)
        {
            if (MaxCooldown > 0f && aContext?.Clock != null)
            {
                mCooldownClock = aContext.Clock;
                mCooldownEndTime = aContext.Clock.Now + MaxCooldown;
            }
            if (MaxUsesPerBattle > 0 && UsesRemaining > 0) UsesRemaining--;
        }
    }

    // 本作のスキルのランタイムインスタンス。AI やコマンドが型で判別するために分けている
    // 属性やコストは BattleSkill.SourceDefinition の PPSkillDefinition から引く
    public class PPBattleSkill : BattleSkill
    {
        public PPBattleSkill(string aSkillId, string aDisplayName, ITargetResolver aDefaultResolver,
            Action<BattleUnit, List<BattleUnit>, BattleContext> aEffect)
            : base(aSkillId, aDisplayName, aDefaultResolver, aEffect)
        {

        }
    }
}
