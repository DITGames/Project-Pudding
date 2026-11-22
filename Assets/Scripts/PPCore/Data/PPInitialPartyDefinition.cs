/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPInitialPartyDefinition.cs
 * @author hqrse
 * @date 2026/11/22
 * @brief はじめからで選ぶ初期パーティの定義
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // 初期パーティに入るユニット 1 体分
    [Serializable]
    public sealed class PPInitialPartyMember
    {
        [Label("ユニット定義")] public PPUnitDefinition Unit;
        [Label("レベル")] [Min(1)] public int Level = 1;
    }

    // はじめからで候補から 1 体を選び、一緒に始めるユニットと合わせて最初のパーティを作る
    [CreateAssetMenu(fileName = "PIPD_New", menuName = "Project-Pudding/Definition/PPInitialPartyDefinition")]
    public class PPInitialPartyDefinition : ScriptableObject
    {
        /* インスペクタ設定 */

        // 選択画面に並べる順
        [Header("候補")]
        [Label("候補", true)]
        [SerializeField] private List<PPInitialPartyMember> mCandidates = new();

        // 選んだユニットの後に、編成の空き(右 → 左)、さらに控えの順に入る
        [Header("一緒に始めるユニット")]
        [Label("一緒に始めるユニット", true)]
        [SerializeField] private List<PPInitialPartyMember> mCompanions = new();

        [Header("パーティ")]
        [Label("コイン変換レート初期値")]
        [SerializeField] private float mCoinConversionRate = 1f;

        /* プロパティ */

        public IReadOnlyList<PPInitialPartyMember> Candidates => mCandidates;
        public IReadOnlyList<PPInitialPartyMember> Companions => mCompanions;
        public float CoinConversionRate => mCoinConversionRate;

        /* 問い合わせ */

        // ユニット定義が設定されている候補だけを返す
        public List<PPInitialPartyMember> GetValidCandidates()
        {
            var list = new List<PPInitialPartyMember>(mCandidates.Count);
            foreach (var member in mCandidates)
            {
                if (member != null && member.Unit != null) list.Add(member);
            }
            return list;
        }

        // 選んだ 1 体を先頭に、一緒に始めるユニットを続けた所持ユニットの並びを返す。この順で編成に入る
        public List<PPPartySaveUnitEntry> CreateUnits(PPInitialPartyMember aPicked)
        {
            var list = new List<PPPartySaveUnitEntry>(1 + mCompanions.Count);
            if (aPicked != null && aPicked.Unit != null) list.Add(new PPPartySaveUnitEntry(aPicked.Unit, aPicked.Level));
            foreach (var member in mCompanions)
            {
                if (member != null && member.Unit != null) list.Add(new PPPartySaveUnitEntry(member.Unit, member.Level));
            }
            return list;
        }
    }
}
