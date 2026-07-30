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
    // スキルのランタイムインスタンス
    // SkillDefinition（ScriptableObject）から CreateRuntimeSkill() で生成され、
    // 効果本体をデリゲート Effect として保持する
    // 加えて、戦闘中に変化する使用制限（クールダウン・1 戦闘あたりの使用回数）を管理するのがこのクラスの役目
    public class BattleSkill
    {
        // スキルID
        public string SkillId { get; }
        // UIへの表示名
        public string DisplayName { get; }
        // 既定のターゲット解決インターフェース。コマンド側で上書きしない場合これを使う
        public ITargetResolver DefaultTargetResolver { get; }

        // スキルの効果本体（行動ユニット, 対象リスト, コンテキスト）
        public Action<BattleUnit, List<BattleUnit>, BattleContext> Effect { get; }

        // aSkillId : スキルID
        // aDisplayName : UI表示名
        // aDefaultResolver : 既定のターゲットリゾルバ
        // aEffect : 効果本体のデリゲート
        public BattleSkill(string aSkillId, string aDisplayName, ITargetResolver aDefaultResolver,
            Action<BattleUnit, List<BattleUnit>, BattleContext> aEffect)
        {
            SkillId = aSkillId;
            DisplayName = aDisplayName;
            DefaultTargetResolver = aDefaultResolver;
            Effect = aEffect;
        }

        // 生成元の定義アセットへの参照
        // AI が定義型で判定するため、生成時に必ず設定する
        public object SourceDefinition { get; set; }

        // スキル効果を実行する。効果が未設定なら何もしない
        // aSource : 行動ユニット
        // aTargets : 解決済みの対象リスト
        // aContext : バトルコンテキスト
        public void Execute(BattleUnit aSource, List<BattleUnit> aTargets, BattleContext aContext)
            => Effect?.Invoke(aSource, aTargets, aContext);

        // クールダウンのターン数。0 ならクールダウンなし
        public int MaxCooldown { get; set; } = 0; // クールダウンなし
        // 残りクールダウンターン数。0 で再使用可能
        public int RemainingCooldown { get; protected internal set; } = 0;

        // 1 戦闘あたりの最大使用可能回数。0 なら無制限
        public int MaxUsesPerBattle { get; set; } = 0; // 無制限
        // この戦闘で残っている使用回数
        public int UsesRemaining { get; protected internal set; } = 0;

        // クールダウンと使用回数をまとめて見た、今このスキルを撃てるかの判定
        public bool IsReady =>
            RemainingCooldown <= 0 && (MaxUsesPerBattle == 0 || UsesRemaining > 0);

        // 使用回数制限を持つスキルかどうか
        public bool IsLimit => MaxUsesPerBattle > 0 && UsesRemaining <= MaxUsesPerBattle;

        // クールダウン中かどうか
        public bool IsCooldown => RemainingCooldown > 0;

        // 戦闘開始状態へリセットする。クールダウンを解除し、使用回数を上限まで戻す
        // BattleManager.StartBattle から全スキルに対して呼ばれる
        public void ResetForBattle()
        {
            RemainingCooldown = 0;
            UsesRemaining = MaxUsesPerBattle;
        }

        // スキル使用を記録し、クールダウン開始と使用回数の消費を行う
        public void NotifyUsed()
        {
            // 使用後にTick走るのでRemainingCooldownはMaxCooldown + 1にすべき
            if (MaxCooldown > 0) RemainingCooldown = MaxCooldown + 1;
            if (MaxUsesPerBattle > 0 && UsesRemaining > 0) UsesRemaining--;
        }

        // クールダウンを 1 ターン分進める。BattleUnit.UnitTick から毎ターン呼ばれる
        public void TickCooldown()
        {
            if (RemainingCooldown > 0) RemainingCooldown--;
        }
    }
}
