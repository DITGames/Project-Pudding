/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPSkillEffectDefinitions.cs
 * @author hqrse
 * @date 2026/08/06
 * @brief スキル効果の定義（基底・攻撃・回復・リソース獲得・状態付与・状態解除・効果見積もり）
 * =====================================*/

using System;
using System.Linq;
using UnityEngine;
using AttributeUtility;
using CustomConsole;

namespace PPCore
{
    // スキル発動時にエフェクトを誰に適用するか
    public enum PPEffectApplyTarget
    {
        [InspectorName("対象")]
        Target,
        [InspectorName("発動者")]
        Self,
    }

    // スキルエフェクト定義の抽象基底。ScriptableObject ではなく [SerializeReference] 対応の通常クラスとする
    // PPSkillDefinition.mSkillEffects にインスタンスとして直接保持され、
    // アセットファイルを介さずインスペクタ上でその場に組み立てられる
    [Serializable]
    public abstract class PPSkillEffectDefinition
    {
        [Label("対象")]
        [SerializeField] private PPEffectApplyTarget mApplyTarget = PPEffectApplyTarget.Target;
        public PPEffectApplyTarget ApplyTarget => mApplyTarget;

        // この効果を対象 1 体に適用する
        // aSource : スキル発動者
        // aTarget : 適用対象（ApplyTarget = Self の場合は aSource と同一）
        // aSourceSkill : この効果を保有するスキル定義。ダメージ情報の発生源表示等に使う
        // aContext : バトルコンテキスト
        public abstract void Apply(BattleUnit aSource, BattleUnit aTarget, PPSkillDefinition aSourceSkill, BattleContext aContext);

        // この効果を実行せずに効果量を見積もる。AI の効用計算から呼ばれる
        // 状態を一切変えずに値を返すこと。乱数も引かないこと（同じ状況で同じ値になる必要がある）
        // 既定は「効果なし」。見積もりに対応しない効果は AI から無視されるだけで、実行時の挙動には影響しない
        // aSource : スキル発動者
        // aTarget : 適用対象
        // aContext : バトルコンテキスト
        // return : 効果量の見積もり
        public virtual PPEffectEstimate Estimate(BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
            => PPEffectEstimate.None;

        // リストの要素ラベルに表示する、この効果の内容を要約した文字列を組み立てる
        // ApplyTarget は含めず、エフェクト固有の設定値のみを表す
        public abstract string BuildString();
    }

    // 属性相性を考慮したダメージを 1 対象に与えるスキルエフェクト
    // 属性・威力・カテゴリを自身で持つため、1 スキルに複数登録すれば複数属性の複合攻撃になる
    [Serializable]
    [PPTypeMenuName("ダメージ")]
    public class PPAttackSkillEffectDefinition : PPSkillEffectDefinition
    {
        [Label("属性")]
        [SerializeField] private PPTypeAttribute mAttribute = PPTypeAttribute.Normal;
        [PercentLabel("威力", 0, 10)]
        [SerializeField] private float mPower = 1.2f;
        [Label("種別")]
        [SerializeField] private PPSkillCategory mCategory = PPSkillCategory.Physical;

        // aSource : スキル発動者
        // aTarget : ダメージを与える対象
        // aSourceSkill : この効果を保有するスキル定義。ダメージ情報の発生源表示等に使う
        // aContext : バトルコンテキスト
        public override void Apply(BattleUnit aSource, BattleUnit aTarget, PPSkillDefinition aSourceSkill, BattleContext aContext)
        {
            var attribute = PPDamageUtility.ResolveAttribute(mAttribute, aSource);
            float dmg = PPDamageUtility.ResolveAttackSkillDamage(aSource, aTarget, mPower, mCategory);
            // スキル効果にはコマンドが渡らないため、発生元コマンドは実行中のコマンドから取る
            var reason = new AttackDamageReason(aContext.ExecutingCommand, aSourceSkill);
            var damageInfo = PPDamageUtility.CreateDamageInfo(aSource, aTarget, dmg, mCategory, attribute, reason, aContext);
            aTarget.ApplyDamage(damageInfo, aContext);
        }

        // 基礎ダメージ量をそのまま見積もりとして返す
        // 命中・クリティカル・属性相性は CreateDamageInfo 側で乱数を含めて解決されるため、
        // 見積もりには含めない（同じ状況で同じ値を返す必要がある）
        // aSource : スキル発動者
        // aTarget : ダメージを与える対象
        // aContext : バトルコンテキスト
        // return : 与ダメージの見積もり
        public override PPEffectEstimate Estimate(BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            if (aSource == null || aTarget == null)
                return PPEffectEstimate.None;

            return PPEffectEstimate.FromDamage(
                PPDamageUtility.ResolveAttackSkillDamage(aSource, aTarget, mPower, mCategory));
        }

