/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file BattleUnit.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief ユニットのインスタンス
 * =====================================*/

using System;
using System.Collections.Generic;

namespace CommandBattleCore
{
    /// <summary>
    /// 戦闘に参加する 1 体分のランタイムインスタンス。
    /// <para>
    /// パラメータ・スキル・ステータスエフェクト・リアクション・行動回数を保持し、
    /// ダメージ／回復／エフェクト付与といった「自分の状態が変わる操作」を受け付ける。
    /// 状態が変わるたびに event を発火し、<see cref="BattleManager"/> がそれを購読して
    /// 全体への通知・ログ出力・リアクション発火へ変換する。
    /// つまりこのクラス自体はバトル全体の進行を一切知らない。
    /// </para>
    /// </summary>
    public class BattleUnit
    {
        /// <summary>ユニットのID。</summary>
        public string UnitId { get; }
        /// <summary>UI表示名。</summary>
        public string DisplayName { get; }
        /// <summary>味方か敵か。パーティ生成時に設定される。</summary>
        public BattleSide Side { get; protected internal set; }
        /// <summary>生存中かどうか。HP が 1 以上なら true。</summary>
        public bool IsAlive => Parameters.Hp.CurrentValue > 0;

        /// <summary>HP・攻撃力などのパラメータ一式。</summary>
        public ParameterSet Parameters { get; }
        /// <summary>発動中のステータスエフェクト（ステータス上昇や状態異常もこれに含まれる想定）。</summary>
        public List<StatusEffect> ActiveStatusEffects { get; } = new();
        /// <summary>使用可能なスキル。</summary>
        public List<BattleSkill> Skills { get; } = new();
        /// <summary>反撃などのリアクション定義。トリガー発生時に <see cref="BattleManager"/> が走査する。</summary>
        public List<IBattleReaction> Reactions { get; } = new();

        /// <summary>1 ターンあたりの行動回数の管理。</summary>
        public ActionBudget Actions { get; } = new();
        /// <summary>コマンド決定クラス。AI 制御ユニットに設定する。</summary>
        public ICommandDecider CommandDecider { get; set; }
        /// <summary>
        /// 生成元の定義アセットへの参照。
        /// AI が定義型で判定するため、<c>CreateRuntimeUnit()</c> での設定を省略しない。
        /// </summary>
        public object SourceDefinition { get; set; }

        /// <summary>ダメージ適用前の介入(ダメージ情報)。ここで Amount を書き換えれば軽減・無効化できる。</summary>
        public event Action<DamageInfo> OnPreDamaged;
        /// <summary>ダメージデリゲート(対象ユニット, 値)。実際に HP が減ったときのみ発火する。</summary>
        public event Action<BattleUnit, float> OnDamaged;
        /// <summary>ダメージ適用後の介入(ダメージ情報)。</summary>
        public event Action<DamageInfo> OnPostDamaged;
        /// <summary>ダメージ結果のデリゲート(ダメージ情報)。ミス・無効化を含む全ての結果で発火する。</summary>
        public event Action<DamageInfo> OnDamageResolved;
        /// <summary>回復デリゲート(対象ユニット, 値)</summary>
        public event Action<BattleUnit, float> OnHealed;
        /// <summary>撃破デリゲート(対象ユニット)</summary>
        public event Action<BattleUnit> OnDefeated;
        /// <summary>ステータスエフェクト追加デリゲート(対象ユニット, エフェクト)</summary>
        public event Action<BattleUnit, StatusEffect> OnStatusEffectAdded;
        /// <summary>ステータスエフェクト除去デリゲート(対象ユニット, エフェクト)</summary>
        public event Action<BattleUnit, StatusEffect> OnStatusEffectRemoved;
        /// <summary>ステータスエフェクトスタック時デリゲート(対象ユニット, エフェクト)</summary>
        public event Action<BattleUnit, StatusEffect> OnStatusEffectStacked;

