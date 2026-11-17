/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleUnit.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief バトルユニット
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace PPCore
{
    // 戦闘に参加する 1 体分のランタイムインスタンス。パラメータ・スキル・ステータスエフェクト・リアクションを保持する
    // 状態が変わるたびに event を発火し、BattleManager が購読して通知・ログ・リアクション発火へ変換する
    public class BattleUnit
    {
        /* 状態 */

        // ユニットのID
        public string UnitId { get; }
        // UI表示名
        public string DisplayName { get; }
        // 味方か敵か。パーティ生成時に設定される
        public BattleSide Side { get; protected internal set; }
        // HP・攻撃力などのパラメータ一式
        public ParameterSet Parameters { get; }
        // 発動中のステータスエフェクト（ステータス上昇や状態異常もこれに含まれる想定）
        public List<StatusEffect> ActiveStatusEffects { get; } = new();
        // 使用可能なスキル
        public List<BattleSkill> Skills { get; } = new();
        // 反撃などのリアクション定義。トリガー発生時に BattleManager が走査する
        public List<IBattleReaction> Reactions { get; } = new();
        // コマンド決定クラス。AI 制御ユニットに設定する
        public ICommandDecider CommandDecider { get; set; }
        // 生成元の定義アセットへの参照
        // AI が定義型で判定するため、CreateRuntimeUnit() での設定を省略しない
        public object SourceDefinition { get; set; }
        // このユニット専用の乱数供給元。ユニットごとに列を分けることで、他のユニットの消費に影響されずに挙動を再現できる
        // 未設定なら BattleRules.RandomProvider へフォールバックする
        public IRandomProvider Random { get; set; }

        /* プロパティ */

        // 生存中かどうか。HP が 1 以上なら true
        public bool IsAlive => Parameters.Hp.CurrentValue > 0;

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

        // 確率の抽選なしで、確定的に行動できない状態か（睡眠など）。失敗率が 1 以上のものも確定扱いにする
        // AI が「このユニットに行動を割り当てても無駄か」を判断するときに使う
        public bool IsActionCertainlyBlocked
        {
            get
            {
                foreach (var eff in ActiveStatusEffects)
                {
                    if ((eff.Restriction & ActionRestriction.CannotAct) == 0)
                        continue;
                    if (eff.ActionFailChance is not float chance || chance >= 1f)
                        return true;
                }
                return false;
            }
        }

        /* 通知 */

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

        /* 生成 */

        // ユニットを生成する
        public BattleUnit(string aUnitId, string aDisplayName, ParameterSet aParameters)
        {
            UnitId = aUnitId;
            DisplayName = aDisplayName;
            Parameters = aParameters;
        }

        /* ダメージと回復 */

        // 数値だけを指定してダメージを適用する簡易版。攻撃元なしの DamageInfo を組んで委譲する
        // スキルのコンテキストを渡してダメージ計算をする拡張もあり、もしくは事前計算
        public void ApplyDamage(float aAmount, BattleContext aContext = null)
        {
            // 発生源が分からない経路のため、攻撃とは見なさない
            ApplyDamage(new DamageInfo(null, this, aAmount, OtherDamageReason.Default), aContext);
        }

        // ダメージを適用する。ミス判定 → ステータスエフェクトによる軽減 → 実装先の介入 → 無効化判定 →
        // HP 減算 → 撃破判定、の順に進み、各段階で対応する event を発火する
        public void ApplyDamage(DamageInfo aDamageInfo, BattleContext aContext = null)
        {
            if (!IsAlive) return;

            // リアクションのコマンド実行中に発生したダメージへ、リアクションの深さと発生元を引き継ぐ
            // Reason は書き換えない。作る側が明示的に設定した Reaction があればそちらを優先する
            if (aContext?.ExecutingCommand != null)
            {
                aDamageInfo.Reaction ??= aContext.ExecutingCommand.ReactionInfo;
            }

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

            // ダメージ量への介入（NotifyIncomingDamage）が済んだ後に消費させる
            // 先に消費すると「次に受けるダメージを変化させる」効果が自分の出番で消えてしまう
            NotifyStatusEffectTrigger(StatusEffectConsumeTrigger.Damaged, aContext, aDamageInfo);

            if (!IsAlive)
            {
                // 撃破を通知する前に状態異常を解除し、OnDefeated の購読側が解除済みの状態を見られるようにする
                // バトルコンテキストが無い経路ではルールを参照できないため解除しない
                if (aContext?.Rules != null && aContext.Rules.ClearStatusEffectsOnDefeat)
                {
                    RemoveAllStatusEffects(aContext);
                }
                OnDefeated?.Invoke(this);
            }
        }

        // 回復を適用する。戦闘不能状態のユニットは回復対象にならない
        // aAmount : 回復量。0 以下なら何もしない
        public void ApplyHeal(float aAmount)
        {
            if (!IsAlive || aAmount <= 0) return;
            Parameters.Hp.Recover(aAmount);
            OnHealed?.Invoke(this, aAmount);
        }

        /* ステータスエフェクト */

        // 状態異常によって今回の行動が失敗するかを抽選する
        // 失敗率が設定されていれば確率判定（麻痺など）、未設定なら無条件で失敗（睡眠など）として扱う
        public bool RollActionBlocked(BattleContext aContext)
        {
            bool blocked = false;
            foreach (var eff in ActiveStatusEffects)
            {
                if((eff.Restriction & ActionRestriction.CannotAct) == 0)
                    continue;
                if (eff.ActionFailChance is float chance)
                {
                    // 自分が行動できるかの抽選なので、自分の乱数列から引く
                    if (ResolveRandom(aContext).NextBool(chance))
                        blocked = true;
                }
                else
                {
                    blocked = true;
                }
            }
            return blocked;
        }

        // ステータスエフェクトを追加する。同一 ID が付与済みなら StatusEffectStackPolicy に従って処理する
        // 死亡しているユニットには付与しない
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
                        RefreshDuration(existing, aContext);
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
                        RefreshDuration(existing, aContext);
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
            ScheduleStatusEffectTick(aStatusEffect, aContext);
            OnStatusEffectAdded?.Invoke(this, aStatusEffect);
        }

        // ステータスエフェクトを除去し、そのエフェクトが加えていた効果を巻き戻す
        public void RemoveStatusEffect(StatusEffect aStatusEffect, BattleContext aContext)
        {
            if (!ActiveStatusEffects.Remove(aStatusEffect)) return;
            aStatusEffect.CancelTickTimer();
            aStatusEffect.DetachFrom(this, aContext);
            OnStatusEffectRemoved?.Invoke(this, aStatusEffect);
        }

        // 掛かっているステータスエフェクトをすべて除去する（1 件ずつ RemoveStatusEffect を通す）
        // 除去中にリストが縮むため、末尾から逆順に走査する
        public int RemoveAllStatusEffects(BattleContext aContext)
        {
            int removedCount = 0;
            for (int i = ActiveStatusEffects.Count - 1; i >= 0; i--)
            {
                // 除去通知の購読側で他のエフェクトが除去され、添字がずれる可能性を考慮する
                if (i >= ActiveStatusEffects.Count) continue;
                RemoveStatusEffect(ActiveStatusEffects[i], aContext);
                removedCount++;
            }
            return removedCount;
        }

        // エフェクトの次の発生を、持続条件の発生間隔（秒）後に予約する。バトル専用の時計で進むため、時計の停止中は進まない
        // バトルコンテキストが無い経路と、発生間隔が 0 以下の持続条件では予約しない
        private void ScheduleStatusEffectTick(StatusEffect aEffect, BattleContext aContext)
        {
            float interval = aEffect.DurationCondition?.Interval ?? 0f;
            if (aContext?.Timers == null || interval <= 0f) return;

            var timer = aContext.Timers.Schedule(interval, () => HandleStatusEffectTick(aEffect, aContext));
            aEffect.SetTickTimer(aContext.Timers, timer);
        }

        // エフェクト 1 つ分の発生を処理する。効果を発生させ、持続条件が切れたら除去し、続くなら次の発生を予約する
        private void HandleStatusEffectTick(StatusEffect aEffect, BattleContext aContext)
        {
            aEffect.SetTickTimer(null, null);
            if (!ActiveStatusEffects.Contains(aEffect)) return;

            aEffect.Tick(this, aContext);

            // 自身の Tick の中で除去された（毒で撃破されて全解除された等）エフェクトは、
            // 持続の消化やスタック消費を行わない。除去済みのエフェクトへ効果を掛け直さないため
            if (!ActiveStatusEffects.Contains(aEffect)) return;

            if (!aEffect.DurationCondition.Tick())
            {
                RemoveStatusEffect(aEffect, aContext);
                return;
            }

            // 寿命が残っていても、効果の発生をきっかけにしたスタック消費で切れることがある
            ApplyConsumeRule(aEffect, StatusEffectConsumeTrigger.Ticked, aContext, null);
            if (!ActiveStatusEffects.Contains(aEffect)) return;

            ScheduleStatusEffectTick(aEffect, aContext);
        }

        // 掛け直しで持続を初期値へ戻す。残り回数を戻し、次の発生までの時間も数え直す
        // 延長し直せない持続条件（永続など）は、発生の周期を変えないよう何もしない
        private void RefreshDuration(StatusEffect aEffect, BattleContext aContext)
        {
            if (aEffect.DurationCondition is not IRefreshableDuration refreshable) return;

            refreshable.Refresh();
            // 予約し直せない経路（バトルコンテキスト無し）では、待っているタイマーをそのまま使う
            if (aContext?.Timers == null) return;

            aEffect.CancelTickTimer();
            ScheduleStatusEffectTick(aEffect, aContext);
        }

        // スタック消費のきっかけが起きたことを全エフェクトへ知らせ、使い切ったエフェクトを除去する
        // 除去中にリストが縮むため、末尾から逆順に走査する
        public void NotifyStatusEffectTrigger(StatusEffectConsumeTrigger aTrigger, BattleContext aContext,
            DamageInfo aDamage = null)
        {
            for (int i = ActiveStatusEffects.Count - 1; i >= 0; i--)
            {
                // 直前の消費で他のエフェクトが除去され、添字がずれる可能性を考慮する
                if (i >= ActiveStatusEffects.Count) continue;
                ApplyConsumeRule(ActiveStatusEffects[i], aTrigger, aContext, aDamage);
            }
        }

        // 1 つのエフェクトへ消費ルールを適用する
        private void ApplyConsumeRule(StatusEffect aEffect, StatusEffectConsumeTrigger aTrigger, BattleContext aContext,
            DamageInfo aDamage)
        {
            if (aEffect?.ConsumeRule == null) return;

            var effectContext = new StatusEffectContext(aEffect, this, aContext);
            int consume = aEffect.ConsumeRule.ResolveConsumeStacks(aTrigger, effectContext, aDamage);
            if (consume <= 0) return;

            if (aEffect.ConsumeStacks(consume, this, aContext))
            {
                RemoveStatusEffect(aEffect, aContext);
            }
        }

        /* 問い合わせ */

        // このユニットが使う乱数供給元を解決する
        // 自前の列を持っていればそちらを、無ければバトル共通の供給元を返す
        public IRandomProvider ResolveRandom(BattleContext aContext)
            => Random ?? aContext.Rules.RandomProvider;

        // パラメータをIDで解決する
        // 追加のパラメータ群を持つ派生クラスはここをoverrideする
        public virtual Parameter ResolveParameter(string aParamId) => Parameters.Get(aParamId);
    }

    // 属性・拡張パラメータ・配置を持つバトルユニット
    public class PPBattleUnit : BattleUnit
    {
        /* 状態 */

        // BattleUnit.Parameters に含まれない追加パラメータ
        public PPParameterSet ExtraParameters { get; }
        // ユニットの属性。弱点・耐性倍率の判定に使う
        public PPTypeAttribute TypeAttribute { get; }
        // 育成値とスキル効果値から BaseValue を組み立てるテーブル
        // パッシブスキルやパーティ効果は、ここへ効果値を足すことで BaseValue に反映させる
        public PPBaseParameterTable BaseParameters { get; }
        // パーティ内の配置。コインを配る順番と正面の相手の判定に使う。パーティ生成時に定義から設定される
        public PPPartyPosition Position { get; set; }
        // 選択中のスキル。行動を積むときに発動可能ならこのスキルを使い、そうでなければ通常攻撃になる。未選択なら null
        // 使用後や失敗後も解除されず、取り消すまで残る
        public BattleSkill SelectedSkill { get; private set; }
        // 選択中のスキルの対象として選んだユニット。単体が対象のスキルで使う。未指定なら null
        public BattleUnit SelectedSkillTarget { get; private set; }

        /* プロパティ */

        // 1 回の行動に必要なコインゲージ量。バフ・デバフ込みの現在値
        public float ActionCost => ExtraParameters.AttackCost.CurrentValue;

        /* イベント */

        // スキルの選択が変わったとき（選択・変更・取り消し）
        public event Action<PPBattleUnit> OnSkillSelectionChanged;

        /* 生成 */

        public PPBattleUnit(string aUnitId, string aDisplayName, ParameterSet aParameterSet,
            PPParameterSet aExtraParameterSet, PPTypeAttribute aTypeAttribute)  : base(aUnitId, aDisplayName, aParameterSet)
        {
            ExtraParameters = aExtraParameterSet;
            TypeAttribute = aTypeAttribute;
            // 生成時点の BaseValue は定義側で評価した育成値そのものなので、それを育成値として登録する
            BaseParameters = CreateBaseParameterTable();
        }

        // 育成値テーブルを組み立てる。コンストラクタから呼ばれる
        // スキルゲージ上限はバフの対象外だが、パッシブによる上限上昇（S）の対象にはなるため登録する
        protected virtual PPBaseParameterTable CreateBaseParameterTable()
        {
            var table = new PPBaseParameterTable();
            table.Register(ParameterSet.ParamIdMaxHp, Parameters.Hp.Max);
            table.Register(ParameterSet.ParamIdAttack, Parameters.Attack);
            table.Register(ParameterSet.ParamIdDefense, Parameters.Defense);
            table.Register(ParameterSet.ParamIdSpeed, Parameters.Speed);
            table.Register(PPParameterSet.ParameterIdDexterity, ExtraParameters.Dexterity);
            table.Register(PPParameterSet.ParameterIdSkillGaugeMax, ExtraParameters.SkillGauge.Max);
            return table;
        }

        /* 操作 */

        // 次の行動から使うスキルを選ぶ。自分の持っていないスキルは選べない
        public bool SelectSkill(BattleSkill aSkill, BattleUnit aTarget = null)
        {
            if (aSkill == null || !Skills.Contains(aSkill)) return false;

            SelectedSkill = aSkill;
            SelectedSkillTarget = aTarget;
            OnSkillSelectionChanged?.Invoke(this);
            return true;
        }

        // スキルの選択を取り消し、通常攻撃を行う状態へ戻す
        public void ClearSkillSelection()
        {
            if (SelectedSkill == null) return;

            SelectedSkill = null;
            SelectedSkillTarget = null;
            OnSkillSelectionChanged?.Invoke(this);
        }

        /* 問い合わせ */

        // 今の状況で発動できるスキルを 1 つでも持っているかを判定する
        // リソース不足やクールダウンで全スキルが撃てない「手詰まり」の検出に使う
        public bool CanValidateSkill(BattleContext aContext)
        {
            foreach (var skill in Skills)
            {
                var result = aContext.Rules.CastValidator.Validate(this, skill, aContext);
                if (result.CanCast)
                    return true;
            }

            return false;
        }

        // ID からパラメータを解決する。基本パラメータを先に探し、無ければ拡張パラメータを探す
        public override Parameter ResolveParameter(string aId)
        {
            if(Parameters.Parameters.TryGetValue(aId, out var paramDef))
            {
                return paramDef;
            }

            if (ExtraParameters.Parameters.TryGetValue(aId, out var paramEx))
            {
                return paramEx;
            }

            return null;
        }
    }
}