        public override string BuildString()
            => $"ダメージ：{mAttribute} / {mCategory} / 威力{mPower:0%}";
    }

    // 対象を回復量分だけ回復するスキルエフェクト
    // 戦闘不能のユニットは BattleUnit.ApplyHeal 側で弾かれるため蘇生にはならない
    [Serializable]
    [PPTypeMenuName("回復")]
    public class PPHealSkillEffectDefinition : PPSkillEffectDefinition
    {
        [Label("回復量")]
        [SerializeField] private float mPower = 10f;

        // aSource : スキル発動者
        // aTarget : 回復する対象
        // aSourceSkill : この効果を保有するスキル定義
        // aContext : バトルコンテキスト
        public override void Apply(BattleUnit aSource, BattleUnit aTarget, PPSkillDefinition aSourceSkill, BattleContext aContext)
        {
            aTarget.ApplyHeal(mPower);
        }

        // 回復量をそのまま見積もりとして返す
        // 対象の HP がどれだけ欠けているかは見ず、純粋な回復量の見積もりを返す
        // aSource : スキル発動者
        // aTarget : 回復する対象
        // aContext : バトルコンテキスト
        // return : 回復量の見積もり
        public override PPEffectEstimate Estimate(BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
            => PPEffectEstimate.FromHeal(mPower);

        public override string BuildString()
            => $"回復：{mPower}";
    }

    // 対象ユニットのゲージを回復するスキルエフェクト
    // ApplyTarget = 発動者 の場合は aTarget が発動者自身になるため、発動者のゲージが回復する
    // ゲージはユニット専有のため、対象 1 体ごとにそのユニットのゲージへ直接加算する
    [Serializable]
    [PPTypeMenuName("ゲージ回復")]
    public class PPResourceGainSkillEffectDefinition : PPSkillEffectDefinition
    {
        [Label("対象ゲージ")]
        [SerializeField] private PPGaugeKind mKind = PPGaugeKind.Skill;
        [Label("回復量")]
        [SerializeField] private float mAmount = 0f;

        // aSource : スキル発動者
        // aTarget : ゲージを回復する対象
        // aSourceSkill : この効果を保有するスキル定義
        // aContext : バトルコンテキスト
        public override void Apply(BattleUnit aSource, BattleUnit aTarget, PPSkillDefinition aSourceSkill, BattleContext aContext)
        {
            if (aTarget is not PPBattleUnit ppTarget) return;

            ppTarget.ExtraParameters.Gauge(mKind).Recover(mAmount);
        }

        public override string BuildString()
            => $"ゲージ回復：{PPGaugeUtility.ToDisplayString(mKind)} {mAmount}";
    }

    // 参照する PPEffectDefinition アセット（毒・パラメータ変動）から StatusEffect を生成し、対象に付与するスキルエフェクト
    // 効果の値は参照先アセットに一元化されており、このクラス自身は値を持たない
    [Serializable]
    [PPTypeMenuName("StatusEffect付与")]
    public class PPStatusApplySkillEffectDefinition : PPSkillEffectDefinition
    {
        [Label("エフェクト")]
        [SerializeField]
        private PPEffectDefinition mEffect;

        // 付与に成功する確率（0～1）。1 なら必ず付与する
        [Label("付与確率")][Range(0f, 1f)]
        [SerializeField]
        private float mApplyChance = 1f;

        // 表示と判定で扱いを揃えるため、常に 0～1 へ丸めた値を使う
        private float ApplyChance => Mathf.Clamp01(mApplyChance);

        // aSource : スキル発動者
        // aTarget : StatusEffect を付与する対象
        // aSourceSkill : この効果を保有するスキル定義
        // aContext : バトルコンテキスト
        public override void Apply(BattleUnit aSource, BattleUnit aTarget, PPSkillDefinition aSourceSkill, BattleContext aContext)
        {
            if (mEffect == null) return;
            if (!RollApplyChance(aTarget, aContext)) return;

            aTarget.AddStatusEffect(
                mEffect.CreateRuntimeStatusEffect(aSource, aTarget, aContext), aContext);
        }

        // 付与確率の抽選を行う
        // 乱数は再現性を保つため必ず Rules.RandomProvider を経由する
        // aTarget : 付与対象。ログ用
        // aContext : バトルコンテキスト
        // return : 付与する場合 true
        private bool RollApplyChance(BattleUnit aTarget, BattleContext aContext)
        {
            float chance = ApplyChance;
            if (chance >= 1f) return true;

            if (aContext.Rules.RandomProvider.NextFloat() < chance) return true;

            // 確率で外れた場合は何も起きないため、追えるようにログだけ残す
            CustomConsoleLog.Verbose("Battle",
                $"{aTarget?.DisplayName} への {mEffect.DisplayName} の付与に失敗しました（付与確率 {chance:P0}）。");
            return false;
        }

