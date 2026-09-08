/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPEffectDefinition.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief PPCore固有のエフェクトデータ定義
 * =====================================*/

using System.Collections.Generic;
using System.Linq;
using CommandBattleCore;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // エフェクト定義（ScriptableObject）
    // ID・表示名・持続期間・スタック挙動・分類といった枠組みを定める
    // 1エフェクト＝1アセットとして運用し、スキル側はこのアセットを参照するだけで独自の値を持たない
    // エフェクト ID は SkillDefinition.mSkillId と同様、アセットごとに手入力する安定した値
    // 効き目そのものは PPStatusEffectBehaviourDefinition のリストとしてインスペクタ上で組み立てる
    // （適用時に攻撃力を上げる、ティックごとにダメージを受ける、等を振る舞いの組み合わせで表現する）
    [CreateAssetMenu(fileName = "PPEffectDefinition", menuName = "Project-Pudding/Effect/PPEffectDefinition")]
    public class PPEffectDefinition : ScriptableObject
    {
        [Header("エフェクト")]
        [Label("エフェクトID")]
        [SerializeField]protected string mEffectId;
        [Label("表示名")]
        [SerializeField]protected string mDisplayName;
        // 持続条件。ターン経過で切れるのか永続なのかを条件の種類として選ぶ
        // 未設定の場合は StatusEffect 側の既定に従い永続になる
        // 単一フィールドのため applyToCollection による回避ができず、[Label] を付けると
        // 属性側の PropertyDrawer が型の PropertyDrawer より優先されて型選択ツリーを出せなくなる
        // そのため表示名はドロワーが読む [PPFieldLabel] で与える
        [PPFieldLabel("持続条件")]
        [SerializeReference]
        [SerializeField]protected PPDurationConditionDefinition mDurationCondition = new PPTurnDurationConditionDefinition();
        [Label("スタックポリシー")]
        [SerializeField]protected StatusEffectStackPolicy mStackPolicy = StatusEffectStackPolicy.Refresh;
        [Label("最大スタック")]
        [SerializeField]protected int mMaxStack = 1;
        // 何をきっかけにスタックが減るか。未設定ならスタックは自然には減らない（持続条件が切れるまで残る）
        // 持続条件と違い「行動したら消える」「攻撃したら消える」といった寿命以外の切れ方を表す
        [PPFieldLabel("スタック消費ルール")]
        [SerializeReference]
        [SerializeField]protected PPStatusEffectConsumeRuleDefinition mConsumeRule;
        // UI・AI・解除スキルから共通に参照できるゲーム固有分類。振る舞いの構成とは独立して手入力する
        [Label("分類")]
        [SerializeField]protected PPEffectCategory mCategory = PPEffectCategory.None;
        // Coreが理解できる汎用分類。振る舞いの構成とは独立して手入力する
        [Label("タグ")]
        [SerializeField]protected StatusEffectTag mTags = StatusEffectTag.None;

        // 効き目そのもの。振る舞いを積み上げて 1 つの状態異常を表現する
        // [Label] は必ず applyToCollection = true で付ける
        // false（既定）だとラベルの PropertyDrawer が各要素へ適用され、
        // 要素側の型選択ツリー（PPStatusEffectBehaviourDefinitionDrawer）が呼ばれなくなる
        [Header("振る舞い")]
        [Label("振る舞い", true)]
        [SerializeReference]
        [SerializeField]protected List<PPStatusEffectBehaviourDefinition> mBehaviours = new();

        public string EffectId => mEffectId;
        public string DisplayName => mDisplayName;
        public PPDurationConditionDefinition DurationCondition => mDurationCondition;
        public StatusEffectStackPolicy StackPolicy => mStackPolicy;
        public PPEffectCategory Category => mCategory;
        public StatusEffectTag Tags => mTags;

        // この定義からランタイムのステータスエフェクトを生成する
        // 共通部分(ID・表示名・持続期間・スタック・分類)を組み立てたのち、
        // mBehaviours の各要素へ効き目の組み立てを委ねる
        // aSource : エフェクトの付与元ユニット
        // aTarget : 付与される対象ユニット
        // aContext : バトルコンテキスト
        // aDurationOverride : スキル側から差し替える持続条件。null なら本定義の設定を使う
        // return : 生成されたステータスエフェクト
        public StatusEffect CreateRuntimeStatusEffect(BattleUnit aSource, BattleUnit aTarget, BattleContext aContext,
            PPDurationConditionDefinition aDurationOverride = null)
        {
            // 効き目は同じで持続だけスキルごとに変えたい場合があるため、スキル側の指定を優先する
            // エフェクト ID は変えないので、上書きしてもスタック・解除・AI 判定は同じエフェクトとして扱われる
            var durationSource = aDurationOverride ?? mDurationCondition;

            // 持続条件は付与のたびに作り直す（残りターン数のような状態を持つため使い回さない）
            // 未設定なら null を渡し、StatusEffect 側の既定である PermanentDurationCondition に委ねる
            var effect = new StatusEffect(mEffectId, mDisplayName, durationSource?.CreateDurationCondition())
                .WithSource(aSource)
                .WithSourceDefinition(this)
                .WithStacking(mStackPolicy, mMaxStack)
                .WithConsumeRule(mConsumeRule?.CreateConsumeRule())
                .WithCategory((long)mCategory)
                .WithTags(mTags);

            foreach (var behaviour in mBehaviours)
            {
                behaviour?.ConfigureBehaviour(effect, aSource, aTarget, aContext);
            }
            return effect;
        }

        // フィールドラベルに表示する、この StatusEffect の内容を要約した文字列を組み立てる
        public virtual string BuildString()
            => mBehaviours.Count == 0
                ? "（振る舞い未設定）"
                : string.Join("、", mBehaviours.Select(b => b?.BuildString() ?? "（未設定）"));
    }
}
