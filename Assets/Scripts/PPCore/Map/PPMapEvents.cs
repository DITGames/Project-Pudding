/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPMapEvents.cs
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
    public sealed class PPMapEventContext
    {
        public PPMapProgress Progress;
        public PPMapNode Node;
        public string BattleSceneName;
        public PPMapRecruitView RecruitView;
        // イベントがその場で終わったとき(マップに出す文)。バトルはシーンを移るため呼ばない
        public Action<string> OnFinished;
    }

    // マスの種類ごとのイベント。終わったら OnFinished でマップへ戻す
    public interface IPPMapEvent
    {
        // そのマスに入れない理由を返す。入れるなら null
        string GetBlockReason(PPMapProgress aProgress, PPMapNode aNode);

        void Begin(PPMapEventContext aContext);
    }

    // マスの種類からイベントを選ぶ共通の入口
    public static class PPMapEvents
    {
        /* static 変数 */

        private static readonly IPPMapEvent sBattle = new PPMapBattleEvent();
        private static readonly IPPMapEvent sUpgrade = new PPMapUpgradeEvent();
        private static readonly IPPMapEvent sRecruit = new PPMapRecruitEvent();

        /* 問い合わせ */

        public static IPPMapEvent Resolve(PPMapNodeType aType) => aType switch
        {
            PPMapNodeType.Upgrade => sUpgrade,
            PPMapNodeType.Recruit => sRecruit,
            _ => sBattle,
        };
    }

    // バトルのマス。敵と戻り先を進行状態へ預けてから、バトルのシーンを開く
    public class PPMapBattleEvent : IPPMapEvent
    {
        public virtual string GetBlockReason(PPMapProgress aProgress, PPMapNode aNode)
        {
            if (!aProgress.CanEnterBattle) return "編成にユニットがいないため、バトルのマスは選べません";
            if (aNode.Battle == null) return "このマスの敵が設定されていません";
            return null;
        }

        // 切り替えを受け付けられなかったときは、預けた敵を取り消してマップに残る
        public virtual void Begin(PPMapEventContext aContext)
        {
            var progress = aContext.Progress;
            var node = aContext.Node;
            progress.BeginBattle(new PPMapBattleRequest(node.Key, node.Type, node.Battle));
            progress.ReturnSceneName = SceneManager.GetActiveScene().name;
            if (!PPSceneTransition.Load(aContext.BattleSceneName)) progress.BeginBattle(null);
        }
    }

    // 強化のマス。所持ユニットのうち一番レベルが低い 1 体（同じなら先に加わった方）のレベルを 1 上げる
    public class PPMapUpgradeEvent : IPPMapEvent
    {
        public virtual string GetBlockReason(PPMapProgress aProgress, PPMapNode aNode)
            => aProgress.OwnedUnits.Count == 0 ? "所持ユニットがいません" : null;

        public virtual void Begin(PPMapEventContext aContext)
        {
            var target = SelectTarget(aContext.Progress);
            aContext.Progress.LevelUp(target);
            aContext.OnFinished?.Invoke($"{target.Unit.DisplayName} のレベルが {target.Level} に上がった");
        }

        protected virtual PPMapOwnedUnit SelectTarget(PPMapProgress aProgress)
        {
            PPMapOwnedUnit target = null;
            foreach (var owned in aProgress.OwnedUnits)
            {
                if (target == null || owned.Level < target.Level) target = owned;
            }
            return target;
        }
    }

    // ユニット獲得のマス。候補から選んだ 1 体を所持ユニットに加える
    // 候補はシードとマスから決めるため、同じマスでは何度開いても同じになる
    public class PPMapRecruitEvent : IPPMapEvent
    {
        public virtual string GetBlockReason(PPMapProgress aProgress, PPMapNode aNode)
        {
            if (aProgress.Definition.RecruitCandidates.Count == 0) return "ユニット獲得の候補が設定されていません";
            return null;
        }

        public virtual void Begin(PPMapEventContext aContext)
        {
            var progress = aContext.Progress;
            var candidates = CreateOffers(progress, aContext.Node);
            int level = progress.Definition.RecruitLevel;
            aContext.RecruitView.Open(candidates, level, aUnit =>
            {
                var owned = progress.AddUnit(aUnit, level);
                var position = progress.FindSlot(owned);
                aContext.OnFinished?.Invoke(position.HasValue
                    ? $"{aUnit.DisplayName} が仲間になり、編成に入った"
                    : $"{aUnit.DisplayName} が仲間になった（編成に空きがないか、コストの上限を超えるため控え）");
            });
        }

        // 候補を並べ替えて、先頭から画面に出す数だけ取る
        public static List<PPUnitDefinition> CreateOffers(PPMapProgress aProgress, PPMapNode aNode)
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
