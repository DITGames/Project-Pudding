/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file AnimSequenceDefinition.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief 複数の2DアニメーションをアニメーションキーごとのList<AnimSequenceEntry>として保持するデータアセット
 * =====================================*/

using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;

namespace AnimSequencer2D
{
    [CreateAssetMenu(fileName = "AnimSequenceDefinition", menuName = "AnimSequencer2D/AnimSequenceDefinition")]
    public class AnimSequenceDefinition : ScriptableObject
    {
        [Label("アニメーション", true)]
        [SerializeField] private List<AnimSequenceEntry> mEntries = new();

        // 複数のアニメーションキー間で共有される表示オブジェクトの一覧(初期配置画面で管理する)
        [Label("オブジェクト", true)]
        [SerializeField] private List<AnimSequenceObject> mObjects = new();

        public IReadOnlyList<AnimSequenceEntry> Entries => mEntries;
        public IReadOnlyList<AnimSequenceObject> Objects => mObjects;

        // オブジェクトIDからオブジェクトを検索する
        // aObjectId : 検索するオブジェクトID
        // 戻り値 : 見つかったオブジェクト。見つからない場合はnull
        public AnimSequenceObject FindObject(string aObjectId)
        {
            if (string.IsNullOrEmpty(aObjectId))
            {
                return null;
            }
            for (int i = 0; i < mObjects.Count; i++)
            {
                if (mObjects[i].ObjectId == aObjectId)
                {
                    return mObjects[i];
                }
            }
            return null;
        }

        // 存在しないオブジェクトを参照しているトラックを検出する(ウィンドウの警告表示用)
        // 戻り値 : "{アニメーションキー}/{トラックID}"形式の文字列一覧
        public List<string> CollectInvalidObjectReferences()
        {
            var result = new List<string>();
            foreach (AnimSequenceEntry entry in mEntries)
            {
                foreach (AnimSequenceTrack track in entry.Tracks)
                {
                    if (FindObject(track.TrackId) == null)
                    {
                        result.Add($"{entry.Key}/{track.TrackId}");
                    }
                }
            }
            return result;
        }

        // アニメーションキーからエントリを検索する。重複キーがある場合は最初に見つかったものを返す
        // aKey : 検索するアニメーションキー
        // 戻り値 : 見つかったエントリ。見つからない場合はnull
        public AnimSequenceEntry FindEntry(string aKey)
        {
            if (string.IsNullOrEmpty(aKey))
            {
                return null;
            }
            // List<T>.Find(lambda)はaKeyをキャプチャするクロージャを毎回アロケーションするため、
            // Transition遷移のたびに呼ばれる本メソッドではGCを避けて手動ループにする
            for (int i = 0; i < mEntries.Count; i++)
            {
                if (mEntries[i].Key == aKey)
                {
                    return mEntries[i];
                }
            }
            return null;
        }

        // 重複したアニメーションキーが存在するかを判定する(ウィンドウの警告表示用)
        public bool HasDuplicateKeys()
        {
            var seen = new HashSet<string>();
            foreach (AnimSequenceEntry entry in mEntries)
            {
                if (string.IsNullOrEmpty(entry.Key))
                {
                    continue;
                }
                if (!seen.Add(entry.Key))
                {
                    return true;
                }
            }
            return false;
        }

        // 存在しないキーを指す遷移設定を持つエントリのキー一覧を返す(ウィンドウの警告表示用)
        public List<string> CollectInvalidTransitionKeys()
        {
            var result = new List<string>();
            foreach (AnimSequenceEntry entry in mEntries)
            {
                if (entry.EndBehavior != AnimSequenceEndBehavior.Transition)
                {
                    continue;
                }
                if (FindEntry(entry.TransitionTargetKey) == null)
                {
                    result.Add(entry.Key);
                }
            }
            return result;
        }

        // 基準Materialのシェーダに存在しないプロパティ名を指すMaterialパラメータトラックを検出する(ウィンドウの警告表示用)。
        // 基準Materialは参照先オブジェクト側が持つため、まずオブジェクトを解決してから判定する。
        // 判定対象は基準Materialのみとし、Material切り替えで使われる個々のMaterialまでは検証しない(重くなるため)
        // 戻り値 : "{アニメーションキー}/{トラックID}/{プロパティ名}"形式の文字列一覧
        public List<string> CollectInvalidMaterialParameterTracks()
        {
            var result = new List<string>();
            foreach (AnimSequenceEntry entry in mEntries)
            {
                foreach (AnimSequenceTrack track in entry.Tracks)
                {
                    AnimSequenceObject obj = FindObject(track.TrackId);
                    if (obj?.BaseMaterial == null)
                    {
                        continue;
                    }
                    foreach (AnimSequenceMaterialParameterTrack paramTrack in track.MaterialParameterTracks)
                    {
                        if (!obj.BaseMaterial.HasProperty(paramTrack.PropertyName))
                        {
                            result.Add($"{entry.Key}/{track.TrackId}/{paramTrack.PropertyName}");
                        }
                    }
                }
            }
            return result;
        }

        // 評価は時刻昇順を前提とするため、編集後は必ず並べ替えておく
        private void OnValidate()
        {
            foreach (AnimSequenceEntry entry in mEntries)
            {
                entry.SortKeyframes();
            }
        }
    }
}