        // 付与するエフェクトの識別子を見積もりとして返す
        // AI はこれを対象の ActiveStatusEffects と突き合わせ、既に付いている場合の重ね掛けを避ける
        // aSource : スキル発動者
        // aTarget : StatusEffect を付与する対象
        // aContext : バトルコンテキスト
        // return : 付与エフェクトの識別子を持つ見積もり。未設定なら効果なし
        public override PPEffectEstimate Estimate(BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
            => mEffect != null ? PPEffectEstimate.FromStatus(mEffect.EffectId) : PPEffectEstimate.None;

        public override string BuildString()
        {
            if (mEffect == null) return "StatusEffect付与（未設定）";

            // 必中のときは付けない（大半のスキルがそうなので、毎行に付くと読みにくくなるため）
            string chance = ApplyChance >= 1f ? string.Empty : $"（{ApplyChance:P0}）";
            return $"StatusEffect付与{chance}：{mEffect.BuildString()}";
        }
    }

    // 指定した種別のエフェクトを対象から解除するスキルエフェクト
    // 状態異常・パラメータ変動を問わず PPEffectCategory のビットマスク1本で指定できるため、
    // 系統をまたいだ解除(例: 毒+攻撃力デバフを同時に解除)も1つの定義で表現できる
    // Unremovable タグが付いたエフェクトはマスクが一致しても解除対象から除外する
    [Serializable]
    [PPTypeMenuName("StatusEffect解除")]
    public class PPStatusCureSkillEffectDefinition : PPSkillEffectDefinition
    {
        [Label("解除するカテゴリ")]
        [SerializeField] private PPEffectCategory mCureMask = PPEffectCategory.AllAilment;

        // 対象のエフェクト一覧からマスクに一致するものを解除する
        // 除去中に BattleUnit.ActiveStatusEffects が変化するため、
        // 一度 ToList() で確定させてから removal を回している
        // aSource : スキル発動者
        // aTarget : StatusEffect を解除する対象
        // aSourceSkill : この効果を保有するスキル定義
        // aContext : バトルコンテキスト
        public override void Apply(BattleUnit aSource, BattleUnit aTarget, PPSkillDefinition aSourceSkill, BattleContext aContext)
        {
            long mask = (long)mCureMask;
            var matched = aTarget.ActiveStatusEffects
                .Where(e => (e.Category & mask) != 0
                         && (e.Tags & StatusEffectTag.Unremovable) == 0)
                .ToList();

            foreach (var effect in matched)
            {
                aTarget.RemoveStatusEffect(effect, aContext);
            }
        }

        public override string BuildString()
            => $"StatusEffect解除：{mCureMask}";
    }

    // スキル効果を実行せずに見積もった結果
    // AI が「この行動にどれだけの価値があるか」を判断するために使う
    // 効果の内部表現（威力・カテゴリ・エフェクト定義）を AI 側へ漏らさずに済むよう、
    // 必要な情報だけをこの型に詰めて受け渡す
    public readonly struct PPEffectEstimate
    {
        // 与ダメージの見込み量。命中率・クリティカル・属性相性は含めない
        public float Damage { get; }
        // 回復量の見込み
        public float Heal { get; }
        // 付与する StatusEffect の識別子。重複付与の判定に使う。付与しないなら null
        public string StatusEffectId { get; }

        private PPEffectEstimate(float aDamage, float aHeal, string aStatusEffectId)
        {
            Damage = aDamage;
            Heal = aHeal;
            StatusEffectId = aStatusEffectId;
        }

        // 効果なしを表す見積もり。推定に対応していない効果はこれを返す
        public static PPEffectEstimate None => default;

        // aDamage : 与ダメージの見込み量
        public static PPEffectEstimate FromDamage(float aDamage) => new(aDamage, 0f, null);

        // aHeal : 回復量の見込み
        public static PPEffectEstimate FromHeal(float aHeal) => new(0f, aHeal, null);

        // aStatusEffectId : 付与する StatusEffect の識別子
        public static PPEffectEstimate FromStatus(string aStatusEffectId) => new(0f, 0f, aStatusEffectId);

        // 別の見積もりと合算する
        // 1 スキルが複数の効果を持つ場合に、それらを 1 つへまとめるのに使う
        // 識別子は先に設定されている方を優先する（複数の付与効果を持つスキルは代表 1 件で判定する）
        // aOther : 合算する見積もり
        // return : 合算後の見積もり
        public PPEffectEstimate Merge(PPEffectEstimate aOther)
            => new(Damage + aOther.Damage,
                   Heal + aOther.Heal,
                   string.IsNullOrEmpty(StatusEffectId) ? aOther.StatusEffectId : StatusEffectId);
    }
}
