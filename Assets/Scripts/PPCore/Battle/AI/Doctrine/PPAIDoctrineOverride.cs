/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPAIDoctrineOverride.cs
 * @author hqrse
 * @date 2026/08/10
 * @brief 状況ルールが持つ作戦の差分指定
 * =====================================*/

using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // 状況ルールが成立したときに作戦へ適用する差分
    // 上書きフラグが立っている項目だけが適用されるため、
    // 「回復の係数だけ変えたい」ルールを書いても、他の項目には既定作戦の値が残る
    // 全置換にすると、ルールを 1 つ足しただけで既定の調整が黙って失われるため、
    // 「未指定」と「0 を指定」を区別できるようフラグを併記する形にしている
    [Serializable]
    public sealed class PPAIDoctrineOverride
    {
        [Label("ロール別係数を上書きする?")]
        [SerializeField] private bool mIsOverrideRoles = false;
        [Label("ロール別係数", true)]
        [EditCondition(nameof(mIsOverrideRoles), true)]
        [SerializeField] private List<PPRoleValue> mRoles = new();

        [Label("支出上限率を上書きする?")]
        [SerializeField] private bool mIsOverrideSpendCap = false;
        [PercentLabel("支出上限率")]
        [EditCondition(nameof(mIsOverrideSpendCap), true)]
        [SerializeField] private float mSpendCapRatio = 1f;

        [Label("λ倍率を上書きする?")]
        [SerializeField] private bool mIsOverrideLambda = false;
        [Label("λ倍率(0〜3)")]
        [EditCondition(nameof(mIsOverrideLambda), true)]
        [SerializeField] private float mLambdaMultiplier = 1f;

        [Label("取り置きを上書きする?")]
        [SerializeField] private bool mIsOverrideReserves = false;
        [Label("属性別取り置き", true)]
        [EditCondition(nameof(mIsOverrideReserves), true)]
        [SerializeField] private List<PPResourceAmount> mReserves = new();

        // 忍耐倍率は λ が待機判定を代替したため、現状の新 AI では参照されない
        [Label("忍耐倍率を上書きする?(現在未使用)")]
        [SerializeField] private bool mIsOverridePatience = false;
        [Label("忍耐倍率(0〜3)")]
        [EditCondition(nameof(mIsOverridePatience), true)]
        [SerializeField] private float mPatienceMultiplier = 1f;

        // 解決中の作戦へ、上書き指定のある項目だけを適用する
        // aDoctrine : 適用先の作戦。null なら何もしない
        public void ApplyTo(PPAIDoctrine aDoctrine)
        {
            if (aDoctrine == null)
                return;

            // リストは複製して渡す。参照を渡すとプロファイル側の設定を書き換えてしまう
            if (mIsOverrideRoles)
                aDoctrine.Roles = new List<PPRoleValue>(mRoles ?? new List<PPRoleValue>());
            if (mIsOverrideSpendCap)
                aDoctrine.SpendCapRatio = mSpendCapRatio;
            if (mIsOverrideLambda)
                aDoctrine.LambdaMultiplier = mLambdaMultiplier;
            if (mIsOverrideReserves)
                aDoctrine.Reserves = new List<PPResourceAmount>(mReserves ?? new List<PPResourceAmount>());
            if (mIsOverridePatience)
                aDoctrine.PatienceMultiplier = mPatienceMultiplier;
        }

        // デバッグ表示用に、上書きしている項目名を並べた文字列を組み立てる
        // return : 上書き項目の一覧。何も上書きしていなければ「(なし)」
        public string BuildOverrideSummary()
        {
            var items = new List<string>();
            if (mIsOverrideRoles) items.Add("ロール別係数");
            if (mIsOverrideSpendCap) items.Add($"支出上限率={mSpendCapRatio:0.##}");
            if (mIsOverrideLambda) items.Add($"λ倍率={mLambdaMultiplier:0.##}");
            if (mIsOverrideReserves) items.Add("取り置き");
            if (mIsOverridePatience) items.Add($"忍耐倍率={mPatienceMultiplier:0.##}");
            return items.Count == 0 ? "(なし)" : string.Join(" / ", items);
        }
    }
}
