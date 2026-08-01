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
    // 戦闘に参加する 1 体分のランタイムインスタンス
    // パラメータ・スキル・ステータスエフェクト・リアクション・行動回数を保持し、
    // ダメージ／回復／エフェクト付与といった「自分の状態が変わる操作」を受け付ける
    // 状態が変わるたびに event を発火し、BattleManager がそれを購読して
    // 全体への通知・ログ出力・リアクション発火へ変換する
    // つまりこのクラス自体はバトル全体の進行を一切知らない
    public class BattleUnit
    {
        // ユニットのID
        public string UnitId { get; }
        // UI表示名
        public string DisplayName { get; }
        // 味方か敵か。パーティ生成時に設定される
        public BattleSide Side { get; protected internal set; }
        // 生存中かどうか。HP が 1 以上なら true
        public bool IsAlive => Parameters.Hp.CurrentValue > 0;

        // HP・攻撃力などのパラメータ一式
        public ParameterSet Parameters { get; }
        // 発動中のステータスエフェクト（ステータス上昇や状態異常もこれに含まれる想定）
        public List<StatusEffect> ActiveStatusEffects { get; } = new();
        // 使用可能なスキル
        public List<BattleSkill> Skills { get; } = new();
        // 反撃などのリアクション定義。トリガー発生時に BattleManager が走査する
        public List<IBattleReaction> Reactions { get; } = new();

        // 1 ターンあたりの行動回数の管理
        public ActionBudget Actions { get; } = new();
        // コマンド決定クラス。AI 制御ユニットに設定する
        public ICommandDecider CommandDecider { get; set; }
        // 生成元の定義アセットへの参照
        // AI が定義型で判定するため、CreateRuntimeUnit() での設定を省略しない
        public object SourceDefinition { get; set; }

        // ダメージ適用前の介入(ダメージ情報)。ここで Amount を書き換えれば軽減・無効化できる
        public event Action<DamageInfo> OnPreDamaged;
        // ダメージデリゲート(対象ユニット, 値)。実際に HP が減ったときのみ発火する
        public event Action<BattleUnit, float> OnDamaged;
        // ダメージ適用後の介入(ダメージ情報)
        public event Action<DamageInfo> OnPostDamaged;
        // ダメージ結果のデリゲート(ダメージ情報)。ミス・無効化を含む全ての結果で発火する
        public event Action<DamageInfo> OnDamageResolved;
        // 回復デリゲート(対象ユニット, 値)
        public event Action<BattleUnit, float> OnHealed;
        // 撃破デリゲート(対象ユニット)
        public event Action<BattleUnit> OnDefeated;
        // ステータスエフェクト追加デリゲート(対象ユニット, エフェクト)
        public event Action<BattleUnit, StatusEffect> OnStatusEffectAdded;
        // ステータスエフェクト除去デリゲート(対象ユニット, エフェクト)
        public event Action<BattleUnit, StatusEffect> OnStatusEffectRemoved;
        // ステータスエフェクトスタック時デリゲート(対象ユニット, エフェクト)
        public event Action<BattleUnit, StatusEffect> OnStatusEffectStacked;

        // ユニットを生成する
        // aUnitId : ユニットID
        // aDisplayName : UI表示名
        // aParameters : このユニットのパラメータ一式
        public BattleUnit(string aUnitId, string aDisplayName, ParameterSet aParameters)
        {
            UnitId = aUnitId;
            DisplayName = aDisplayName;
            Parameters = aParameters;
        }

        // 現在の行動制限状況。発動中の全ステータスエフェクトの制限をビット OR で合成して返す
        public ActionRestriction CurrentRestrictions
        {
            get
            {
                var r = ActionRestriction.None;
                foreach (var a in ActiveStatusEffects) r |= a.Restriction;
                return r;
            }
        }

        // 状態異常によって今回の行動が失敗するかを抽選する
        // 行動不能エフェクトのうち、失敗率が設定されていれば確率判定（麻痺など）、
        // 未設定なら無条件で失敗（睡眠など）として扱う
        // aContext : 乱数供給元を含むバトルコンテキスト
        // return : 行動が阻害された場合 true
        public bool RollActionBlocked(BattleContext aContext)
        {
            bool blocked = false;
            foreach (var eff in ActiveStatusEffects)
            {
                if((eff.Restriction & ActionRestriction.CannotAct) == 0)
                    continue;
                if (eff.ActionFailChance is float chance)
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

        // パラメータをIDで解決する
        // 追加のパラメータ群を持つ派生クラスはここをoverrideする
        // aParamId : パラメータID
        public virtual Parameter ResolveParameter(string aParamId) => Parameters.Get(aParamId);

        // 数値だけを指定してダメージを適用する簡易版。攻撃元なしの DamageInfo を組んで委譲する
        // スキルのコンテキストを渡してダメージ計算をする拡張もあり、もしくは事前計算
        // aAmount : ダメージ量
        // aContext : バトルコンテキスト。ステータスエフェクトの被ダメージ介入で乱数等を参照する場合に渡す
        public void ApplyDamage(float aAmount, BattleContext aContext = null)
        {
            ApplyDamage(new DamageInfo(null, this, aAmount), aContext);
        }

        // ダメージを適用する
        // ミス判定 → ステータスエフェクトによる軽減 → 実装先の介入 → 無効化判定 →
        // HP 減算 → 撃破判定、の順に進み、各段階で対応する event を発火する
        // aDamageInfo : 攻撃元・対象・ダメージ量・命中結果を持つダメージ情報
        // aContext : バトルコンテキスト。ステータスエフェクトの被ダメージ介入で乱数等を参照する場合に渡す
        public void ApplyDamage(DamageInfo aDamageInfo, BattleContext aContext = null)
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
                ef.NotifyIncomingDamage(this, aContext, aDamageInfo);
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

        // 回復を適用する。戦闘不能状態のユニットは回復対象にならない
        // aAmount : 回復量。0 以下なら何もしない
        public void ApplyHeal(float aAmount)
        {
            if (!IsAlive || aAmount <= 0) return;
            Parameters.Hp.Recover(aAmount);
            OnHealed?.Invoke(this, aAmount);
        }

        // ステータスエフェクトを追加する
        // 同一 ID のエフェクトが既に付与されている場合は
        // StatusEffectStackPolicy に従って無視／継続時間更新／スタック加算／置き換えのいずれかを行う
        // 死亡しているユニットには付与しない
        // aStatusEffect : 付与するエフェクト
        // aContext : バトルコンテキスト
        public void AddStatusEffect(StatusEffect aStatusEffect, BattleContext aContext)
        {
            if (aStatusEffect == null || !IsAlive) return;

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
                        OnStatusEffectStacked?.Invoke(this, existing);
                        return;
                    case StatusEffectStackPolicy.StackCount:
                        if (existing.TryAddStack(this, aContext))
                        {
                            OnStatusEffectStacked?.Invoke(this, existing);
                        }
                        return;
                    case StatusEffectStackPolicy.StackCountAndRefresh:
                        existing.TryAddStack(this, aContext);
                        (existing.DurationCondition as IRefreshableDuration)?.Refresh();
                        OnStatusEffectStacked?.Invoke(this, existing);
                        return;
                    case StatusEffectStackPolicy.Replace:
                        RemoveStatusEffect(existing, aContext);
                        // 置き換えるので下で処理
                        break;
                    case StatusEffectStackPolicy.Stack:
                    default:
                        break;
                }
            }

            // 新規付与。効果を適用してから通知する
            ActiveStatusEffects.Add(aStatusEffect);
            aStatusEffect.AttachTo(this, aContext);
            OnStatusEffectAdded?.Invoke(this, aStatusEffect);
        }

        // ステータスエフェクトを除去し、そのエフェクトが加えていた効果を巻き戻す
        // aStatusEffect : 除去するエフェクト
        // aContext : バトルコンテキスト
        public void RemoveStatusEffect(StatusEffect aStatusEffect, BattleContext aContext)
        {
            if (!ActiveStatusEffects.Remove(aStatusEffect)) return;
            aStatusEffect.DetachFrom(this, aContext);
            OnStatusEffectRemoved?.Invoke(this, aStatusEffect);
        }

        // 状況更新の度に呼び出す。各エフェクトの Tick（毒ダメージ等）を実行し、
        // 持続条件が切れたものを除去する
        // 除去中にリストが縮むため、末尾から逆順に走査している
        // aContext : バトルコンテキスト
        public void TickStatusEffects(BattleContext aContext)
        {
            for (int i = ActiveStatusEffects.Count - 1; i >= 0; i--)
            {
                var effect = ActiveStatusEffects[i];
                effect.Tick(this, aContext);
                if (!effect.DurationCondition.Tick())
                {
                    RemoveStatusEffect(effect, aContext);
                }
            }
        }

        // 1 ターン分の更新処理。ステータスエフェクトの更新、行動回数のリセット、
        // 全スキルのクールダウン消化をまとめて行う
        // aContext : バトルコンテキスト
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
