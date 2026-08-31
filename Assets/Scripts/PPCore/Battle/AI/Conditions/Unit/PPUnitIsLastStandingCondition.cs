/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitIsLastStandingCondition.cs
 * @author hqrse
 * @date 2026/08/27
 * @brief ユニット条件 : 自分が最後の1体か
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // ユニット条件: 生存している味方が自分 1 体だけか
    //
    // 「独りになったら自己強化して粘る」「味方が居るうちは支援に徹する」といった切り替えに使う
    // 生存数の条件（PPAliveUnitCountCondition）でも数は見られるが、
    // こちらは「残っているのが自分か」まで見るため、
    // 同じツリーを複数ユニットで共有していても本人だけが反応する
    [Serializable]
    [PPTypeMenuName("戦況/自分が最後の1体")]
    public sealed class PPUnitIsLastStandingCondition : PPUnitConditionValidator
    {
        // 反転すると「まだ味方が残っている」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 生存している味方が自分だけかを判定する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            if (aUnit == null) return false;

            var members = aSnapShot.AliveMembers;
            bool isLast = members.Count == 1 && members[0] == aUnit;
            return isLast != mIsInvert;
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
            => mDescription = mIsInvert ? "自分以外にも味方が生存している" : "生存している味方が自分だけ";
    }
}
