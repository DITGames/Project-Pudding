/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPAIDoctrine.cs
 * @author hqrse
 * @date 2026/08/10
 * @brief パーティAIの作戦。リソース運用方針を含む状況別の振る舞い指定
 * =====================================*/

using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // 「今どういう方針でリソースを使うか」をまとめた作戦
    // プロファイルの既定作戦を起点に、成立した状況ルールの差分（PPAIDoctrineOverride）を
    // 優先度順へ重ねていくことで、そのティックの最終的な方針が決まる
    // ロール別係数しか持たなかった PPAISituationScore に対し、
    // 支出上限・λ倍率・取り置きといったリソース運用の指定を持てるようにしたもの
    [Serializable]
    public sealed class PPAIDoctrine
    {
        [Label("ロール別係数", true)]
        [SerializeField] private List<PPRoleValue> mRoles = new();

        [PercentLabel("支出上限率")]
        [SerializeField] private float mSpendCapRatio = 1f;

        [Label("λ倍率")]
        [Range(0f, 3f)]
        [SerializeField] private float mLambdaMultiplier = 1f;

        [Label("属性別取り置き", true)]
        [SerializeField] private List<PPResourceAmount> mReserves = new();

        // 待機判定は λ が代替しているため、現状の新 AI はこの値を参照しない
        // 溜めの粘りを調整したい場合は λ 倍率か、プロファイルの警戒度を使う
        [Label("忍耐倍率(現在未使用)")]
        [Range(0f, 3f)]
        [SerializeField] private float mPatienceMultiplier = 1f;

        // ロール別の効用係数。登録の無いロールは 1（補正なし）として扱われる
        public List<PPRoleValue> Roles { get => mRoles; set => mRoles = value; }
        // このティックで使ってよい残量の割合
        public float SpendCapRatio { get => mSpendCapRatio; set => mSpendCapRatio = value; }
        // 算出された λ に掛ける補正。高いほどリソースを出し惜しみする
        public float LambdaMultiplier { get => mLambdaMultiplier; set => mLambdaMultiplier = value; }
        // 属性別に使わず残しておく量
        public List<PPResourceAmount> Reserves { get => mReserves; set => mReserves = value; }
        // 忍耐係数への乗算補正
        public float PatienceMultiplier { get => mPatienceMultiplier; set => mPatienceMultiplier = value; }

        // 既定作戦を複製して、差分適用に使う作業用インスタンスを作る
        // リストは中身ごと複製する。参照をそのまま渡すと、差分適用で
        // プロファイルアセット側の既定作戦を書き換えてしまう
        // aSource : 複製元。null なら既定値のインスタンスを返す
        // return : 複製されたインスタンス
        public static PPAIDoctrine From(PPAIDoctrine aSource)
        {
            if (aSource == null)
                return new PPAIDoctrine();

            return new PPAIDoctrine
            {
                mRoles = new List<PPRoleValue>(aSource.mRoles ?? new List<PPRoleValue>()),
                mSpendCapRatio = aSource.mSpendCapRatio,
                mLambdaMultiplier = aSource.mLambdaMultiplier,
                mReserves = new List<PPResourceAmount>(aSource.mReserves ?? new List<PPResourceAmount>()),
                mPatienceMultiplier = aSource.mPatienceMultiplier,
            };
        }

        // 指定属性の取り置き量を引く。登録が無ければ 0
        // a : 対象の属性
        public float ReserveOf(PPTypeAttribute a)
        {
            if (mReserves == null)
                return 0f;

            float total = 0f;
            foreach (var entry in mReserves)
            {
                if (entry.Type == a) total += Mathf.Max(0f, entry.Amount);
            }
            return total;
        }
    }
}