        /// <param name="aUnitId">ユニットID。</param>
        /// <param name="aDisplayName">UI表示名。</param>
        /// <param name="aParameters">このユニットのパラメータ一式。</param>
        public BattleUnit(string aUnitId, string aDisplayName, ParameterSet aParameters)
        {
            UnitId = aUnitId;
            DisplayName = aDisplayName;
            Parameters = aParameters;
        }

        /// <summary>
        /// 現在の行動制限状況。発動中の全ステータスエフェクトの制限をビット OR で合成して返す。
        /// </summary>
        public ActionRestriction CurrentRestrictions
        {
            get
            {
                var r = ActionRestriction.None;
                foreach (var a in ActiveStatusEffects) r |= a.Restriction;
                return r;
            }
        }

        /// <summary>
        /// 状態異常によって今回の行動が失敗するかを抽選する。
        /// 行動不能エフェクトのうち、失敗率が設定されていれば確率判定（麻痺など）、
        /// 未設定なら無条件で失敗（睡眠など）として扱う。
        /// </summary>
        /// <param name="aContext">乱数供給元を含むバトルコンテキスト。</param>
        /// <returns>行動が阻害された場合 true。</returns>
        public bool RollActionBlocked(BattleContext aContext)
        {
            bool blocked = false;
            foreach (var eff in ActiveStatusEffects)
            {
                if((eff.Restriction & ActionRestriction.CannotAct) == 0)
                    continue;
                if (eff.ActionFailChange is float chance)
                {
                    if (aContext.Rules.RandomProvider.NextBool(chance))
                        blocked = true;
                }
                else
                {
                    blocked = true;
                }
            }
            return blocked;
        }

        /// <summary>
        /// 数値だけを指定してダメージを適用する簡易版。攻撃元なしの <see cref="DamageInfo"/> を組んで委譲する。
        /// スキルのコンテキストを渡してダメージ計算をする拡張もあり、もしくは事前計算。
        /// </summary>
        /// <param name="aAmount">ダメージ量。</param>
        public void ApplyDamage(float aAmount)
        {
            ApplyDamage(new DamageInfo(null, this, aAmount));
        }

        /// <summary>
        /// ダメージを適用する。
        /// ミス判定 → ステータスエフェクトによる軽減 → 実装先の介入 → 無効化判定 →
        /// HP 減算 → 撃破判定、の順に進み、各段階で対応する event を発火する。
        /// </summary>
        /// <param name="aDamageInfo">攻撃元・対象・ダメージ量・命中結果を持つダメージ情報。</param>
        public void ApplyDamage(DamageInfo aDamageInfo)
        {
            if (!IsAlive) return;

            // ミスの場合は結果通知だけ行って終了する
            if (aDamageInfo.IsMiss)
            {
                OnDamageResolved?.Invoke(aDamageInfo);
                return;
            }

            // ステータスエフェクトによるダメージ適用前の耐性や無効などを適用する
            foreach (var ef in ActiveStatusEffects)
            {
                ef.ModifyIncomingDamage?.Invoke(this, aDamageInfo);
            }

            // 実装先でのダメージ適用前介入
            OnPreDamaged?.Invoke(aDamageInfo);

            // 無効化済みまたはダメージなし
            if (aDamageInfo.IsNullified || aDamageInfo.Amount <= 0f)
            {
                OnPostDamaged?.Invoke(aDamageInfo);
                OnDamageResolved?.Invoke(aDamageInfo);
                return;
            }

            // ダメージ適用
            float applied = aDamageInfo.Amount;
            Parameters.Hp.Damage(aDamageInfo.Amount);
            OnDamaged?.Invoke(this, applied);
            OnPostDamaged?.Invoke(aDamageInfo);
            OnDamageResolved?.Invoke(aDamageInfo);
            if(!IsAlive) OnDefeated?.Invoke(this);
        }

        /// <summary>
        /// 回復を適用する。戦闘不能状態のユニットは回復対象にならない。
        /// </summary>
        /// <param name="aAmount">回復量。0 以下なら何もしない。</param>
        public void ApplyHeal(float aAmount)
        {
            if (!IsAlive || aAmount <= 0) return;
            Parameters.Hp.Recover(aAmount);
            OnHealed?.Invoke(this, aAmount);
        }

