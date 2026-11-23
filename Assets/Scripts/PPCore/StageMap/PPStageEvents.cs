/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageEvents.cs
 * @author hqrse
 * @date 2026/11/21
 * @brief マスを選んだときに起きるイベント（バトル・強化・ユニット獲得）
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace PPCore
{
    // イベントへ渡す、選んだマスと進行状態と、終わったときの知らせ先
    public sealed class PPStageEventContext
    {
        public PPStageProgress Progress;
        public PPStageMapNode Node;
        public string BattleSceneName;
        public PPStageRecruitView RecruitView;
        // イベントがその場で終わったとき(マップに出す文)。バトルはシーンを移るため呼ばない
        public Action<string> OnFinished;
    }

    // マスの種類ごとのイベント。終わったら OnFinished でマップへ戻す
    public interface IPPStageEvent
    {
        // そのマスに入れない理由を返す。入れるなら null
        string GetBlockReason(PPStageProgress aProgress, PPStageMapNode aNode);

        void Begin(PPStageEventContext aContext);
    }

    // マスの種類からイベントを選ぶ共通の入口
    public static class PPStageEvents
    {
        /* static 変数 */

        private static readonly IPPStageEvent sBattle = new PPStageBattleEvent();
        private static readonly IPPStageEvent sUpgrade = new PPStageUpgradeEvent();
        private static readonly IPPStageEvent sRecruit = new PPStageRecruitEvent();

        /* 問い合わせ */

        public static IPPStageEvent Resolve(PPStageNodeType aType) => aType switch
        {
            PPStageNodeType.Upgrade => sUpgrade,
            PPStageNodeType.Recruit => sRecruit,
            _ => sBattle,
        };
    }

    // バトルのマス。敵と戻り先を進行状態へ預けてから、バトルのシーンを開く
    public class PPStageBattleEvent : IPPStageEvent
    {
        public virtual string GetBlockReason(PPStageProgress aProgress, PPStageMapNode aNode)
        {
            if (!aProgress.CanEnterBattle) return "中央にユニットがいないため、バトルのマスは選べません";
            if (aNode.Enemy == null) return "このマスの敵が設定されていません";
            return null;
        }

        // 切り替えを受け付けられなかったときは、預けた敵を取り消してマップに残る
        public virtual void Begin(PPStageEventContext aContext)
        {
            var progress = aContext.Progress;
            var node = aContext.Node;
            progress.BeginBattle(new PPStageBattleRequest(node.Key, node.Type, node.Enemy, node.Encounter));
            progress.ReturnSceneName = SceneManager.GetActiveScene().name;
            if (!PPSceneTransition.Load(aContext.BattleSceneName)) progress.BeginBattle(null);
        }
    }

    // 強化のマス。所持ユニットのうち一番レベルが低い 1 体（同じなら先に加わった方）のレベルを 1 上げる
    public class PPStageUpgradeEvent : IPPStageEvent
    {
        public virtual string GetBlockReason(PPStageProgress aProgress, PPStageMapNode aNode)
            => aProgress.OwnedUnits.Count == 0 ? "所持ユニットがいません" : null;

        public virtual void Begin(PPStageEventContext aContext)
        {
            var target = SelectTarget(aContext.Progress);
            aContext.Progress.LevelUp(target);
            aContext.OnFinished?.Invoke($"{target.Unit.DisplayName} のレベルが {target.Level} に上がった");
        }

        protected virtual PPStageOwnedUnit SelectTarget(PPStageProgress aProgress)
        {
            PPStageOwnedUnit target = null;
            foreach (var owned in aProgress.OwnedUnits)
            {
                if (target == null || owned.Level < target.Level) target = owned;
            }
            return target;
        }
    }

    // ユニット獲得のマス。候補から選んだ 1 体を所持ユニットに加える
    // 候補はシードとマスから決めるため、同じマスでは何度開いても同じになる
    public class PPStageRecruitEvent : IPPStageEvent
    {
        public virtual string GetBlockReason(PPStageProgress aProgress, PPStageMapNode aNode)
        {
            if (aProgress.Definition.RecruitCandidates.Count == 0) return "ユニット獲得の候補が設定されていません";
            return null;
        }

        public virtual void Begin(PPStageEventContext aContext)
        {
            var progress = aContext.Progress;
            var candidates = CreateOffers(progress, aContext.Node);
            int level = progress.Definition.RecruitLevel;
            aContext.RecruitView.Open(candidates, level, aUnit =>
            {
                var owned = progress.AddUnit(aUnit, level);
                var position = progress.FindPosition(owned);
                aContext.OnFinished?.Invoke(position.HasValue
                    ? $"{aUnit.DisplayName} が仲間になり、編成に入った"
                    : $"{aUnit.DisplayName} が仲間になった（編成に空きがないため控え）");
            });
        }

        // 候補を並べ替えて、先頭から画面に出す数だけ取る
        public static List<PPUnitDefinition> CreateOffers(PPStageProgress aProgress, PPStageMapNode aNode)
        {
            var pool = new List<PPUnitDefinition>();
            foreach (var unit in aProgress.Definition.RecruitCandidates)
            {
                if (unit != null) pool.Add(unit);
            }

            // HashCode.Combine は起動ごとに値が変わるため、シードを持ち越せるよう自前で混ぜる
            int seed = unchecked((aProgress.Seed * 397 + aNode.Floor) * 397 + aNode.Column);
            var random = new Random(seed);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            int count = Math.Min(pool.Count, aProgress.Definition.RecruitOfferCount);
            return pool.GetRange(0, count);
        }
    }
}
