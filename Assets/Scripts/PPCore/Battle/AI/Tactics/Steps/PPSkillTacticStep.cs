/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillTacticStep.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief 戦術ステップ : 指定タグのスキルを使う
 * =====================================*/

using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // 戦術ステップ: タグで指定したスキルを撃つ
    // 候補が複数ある場合はスキル選択ルールで 1 つに絞る
    // 発動可否の判定でリソース不足だけは候補から外さないのが要点で、
    // 「今は買えないが溜めれば撃てる」ステップを戦術側の待機判断へ回すためにこうしている
    [Serializable]
    [PPTypeMenuName("行動/スキル使用")]
    public sealed class PPSkillTacticStep : PPTacticStepBase
    {
        // このタグのいずれかを持つスキルが候補になる。空なら全スキルが候補
        [Label("対象スキルタグ", true)]
        [SerializeField] private List<PPSkillTagDefinition> mSkillTags = new();
        [Label("スキル選択ルール")]
        [SerializeField] private PPTacticSelectRule mSkillSelectRule = PPTacticSelectRule.HighestAIScore;

        public IReadOnlyList<PPSkillTagDefinition> SkillTags => mSkillTags;

        // このステップを今の盤面へ当てはめる
        // 実行者 → 使用スキル → 対象 の順に解決し、どれかが決まらなければ理由付きで失敗を返す
        // aSnap : パーティ状況スナップショット
        // aRuntime : 進行状況を保持するランタイム戦術
        // aLedger : 行動回数の仮押さえ帳
        // aReason : 解決できなかった場合の理由
        // return : 解決結果。解決できない場合は null
        public override PPTacticStepResolution Resolve(PPPartyAIContext aSnap, PPRuntimeTactics aRuntime,
            PPTacticActionLedger aLedger, out PPTacticRejectReason aReason)
        {
            var (actor, skill, definition) = PPTacticUnitSelector.SelectActorSkill(
                CollectEntries(aSnap, aLedger), mActorSelectRule, aSnap.Context);
            if (actor == null)
            {
                aReason = PPTacticRejectReason.NoActor;
                return null;
            }

            var target = ResolveTarget(aSnap, aRuntime, actor);
            if (IsRequireTarget && target == null)
            {
                aReason = PPTacticRejectReason.NoTarget;
                return null;
            }

            // ラムダに載せるためローカルへ退避する
            var resolver = BuildResolver(definition.TargetScope, target);
            var user = actor;
            var used = skill;

            aReason = PPTacticRejectReason.None;
            return new PPTacticStepResolution
            {
                Actor = actor,
                Target = target,
                Skill = skill,
                Cost = definition.Cost,
                RequiredActionCount = RequiredActionCount,
                BuildCommand = _ => new PPSkillCommand(user, used, resolver),
            };
        }

        // 実行者と、その人が撃つスキルの組を作る
        // 実行者を先に 1 体決めてからスキルを探すと、スキルを持たない人が選ばれた時点で
        // 他に撃てる人が居ても打ち切られてしまうため、組にしてから選ぶ
        // ユニット内での使用スキルはここで決め、ユニット間の比較はその結果に対して行う
        // aSnap : パーティ状況スナップショット
        // aLedger : 行動回数の仮押さえ帳
        // return : 実行できるユニットと、その人が使うスキルの組
        private List<(PPBattleUnit Actor, PPBattleSkill Skill, PPSkillDefinition Definition)> CollectEntries(
            PPPartyAIContext aSnap, PPTacticActionLedger aLedger)
        {
            var entries = new List<(PPBattleUnit, PPBattleSkill, PPSkillDefinition)>();
            foreach (var unit in CollectActorCandidates(aSnap, aLedger))
            {
                var skills = CollectCastableSkills(unit, aSnap.Context);
                if (skills.Count == 0) continue;

                var (skill, definition) = PPTacticUnitSelector.SelectSkill(skills, mSkillSelectRule, aSnap.Context);
                entries.Add((unit, skill, definition));
            }
            return entries;
        }

        // 実行者を 1 体選ぶ
        // 撃てるスキルを持つユニットだけを候補にし、比較値もこのステップのタグに
        // 合致したスキルの値を使う（基底の実装は全所持スキルから集計してしまう）
        // aSnap : パーティ状況スナップショット
        // aLedger : 行動回数の仮押さえ帳
        // return : 選ばれた実行者。候補が居なければ null
        protected override PPBattleUnit SelectActor(PPPartyAIContext aSnap, PPTacticActionLedger aLedger)
            => PPTacticUnitSelector.SelectActorSkill(CollectEntries(aSnap, aLedger), mActorSelectRule, aSnap.Context).Actor;

        // タグに合致し、リソース以外の理由では弾かれないスキルを集める
        // リソース不足（NotEnoughResource）だけは候補に残す
        // ここで落としてしまうと、戦術側で「待てば撃てるか」を判断する機会が無くなるため
        // aActor : 対象ユニット
        // aContext : 発動可否の検証に使うバトルコンテキスト
        // return : 候補になったスキルと定義の組
        private List<(PPBattleSkill Skill, PPSkillDefinition Definition)> CollectCastableSkills(
            PPBattleUnit aActor, BattleContext aContext)
        {
            var list = new List<(PPBattleSkill, PPSkillDefinition)>();
            foreach (var (skill, definition) in PPSkillTagUtility.EnumerateTaggedSkills(aActor, mSkillTags))
            {
                var validation = aContext.Rules.CastValidator.Validate(aActor, skill, aContext);
                if (!validation.CanCast && validation.Reason != CastFailReason.NotEnoughResource) continue;

                list.Add((skill, definition));
            }
            return list;
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
            => mDescription = $"{PPSkillTagUtility.ToDisplayString(mSkillTags)} のスキルを {GetTargetPolicyString(TargetPolicy)} へ";
    }
}
