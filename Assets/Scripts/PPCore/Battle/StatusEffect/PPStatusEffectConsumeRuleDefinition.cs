/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStatusEffectConsumeRuleDefinition.cs
 * @author hqrse
 * @date 2026/09/04
 * @brief スタック消費ルールのデータ定義（[SerializeReference] 対応の通常クラス）の抽象基底
 * =====================================*/

using System;
using CommandBattleCore;

namespace PPCore
{
    // 「何をきっかけに、何スタック消費するか」をインスペクタ上で組み立てるためのデータ定義
    // 持続条件（PPDurationConditionDefinition）が寿命を表すのに対し、こちらは消費のきっかけを表す
    // スタック数は効果量の倍率でもあるため、両者は別の設定として持たせている
    [Serializable]
    public abstract class PPStatusEffectConsumeRuleDefinition
    {
        // この定義から消費ルールのランタイムインスタンスを生成する
        // return : 生成された消費ルール
        public abstract IStatusEffectConsumeRule CreateConsumeRule();

        // フィールドラベルに表示する、この消費ルールの内容を要約した文字列を組み立てる
        public abstract string BuildString();

        // ダメージ種別の日本語表示名を得る
        // aReason : 対象のダメージ種別
        // return : 表示名
        protected static string ToDisplayName(DamageReason aReason)
            => aReason switch
            {
                DamageReason.Attack => "攻撃",
                DamageReason.StatusEffect => "継続ダメージ",
                DamageReason.Other => "その他",
                _ => aReason.ToString(),
            };

        // きっかけの日本語表示名を得る
        // aTrigger : 対象のきっかけ
        // return : 表示名
        protected static string ToDisplayName(StatusEffectConsumeTrigger aTrigger)
            => aTrigger switch
            {
                StatusEffectConsumeTrigger.TurnEnded => "ターン経過",
                StatusEffectConsumeTrigger.Acted => "行動",
                StatusEffectConsumeTrigger.Attacked => "攻撃",
                StatusEffectConsumeTrigger.Damaged => "被ダメージ",
                _ => aTrigger.ToString(),
            };
    }
}
