/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPDebugBattleLauncher.cs
 * @author hqrse
 * @date 2026/11/24
 * @brief デバッグバトルの設定と、Editor から S_Battle への受け渡し
 * =====================================*/

using System;
using UnityEngine;

namespace PPCore
{
    // デバッグバトルの設定。味方の 3 枠とレベル、読み込むステージ定義、深さで割り振るレベルを持つ
    // アセットを指す値は GUID で持ち、JSON にしてこの PC だけに保存する(Git に入れず、競合させない)
    [Serializable]
    public class PPDebugBattleSettings
    {
        /* 定数 */

        // 中央 → 右 → 左 の順に並べる
        public static readonly PPPartyPosition[] Positions = { PPPartyPosition.Center, PPPartyPosition.Right, PPPartyPosition.Left };

        /* 状態 */

        public string[] AllyUnitGuids = new string[3];
        public int[] AllyLevels = { 1, 1, 1 };
        public string StageGuid;
        public int LevelPoints;

        /* static 関数 */

        // 枠の添字(Positions の並び)。見つからなければ 0
        public static int IndexOf(PPPartyPosition aPosition) => Mathf.Max(0, Array.IndexOf(Positions, aPosition));

        /* 操作 */

        // 足りない要素を埋める。古い保存内容を読んだときの備え
        public void Normalize()
        {
            if (AllyUnitGuids == null || AllyUnitGuids.Length != 3) Array.Resize(ref AllyUnitGuids, 3);
            if (AllyLevels == null || AllyLevels.Length != 3) AllyLevels = new[] { 1, 1, 1 };
            for (int i = 0; i < 3; i++) AllyLevels[i] = Mathf.Max(1, AllyLevels[i]);
            LevelPoints = Mathf.Max(0, LevelPoints);
        }
    }

    // デバッグバトルで戦う内容。設定のアセットを読み込んだもの
    public class PPDebugBattleRequest
    {
        /* 状態 */

        private readonly PPPartyMemberEntry[] mAllyMembers;

        /* プロパティ */

        public PPEncounterDefinition Stage { get; }
        public int LevelPoints { get; }

        /* 生成 */

        public PPDebugBattleRequest(PPPartyMemberEntry[] aAllyMembers, PPEncounterDefinition aStage, int aLevelPoints)
        {
            mAllyMembers = aAllyMembers;
            Stage = aStage;
            LevelPoints = aLevelPoints;
        }

        /* 操作 */

        public PPBattleParty CreateAllyParty() => PPPartyFactory.CreateFromMembers(p => mAllyMembers[PPDebugBattleSettings.IndexOf(p)], BattleSide.Ally);
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
            var members = new PPPartyMemberEntry[3];
            for (int i = 0; i < 3; i++)
            {
                members[i] = new PPPartyMemberEntry { Unit = LoadByGuid<PPUnitDefinition>(settings.AllyUnitGuids[i]), Level = settings.AllyLevels[i] };
            }
            return new PPDebugBattleRequest(members, LoadByGuid<PPEncounterDefinition>(settings.StageGuid), settings.LevelPoints);
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
