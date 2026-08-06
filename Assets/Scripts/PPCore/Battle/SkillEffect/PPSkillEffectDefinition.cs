/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillEffectDefinition.cs
 * @author hqrse
 * @date 2026/08/06
 * @brief スキルエフェクト定義の抽象基底
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;

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

        // リストの要素ラベルに表示する、この効果の内容を要約した文字列を組み立てる
        // ApplyTarget は含めず、エフェクト固有の設定値のみを表す
        public abstract string BuildString();
    }
}
