/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAIEnums.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ユニットAIが使う列挙型一式
 * =====================================*/

using UnityEngine;

namespace PPCore
{
    // ユニット 1 体がその思考で最終的に選んだ行動
    public enum PPUnitAIDecision
    {
        // 何もしない
        [InspectorName("待機")]
        Wait,
        // スキルを発動する
        [InspectorName("スキル")]
        Skill,
        // 通常攻撃を行う
        [InspectorName("通常攻撃")]
        NormalAttack,
    }

    // ユニットがその思考で行動しなかった理由
    // 判断ツリーのどこで止まったかを追うために使う
    public enum PPUnitAIRejectReason
    {
        // 行動を採用した（棄却されていない）
        None,
        // AI プロファイルが未設定
        NoProfile,
        // このティックの行動回数を使い切っている
        NoActionBudget,
        // ツリーのどの枝も成立しなかった（末尾に無条件の行動を置けば埋められる）
        NoMatchedNode,
        // ツリーが待機を選んだ（溜めるための意図的な待機）
        DecidedToWait,
    }
}
