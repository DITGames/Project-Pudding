/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
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
        // 使えるツリーが無い（個別・ロール既定・全体既定のどれも未設定）
        NoTree,
        // このティックの行動回数を使い切っている
        NoActionBudget,
        // ツリーのどの枝も成立しなかった（末尾に無条件の行動を置けば埋められる）
        NoMatchedNode,
        // ツリーが待機を選んだ（溜めるための意図的な待機）
        DecidedToWait,
    }

    // ユニットが使ったツリーの出どころ
    public enum PPUnitAITreeSource
    {
        // 使えるツリーが無い
        None,
        // ユニット定義に設定された個別ツリー
        Individual,
        // ロールの既定ツリー
        RoleDefault,
        // 全体既定ツリー
        GlobalDefault,
    }

    // ツリーの出どころを表示用の日本語へ変換するヘルパー。ログ・デバッグ表示・作成ウィンドウで表記を揃える
    public static class PPUnitAITreeSourceUtility
    {
        // ツリーの出どころを日本語表記へ変換する
        // aSource : ツリーの出どころ
        // return : 日本語の表記
        public static string ToDisplayString(PPUnitAITreeSource aSource)
            => aSource switch
            {
                PPUnitAITreeSource.Individual => "個別",
                PPUnitAITreeSource.RoleDefault => "ロール既定",
                PPUnitAITreeSource.GlobalDefault => "全体既定",
                _ => "なし",
            };
    }
}
