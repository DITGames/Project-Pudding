/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPDebugBattleLauncher.cs
 * @author hqrse
 * @date 2026/11/24
 * @brief デバッグバトルの設定と、Editor から S_Battle への受け渡し
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace PPCore
{
    // デバッグバトルの味方 1 体分。ユニット定義は GUID で持つ
    [Serializable]
    public class PPDebugBattleMember
    {
        public string UnitGuid;
        public int Level = 1;
    }

    // デバッグバトルの設定。味方の並び(最大 PPPartyFactory.MaxMembers 体)、読み込むバトル定義、深さで割り振るレベルを持つ
    // アセットを指す値は GUID で持ち、JSON にしてこの PC だけに保存する(Git に入れず、競合させない)
    [Serializable]
    public class PPDebugBattleSettings
    {
        /* 状態 */

        public List<PPDebugBattleMember> Allies = new();
        public string BattleGuid;
        public int LevelPoints;

        /* 操作 */

        // 範囲外の値を直し、上限より後ろの味方を外す。古い保存内容を読んだときの備え
        public void Normalize()
        {
            Allies ??= new List<PPDebugBattleMember>();
            Allies.RemoveAll(m => m == null);
            if (Allies.Count > PPPartyFactory.MaxMembers) Allies.RemoveRange(PPPartyFactory.MaxMembers, Allies.Count - PPPartyFactory.MaxMembers);
            foreach (var member in Allies) member.Level = Mathf.Max(1, member.Level);
            LevelPoints = Mathf.Max(0, LevelPoints);
        }
    }

    // デバッグバトルで戦う内容。設定のアセットを読み込んだもの
    public class PPDebugBattleRequest
    {
        /* 状態 */

        private readonly PPPartyMemberEntry[] mAllyMembers;

        /* プロパティ */

        public PPBattleDefinition Battle { get; }
        public int LevelPoints { get; }

        /* 生成 */

        public PPDebugBattleRequest(PPPartyMemberEntry[] aAllyMembers, PPBattleDefinition aBattle, int aLevelPoints)
        {
            mAllyMembers = aAllyMembers;
            Battle = aBattle;
            LevelPoints = aLevelPoints;
        }

        /* 操作 */

        public PPBattleParty CreateAllyParty() => PPPartyFactory.CreateFromMembers(mAllyMembers, BattleSide.Ally);
    }

    // Editor のデバッグバトルウィンドウから S_Battle へ、戦う内容を渡す
    // Play に入るときに static が消えないよう、設定の JSON を SessionState に置く
    public static class PPDebugBattleLauncher
    {
        /* 定数 */

        public const string RequestKey = "PPCore.DebugBattle.RequestedSettings";

        /* 操作 */

        // デバッグバトルとして始めるよう頼まれていれば、その内容を返して頼みを消す。頼まれていなければ null
        // プレイヤービルドでは常に null
        public static PPDebugBattleRequest TakeRequested()
        {
#if UNITY_EDITOR
            string json = UnityEditor.SessionState.GetString(RequestKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return null;
            UnityEditor.SessionState.EraseString(RequestKey);

            var settings = JsonUtility.FromJson<PPDebugBattleSettings>(json);
            settings.Normalize();
            var members = new PPPartyMemberEntry[settings.Allies.Count];
            for (int i = 0; i < members.Length; i++)
            {
                members[i] = new PPPartyMemberEntry { Unit = LoadByGuid<PPUnitDefinition>(settings.Allies[i].UnitGuid), Level = settings.Allies[i].Level };
            }
            return new PPDebugBattleRequest(members, LoadByGuid<PPBattleDefinition>(settings.BattleGuid), settings.LevelPoints);
#else
            return null;
#endif
        }

        /* ヘルパー */

#if UNITY_EDITOR
        private static T LoadByGuid<T>(string aGuid) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(aGuid)) return null;
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(aGuid));
            return asset == null ? null : asset;
        }
#endif
    }
}