        /// <summary>
        /// ステータスエフェクトを追加する。
        /// 同一 ID のエフェクトが既に付与されている場合は
        /// <see cref="StatusEffectStackPolicy"/> に従って無視／継続時間更新／スタック加算／置き換えのいずれかを行う。
        /// </summary>
        /// <param name="aStatusEffect">付与するエフェクト。</param>
        public void AddStatusEffect(StatusEffect aStatusEffect)
        {
            // 既に同じエフェクトが付いている場合はスタックポリシーで挙動を決める
            var existing = ActiveStatusEffects.Find(e => e.EffectId == aStatusEffect.EffectId);
            if (existing != null)
            {
                switch (aStatusEffect.StackPolicy)
                {
                    case StatusEffectStackPolicy.Ignore:
                        return;
                    case StatusEffectStackPolicy.Refresh:
                        (existing.DurationCondition as IRefreshableDuration)?.Refresh();
                        existing.OnStackChanged?.Invoke(this, existing);
                        OnStatusEffectStacked?.Invoke(this, existing);
                        return;
                    case StatusEffectStackPolicy.StackCount:
                        if (existing.CurrentStacks < existing.MaxStacks)
                        {
                            existing.CurrentStacks++;
                            existing.OnStackChanged?.Invoke(this, existing);
                            OnStatusEffectStacked?.Invoke(this, existing);
                        }
                        return;
                    case StatusEffectStackPolicy.StackCountAndRefresh:
                        if (existing.CurrentStacks < existing.MaxStacks)
                        {
                            existing.CurrentStacks++;
                        }
                        (existing.DurationCondition as IRefreshableDuration)?.Refresh();
                        existing.OnStackChanged?.Invoke(this, existing);
                        OnStatusEffectStacked?.Invoke(this, existing);
                        return;
                    case StatusEffectStackPolicy.Replace:
                        RemoveStatusEffect(existing);
                        // 置き換えるので下で処理
                        break;
                    case StatusEffectStackPolicy.Stack:
                    default:
                        break;
                }
            }

            // 新規付与。効果を適用してから通知する
            ActiveStatusEffects.Add(aStatusEffect);
            aStatusEffect.CurrentStacks = 1;
            aStatusEffect.ApplyTo(this);
            OnStatusEffectAdded?.Invoke(this, aStatusEffect);
        }

        /// <summary>
        /// ステータスエフェクトを除去し、そのエフェクトが加えていた効果を巻き戻す。
        /// </summary>
        /// <param name="aStatusEffect">除去するエフェクト。</param>
        public void RemoveStatusEffect(StatusEffect aStatusEffect)
        {
            ActiveStatusEffects.Remove(aStatusEffect);
            aStatusEffect.RemoveFrom(this);
            OnStatusEffectRemoved?.Invoke(this, aStatusEffect);
        }

        /// <summary>
        /// 状況更新の度に呼び出す。各エフェクトの OnTick（毒ダメージ等）を実行し、
        /// 持続条件が切れたものを除去する。
        /// 除去中にリストが縮むため、末尾から逆順に走査している。
        /// </summary>
        /// <param name="aContext">バトルコンテキスト。</param>
        public void TickStatusEffects(BattleContext aContext)
        {
            for (int i = ActiveStatusEffects.Count - 1; i >= 0; i--)
            {
                var effect = ActiveStatusEffects[i];
                effect.OnTick?.Invoke(this, aContext);
                if (!effect.DurationCondition.Tick())
                {
                    RemoveStatusEffect(effect);
                }
            }
        }

        /// <summary>
        /// 1 ターン分の更新処理。ステータスエフェクトの更新、行動回数のリセット、
        /// 全スキルのクールダウン消化をまとめて行う。
        /// </summary>
        /// <param name="aContext">バトルコンテキスト。</param>
        public virtual void UnitTick(BattleContext aContext)
        {
            TickStatusEffects(aContext);
            Actions.ResetForTurn();
            foreach (var skill in Skills)
            {
                skill.TickCooldown();
            }
        }
    }
}
