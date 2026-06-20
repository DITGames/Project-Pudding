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
    public class BattleUnit
    {
        // ユニットのID
        public string UnitId { get; }
        // UI表示名
        public string DisplayName { get; }
        // 味方か敵か
        public BattleSide Side { get; internal set; }
        // 生存中?
        public bool IsAlive => Parameters.Hp.CurrentValue > 0;
        
        // パラメータ
        public ParameterSet Parameters { get; }
        // 発動中のステータスエフェクト（ステータス上昇や状態異常もこれに含まれる想定）
        public List<StatusEffect> ActiveStatusEffects { get; } = new();
        // 使用可能なスキル
        public List<BattleSkill> Skills { get; } = new();
        // リアクション
        public List<IBattleReaction> Reactions { get; } = new();
        
        public ActionBudget Actions { get; } = new();
        // コマンド決定クラス
        public ICommandDecider CommandDecider { get; set; }
        // 生成元の定義アセットへの参照
        public object SourceDefinition { get; set; }

        // ダメージ適用前の介入(ダメージ情報)
        public event Action<DamageInfo> OnPreDamaged;
        // ダメージデリゲート(対象ユニット, 値)
        public event Action<BattleUnit, float> OnDamaged;
        // ダメージ適用後の介入(ダメージ情報)
        public event Action<DamageInfo> OnPostDamaged;
        // ダメージ結果のデリゲート(ダメージ情報)
        public event Action<DamageInfo> OnDamageResolved;
        // 回復デリゲート(対象ユニット, 値)
        public event Action<BattleUnit, float> OnHealed;
        // 撃破デリゲート(対象ユニット)
        public event Action<BattleUnit> OnDefeated;
        // ステータスエフェクト追加デリゲート(対象ユニット,　エフェクト)
        public event Action<BattleUnit, StatusEffect> OnStatusEffectAdded;
        // ステータスエフェクト除去デリゲート(対象ユニット,　エフェクト)
        public event Action<BattleUnit, StatusEffect> OnStatusEffectRemoved;
        // ステータスエフェクトスタック時デリゲート(対象ユニット,　エフェクト)
        public event Action<BattleUnit, StatusEffect> OnStatusEffectStacked;

        public BattleUnit(string aUnitId, string aDisplayName, ParameterSet aParameters)
        {
            UnitId = aUnitId;
            DisplayName = aDisplayName;
            Parameters = aParameters;
        }

        // 行動制限状況を取得
        public ActionRestriction CurrentRestrictions
        {
            get
            {
                var r = ActionRestriction.None;
                foreach (var a in ActiveStatusEffects) r |= a.Restriction;
                return r;
            }
        }

        // ダメージ適用 スキルのコンテキストを引数で渡してダメージ計算をする拡張もあり、もしくは事前計算
        public void ApplyDamage(float aAmount)
        {
            ApplyDamage(new DamageInfo(null, this, aAmount));
        }

        public void ApplyDamage(DamageInfo aDamageInfo)
        {
            if (!IsAlive) return;

            if (aDamageInfo.IsMiss)
            {
                OnDamageResolved?.Invoke(aDamageInfo);
                return;
            }

            // ステータスエフェクトによるダメージ適用前の耐性や無効などを適用する
            foreach (var ef in ActiveStatusEffects)
            {
                ef.ModifyIncomingDamage(this, aDamageInfo);
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
        
        // 回復適用
        public void ApplyHeal(float aAmount)
        {
            if (!IsAlive || aAmount <= 0) return;
            Parameters.Hp.Recover(aAmount);
            OnHealed?.Invoke(this, aAmount);
        }
        
        // ステータスエフェクト追加
        public void AddStatusEffect(StatusEffect aStatusEffect)
        {
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

            ActiveStatusEffects.Add(aStatusEffect);
            aStatusEffect.CurrentStacks = 1;
            aStatusEffect.ApplyTo(this);
            OnStatusEffectAdded?.Invoke(this, aStatusEffect);
        }

        // ステータスエフェクト除去
        public void RemoveStatusEffect(StatusEffect aStatusEffect)
        {
            ActiveStatusEffects.Remove(aStatusEffect);
            aStatusEffect.RemoveFrom(this);
            OnStatusEffectRemoved?.Invoke(this, aStatusEffect);
        }
        
        // 状況更新の度に呼び出す OnTickの実行と持続条件のチェックを行う
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
    }
}