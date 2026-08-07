/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyAIProfileDefinition.cs
 * @author hqrse
 * @date 2026/07/16
 * @brief 敵パーティAIの性格定義
 * =====================================*/

using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // 状況が成立したときに適用される係数一式
    // ロール別の状況係数（登録が無いロールは 1 = 補正なし）に加えて、
    // 忍耐係数をこの状況だけ上書きできる
    // 「ピンチなので回復優先」といった振る舞いはここで表現する
    [Serializable]
    public sealed class PPAISituationScore
    {
        // ロール別の状況係数。登録の無いロールは 1（補正なし）として扱われる
        [Label("ロール別係数", true)] public List<PPRoleValue> Roles = new();

        // この状況における忍耐係数の乗算補正。待機判定の許容ティック数に掛かる
        [Header("プロファイル上書き")]
        [Label("忍耐係数の乗算補正")][Range(0, 3)] public float PatienceMultiplier = 1f;
    }

    // ロール別の実行順序。同一ティックで複数行動が採用されたときの並び順を決める
    // 値が小さいほど先に実行される（既定では支援 → 攻撃 → 回復の順）
    [Serializable]
    public sealed class PPRoleOrder
    {
        [Label(PPBattleUtilityDefinition.RoleNameAttack)]
        public int Attack = 1;
        [Label(PPBattleUtilityDefinition.RoleNameSupport)] public int Support = 0;
        [Label(PPBattleUtilityDefinition.RoleNameHeal)] public int Heal = 2;
        // ロール未分類（Special 含む）の行動の実行順序
        [Label("デフォルト")] public int Default = 3;
    }

    // 状況ルール 1 件。条件をすべて満たしたときにスコアが適用される
    // 条件は AND 判定で、プロファイル内で後に定義されたルールほど優先される
    [Serializable]
    public sealed class PPPartyAISituationRule
    {
        // ルール名。デバッグ表示に使われる
        [Label("ルール名")] public string Name = "New Situation";
        [SerializeReference]
        [Label("条件リスト", true)] public List<PPPartyConditionValidator> Conditions = new();
        [Label("成立時スコア")] public PPAISituationScore Score = new();
    }


    // 敵パーティ AI の性格を定義する ScriptableObject
    // PPPartyAIStrategistBase の挙動はほぼすべてこのアセットの値で決まる
    // 大きく「性能（警戒度・知能）」「リソース感度」「行動制御」
    // 「ロール別重みと実行順序」「状況別ルール」に分かれる
    // スキル自体の強さは PPSkillDefinition 側のロール別AIScoreで表現するため、
    // このアセットは「性格」（状況への反応・ロールの好み）だけを持つ
    [CreateAssetMenu(fileName = "PPPartyAIProfileDefinition", menuName = "Project-Pudding/AI/PPPartyAIProfileDefinition")]
    public class PPPartyAIProfileDefinition : ScriptableObject
    {
        // 警戒度（0〜1）。高いほど「待って溜める」判断をしなくなる
        [Header("性能")]
        [PercentLabel("警戒度")] public float Caution = 0.5f;
        // 知能（0〜1）。低いほど選択にノイズが乗り、最適解を外しやすくなる
        [PercentLabel("知能")] public float Intelligence = 0.5f;

        // リソース増加トレンドの平均を取るサンプル数
        [Header("リソース")]
        [Label("リソース推移サンプル数")] public int TrendSampleCount = 4;

        // 思考間隔（秒）。PPEnemyAIDriver の駆動周期
        [Header("行動")]
        [Label("思考間隔(秒)")] public float ThinkInterval = 0.5f;
        // 1 ティックで採用する行動数の上限
        [Label("同時行動数上限")] public int MaxActionsPerTick = 3;
        // 行動選択に乗せるノイズの最大比率。知能が低いほどこの幅いっぱいまでぶれる
        [Label("行動選択ノイズ最大比率")][Range(0, 1)] public float ActionNoiseAmplitude = 0.6f;

        // ロール別重み。状況係数とは別に、AI の基本的な行動傾向（性格）を決める
        // 登録の無いロールは 1（補正なし）として扱われる
        [Header("ロール")]
        [Label("重み", true)] public List<PPRoleValue> Weights = new();
        [Label("行動順")] public PPRoleOrder Order = new();

        // どの状況ルールも成立しなかった場合に使われる既定スコア
        [Header("シチュエーション")]
        [Label("デフォルトルール")] public PPAISituationScore DefaultScore = new();
        // 状況別ルール。上から順に評価され、成立したもので上書きされる（後勝ち）
        [Label("シチュエーション別ルール")] public List<PPPartyAISituationRule> Rules = new();
    }
}
