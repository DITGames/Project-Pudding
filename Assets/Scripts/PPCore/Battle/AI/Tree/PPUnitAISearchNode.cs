/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAISearchNode.cs
 * @author hqrse
 * @date 2026/08/25
 * @brief 条件に合うユニットを探して対象候補へ積むノード
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // ターゲット検索の探索範囲
    public enum PPUnitAISearchScope
    {
        [InspectorName("味方")]
        Ally,
        [InspectorName("敵")]
        Enemy,
        [InspectorName("味方と敵")]
        Both,
    }

    // 条件に合うユニットを探し、見つかった分を対象候補（PPPartyAIContext.ConditionedUnits）へ積むノード
    //
    // 「誰を狙うか」を決めるのがこのノードの役割で、条件クラス側は判定だけを担う
    // 条件が対象の収集まで兼ねると、判定に使っただけのつもりが候補を書き換えていた、という事故が起きるため、
    // 収集はツリー上の明示的なノードとしてここへ切り出している
    //
    // 積んだ候補は、行動側の対象の選び方で「条件を満たしているユニット」を選ぶと使われる
    // 重複を許して積むと、その分だけランダム選択で選ばれやすくなる
    // 検索ノードを重ねて「複数の条件に当てはまるユニットほど狙われやすい」を作れる
    [Serializable]
    [PPTypeMenuName("制御/ターゲット検索")]
    public sealed class PPUnitAISearchNode : PPUnitAINode
    {
        // 接続口の番号。エディタからの接続操作で使う
        private const int PortFound = 0;
        private const int PortNotFound = 1;

        [Header("探索")]
        [Label("探索範囲")]
        [SerializeField] private PPUnitAISearchScope mScope = PPUnitAISearchScope.Ally;
        // 候補 1 体ずつに対して評価する条件。全て満たしたユニットだけを積む
        [Label("抽出条件", true)]
        [SerializeReference]
        [SerializeField] private List<PPUnitConditionValidator> mConditions = new();
        // 自分自身を探索対象に含めるか。味方への支援で自分を除きたい場合に外す
        [Label("自分自身を含める")]
        [SerializeField] private bool mIsIncludeSelf = true;

        [Header("積み方")]
        // 探索を始める前に、それまでの候補を捨てるか
        // 複数の検索ノードを重ねて候補を積み増したい場合は外す
        [Label("探索前に候補を空にする")]
        [SerializeField] private bool mIsResetBefore = true;
        // 既に積まれているユニットを積み直さないか
        // 外すと重複して積まれ、その分だけランダム選択で選ばれやすくなる
        [Label("重複して積まない")]
        [SerializeField] private bool mIsUnique = true;

        [Header("枝")]
        [Label("見つかったとき")]
        [SerializeField] private string mFoundId = "";
        [Label("見つからなかったとき")]
        [SerializeField] private string mNotFoundId = "";

        protected override string DefaultNodeName => "ターゲット検索";

        public override IReadOnlyList<PPUnitAINodePort> Ports
            => new[]
            {
                new PPUnitAINodePort("発見", ToSingle(mFoundId), false),
                new PPUnitAINodePort("なし", ToSingle(mNotFoundId), false),
            };

        // 探索範囲のユニットを条件で絞り込み、合致したものを対象候補へ積む
        // 1 体でも積めたら発見側の枝へ、1 体も積めなければ なし 側の枝へ進む
        // aContext : 評価 1 回分の入力
        // return : 進んだ枝の結果。枝が未接続なら Failed
        public override PPUnitAINodeResult Evaluate(PPUnitAIEvalContext aContext)
        {
            var snapshot = aContext.Snapshot;
            if (mIsResetBefore)
            {
                snapshot.ResetConditionedUnits();
            }

            int found = 0;
            if (mScope != PPUnitAISearchScope.Enemy) found += Collect(snapshot.AliveMembers, aContext);
            if (mScope != PPUnitAISearchScope.Ally) found += Collect(snapshot.AliveEnemies, aContext);

            var next = aContext.ResolveNode(found > 0 ? mFoundId : mNotFoundId);
            return next == null ? PPUnitAINodeResult.Failed : next.Evaluate(aContext);
        }

        // 指定範囲のユニットを条件で絞り込んで積む
        // aCandidates : 探索対象のユニット
        // aContext : 評価 1 回分の入力
        // return : 積んだ体数
        private int Collect(IReadOnlyList<PPBattleUnit> aCandidates, PPUnitAIEvalContext aContext)
        {
            int count = 0;
            foreach (var candidate in aCandidates)
            {
                if (candidate == null) continue;
                if (!mIsIncludeSelf && ReferenceEquals(candidate, aContext.Unit)) continue;
                // 条件は候補 1 体ずつに対して評価する。思考主体ではない点に注意
                if (!PPUnitConditionValidator.EvaluateAll(mConditions, candidate, aContext.Snapshot)) continue;

                aContext.Snapshot.RegisterConditionedUnit(candidate, mIsUnique);
                count++;
            }
            return count;
        }

        // 指定した接続口へ子ノードを繋ぐ。既に繋がっていた場合は置き換える
        // aPortIndex : 接続口の番号
        // aChildId : 繋ぐ子ノードの ID
        public override void ConnectChild(int aPortIndex, string aChildId)
        {
            if (aPortIndex == PortFound) mFoundId = aChildId;
            else if (aPortIndex == PortNotFound) mNotFoundId = aChildId;
        }

        // 指定した接続口の接続を外す
        // aPortIndex : 接続口の番号
        // aChildId : 外す子ノードの ID
        public override void DisconnectChild(int aPortIndex, string aChildId)
        {
            if (aPortIndex == PortFound && mFoundId == aChildId) mFoundId = "";
            else if (aPortIndex == PortNotFound && mNotFoundId == aChildId) mNotFoundId = "";
        }

        // 単一の接続先を接続口の形式へ揃える。未接続なら空の並びを返す
        // aChildId : 接続先の ID
        // return : 接続口が持つ子ノード ID の並び
        private static IReadOnlyList<string> ToSingle(string aChildId)
            => string.IsNullOrEmpty(aChildId) ? Array.Empty<string>() : new[] { aChildId };
    }
}
