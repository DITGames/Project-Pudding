/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPCoinDropAIDefaultsDefinition.cs
 * @author hqrse
 * @date 2026/11/22
 * @brief コイン投入 AI の型 × 段階ごとの既定のプロファイルと、プレイヤーの既定
 * =====================================*/

using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // 型(バランス・コイン重視・アイテム重視)ごとに段階 1〜5 のプロファイルを持つ。PPRuntimeSettings から参照する
    // 段階が上がるほど、投入が速く、最良の列を選ぶ確率が高く、考え直すのが早い
    [CreateAssetMenu(fileName = "PPCoinDropAIDefaults", menuName = "Project-Pudding/AI/PPCoinDropAIDefaultsDefinition")]
    public class PPCoinDropAIDefaultsDefinition : ScriptableObject
    {
        /* 定数 */

        private const int LevelCount = PPCoinDropStyleUtility.LevelMax - PPCoinDropStyleUtility.LevelMin + 1;

        /* インスペクタ設定 */

        // 段階 1〜5 の順に並べる
        [Header("型ごとの段階")]
        [Label("バランス", true)]
        [SerializeField] private PPCoinDropAIProfile[] mBalance = CreateDefaults(PPCoinDropStyle.Balance);
        [Label("コイン重視", true)]
        [SerializeField] private PPCoinDropAIProfile[] mCoin = CreateDefaults(PPCoinDropStyle.Coin);
        [Label("アイテム重視", true)]
        [SerializeField] private PPCoinDropAIProfile[] mItem = CreateDefaults(PPCoinDropStyle.Item);

        // オートバトル中の味方の投入。型はバトル中に切り替えられる
        [Header("プレイヤー")]
        [Label("オートの既定の型")]
        [SerializeField] private PPCoinDropStyle mPlayerStyle = PPCoinDropStyle.Balance;
        [Label("オートの思考の段階")]
        [Range(PPCoinDropStyleUtility.LevelMin, PPCoinDropStyleUtility.LevelMax)]
        [SerializeField] private int mPlayerLevel = 3;

        /* プロパティ */

        public PPCoinDropStyle PlayerStyle => mPlayerStyle;
        public int PlayerLevel => mPlayerLevel;

        /* 問い合わせ */

        // 段階は 1〜5 に収める。表に無ければ null
        public PPCoinDropAIProfile GetProfile(PPCoinDropStyle aStyle, int aLevel)
        {
            var profiles = aStyle switch
            {
                PPCoinDropStyle.Coin => mCoin,
                PPCoinDropStyle.Item => mItem,
                _ => mBalance,
            };
            if (profiles == null || profiles.Length == 0) return null;
            int index = Mathf.Clamp(aLevel - PPCoinDropStyleUtility.LevelMin, 0, profiles.Length - 1);
            return profiles[index];
        }

        /* ヘルパー */

        // 新しく作ったアセットに入れる初期値。段階で速さと正確性を、型で優先度と投入の仕方を変える
        private static PPCoinDropAIProfile[] CreateDefaults(PPCoinDropStyle aStyle)
        {
            float[] intervals = { 2.2f, 1.8f, 1.5f, 1.2f, 0.9f };
            float[] accuracies = { 0.2f, 0.4f, 0.6f, 0.8f, 0.95f };
            float[] rethinks = { 3f, 2.5f, 2f, 1.5f, 1f };
            var profiles = new PPCoinDropAIProfile[LevelCount];
            for (int i = 0; i < LevelCount; i++)
            {
                var profile = new PPCoinDropAIProfile
                {
                    DropInterval = intervals[i],
                    Accuracy = accuracies[i],
                    RethinkInterval = rethinks[i],
                };
                switch (aStyle)
                {
                    // コインの多い列へ数で押すため、投入を速くする
                    case PPCoinDropStyle.Coin:
                        profile.CoinPriority = 1.5f;
                        profile.ItemPriority = 0.3f;
                        profile.DropInterval *= 0.85f;
                        break;
                    // 動くアイテムを追うため、考え直しを早くする
                    case PPCoinDropStyle.Item:
                        profile.CoinPriority = 0.4f;
                        profile.ItemPriority = 3f;
                        profile.RethinkInterval *= 0.7f;
                        break;
                    default:
                        profile.CoinPriority = 1f;
                        profile.ItemPriority = 1f;
                        break;
                }
                profiles[i] = profile;
            }
            return profiles;
        }
    }
}
