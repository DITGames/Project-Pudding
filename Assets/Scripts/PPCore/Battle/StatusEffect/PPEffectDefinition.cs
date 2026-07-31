/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPEffectDefinition.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief PPCore固有のエフェクトデータ定義
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // エフェクト定義（ScriptableObject）の抽象基底
    // ID・表示名・持続期間・スタック挙動といった枠組みを定める
    // エフェクト ID はインスペクタでの手入力ではなく BuildAutoEffectId で設定内容から自動生成する
    // 同じ効果に別 ID が付いてスタック判定が働かなくなるのを防ぐため
    // 効き目そのものは ConfigureBehaviours で StatusEffectBehaviour を組み立てる形に一本化されており、
    // ランタイムインスタンスの生成自体は本基底が一手に引き受ける
    public abstract class PPEffectDefinition : ScriptableObject
    {
        // エフェクトID。BuildAutoEffectId により自動設定されるため手入力しない
        [Header("エフェクト")]
        [Label("エフェクトID")]
        [SerializeField]protected string mEffectId;
        [Label("表示名")]
        [SerializeField]protected string mDisplayName;
        [Label("期間")]
        [SerializeField]protected int mDuration = 3;
        [Label("スタックポリシー")]
        [SerializeField]protected StatusEffectStackPolicy mStackPolicy = StatusEffectStackPolicy.Refresh;
        [Label("最大スタック")]
        [SerializeField]protected int mMaxStack = 1;

        public string EffectId => mEffectId;
        public string DisplayName => mDisplayName;
        public int Duration => mDuration;
        public StatusEffectStackPolicy StackPolicy => mStackPolicy;

        // 分類。UI・AI・解除スキルから共通に参照できるよう基底で公開する
        public abstract PPEffectCategory Category { get; }
        // Coreが理解できる汎用分類
        public abstract StatusEffectTag Tags { get; }

        // この定義からランタイムのステータスエフェクトを生成する
        // 共通部分(ID・表示名・持続期間・スタック・分類)を組み立てたのち、
        // 派生側の ConfigureBehaviours へ効き目の組み立てを委ねる
        // aSource : エフェクトの付与元ユニット
        // aTarget : 付与される対象ユニット
        // aContext : バトルコンテキスト
        // return : 生成されたステータスエフェクト
        public StatusEffect CreateRuntimeStatusEffect(BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            var effect = new StatusEffect(mEffectId, mDisplayName, new TurnDurationCondition(mDuration))
                .WithSource(aSource)
                .WithSourceDefinition(this)
                .WithStacking(mStackPolicy, mMaxStack)
                .WithCategory((long)Category)
                .WithTags(Tags);

            ConfigureBehaviours(effect, aSource, aTarget, aContext);
            return effect;
        }

        // 効果の中身。派生クラスが AddBehaviour で組み立てる
        // aEffect : 設定対象のエフェクト
        // aSource : エフェクトの付与元ユニット
        // aTarget : 付与される対象ユニット
        // aContext : バトルコンテキスト
        protected abstract void ConfigureBehaviours(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext);

        // 設定内容からエフェクト ID を組み立てる。派生側で実装する
        // 同じ効果の定義が同じ ID になるようにすること（スタック判定の単位になるため）
        // return : 自動生成されたエフェクトID
        protected abstract string BuildAutoEffectId();

        // インスペクタでの変更時にエフェクト ID を再生成して同期させる
        private void OnValidate()
        {
            var autoId = BuildAutoEffectId();
            mEffectId = autoId;
        }
    }
}
