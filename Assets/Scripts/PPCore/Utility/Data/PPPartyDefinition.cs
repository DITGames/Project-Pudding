/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyDefinition.cs
 * @author hqrse
 * @date 2026/07/23
 * @brief パーティ定義
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;
using CommandBattleCore;

namespace PPCore
{
    // パーティメンバー 1 体分の設定
    // ユニット定義の既定値をそのまま使い、編成上の違いはレベルで表現する
    [Serializable]
    public sealed class PPPartyMemberEntry
    {
        [Label("ユニット定義")] public PPUnitDefinition Unit;
        // 生成するレベル。成長曲線の評価に使う
        [Label("レベル")] public int Level = 1;
    }

    // パーティ編成の定義（ScriptableObject）
    // メンバー構成・リソース設定・使用する AI プロファイルをまとめて持つ
    // ランタイムの PPBattleParty への変換は PPPartyFactory が担う
    // 敵の遭遇パターンを 1 アセット = 1 戦闘の単位で用意できる
    [CreateAssetMenu(fileName = "PPPartyDefinition", menuName = "Project-Pudding/Battle/PPPartyDefinition")]
    public class PPPartyDefinition : ScriptableObject
    {
        [Header("パーティ")]
        [Label("パーティメンバー", true)] public List<PPPartyMemberEntry> Members = new();
        [Label("リソース上限")] public int MaxResource = 100;
        [Label("リソース変換レート初期値")] public float BaseResourceConversionRate = 1f;

        // このパーティが使う戦術リスト。未設定の場合そのパーティは常に待機する
        [Header("AI")]
        [Label("AIプロファイル")] public PPPartyAIProfileDefinition AIProfile;
    }
}
