/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPCoinDropAI.cs
 * @author hqrse
 * @date 2026/11/22
 * @brief コイン投入 AI（型・段階・プロファイル・床の区画の数え上げ・投入する列の選び方）
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using AttributeUtility;
using CustomConsole;

namespace PPCore
{
    // 投入 AI の型。何を重視して投入する列を選ぶか
    public enum PPCoinDropStyle
    {
        [InspectorName("バランス")]
        Balance = 0,
        [InspectorName("コイン重視")]
        Coin = 1,
        [InspectorName("アイテム重視")]
        Item = 2,
    }

    // 投入 AI の型の表示名と、切り替えの順番
    public static class PPCoinDropStyleUtility
    {
        /* 定数 */

        public const int LevelMin = 1;
        public const int LevelMax = 5;

        /* 問い合わせ */

        public static string GetName(PPCoinDropStyle aStyle) => aStyle switch
        {
            PPCoinDropStyle.Coin => "コイン重視",
            PPCoinDropStyle.Item => "アイテム重視",
            _ => "バランス",
        };

        // 切り替えキーで進める次の型。バランス → コイン重視 → アイテム重視 → バランス の順
        public static PPCoinDropStyle Next(PPCoinDropStyle aStyle) => aStyle switch
        {
            PPCoinDropStyle.Balance => PPCoinDropStyle.Coin,
            PPCoinDropStyle.Coin => PPCoinDropStyle.Item,
            _ => PPCoinDropStyle.Balance,
        };
    }

    // 投入 AI の考え方の数値。型と段階の組ごとに 1 つ持つ
    [Serializable]
    public sealed class PPCoinDropAIProfile
    {
        // 区画のコイン 1 枚あたりの点数
        [Label("コイン優先度")] [Min(0f)] public float CoinPriority = 1f;
        // 区画のチャージ用アイテム 1 個あたりの点数
        [Label("アイテム優先度")] [Min(0f)] public float ItemPriority = 1f;
        // 1 回の投入で、考えて選んだ最良の列へ入れる確率(0〜1)。外れたときはランダムな列へ入れる
        [Label("思考の正確性")] [Range(0f, 1f)] public float Accuracy = 0.6f;
        [Label("投入の間隔(秒)")] [Min(0.05f)] public float DropInterval = 1.5f;
        [Label("考え直す間隔(秒)")] [Min(0.05f)] public float RethinkInterval = 2f;

        public PPCoinDropAIProfile Clone() => (PPCoinDropAIProfile)MemberwiseClone();
    }

    // パーティ定義などに持たせる、投入 AI の型と段階。カスタムのプロファイルを指定するとそちらを使う
    [Serializable]
    public sealed class PPCoinDropAISetting
    {
        [Label("型")] public PPCoinDropStyle Style = PPCoinDropStyle.Balance;
        // 1 が最も弱く 5 が最も強い
        [Label("段階")] [Range(PPCoinDropStyleUtility.LevelMin, PPCoinDropStyleUtility.LevelMax)] public int Level = 3;
        // 指定すると型と段階を使わず、このプロファイルで投入する
        [Label("カスタムのプロファイル")] public PPCoinDropAIProfileDefinition CustomProfile;

        // 使うプロファイルを返す。既定値の定義が無ければ null
        public PPCoinDropAIProfile Resolve(PPCoinDropAIDefaultsDefinition aDefaults)
        {
            if (CustomProfile != null) return CustomProfile.Profile;
            return aDefaults != null ? aDefaults.GetProfile(Style, Level) : null;
        }

        public string Describe() => CustomProfile != null ? $"カスタム({CustomProfile.name})" : $"{PPCoinDropStyleUtility.GetName(Style)} Lv{Level}";
    }

    // 床を列 × 行の区画に分け、区画ごとのコインの枚数とチャージ用アイテムの数を数える。数えるのは考えるときだけ
    // 区画は数える範囲の Transform の向きと大きさで決め、ローカルの -Z 側(落下口の側)を手前の行 0 とする
    public sealed class PPPusherFloorSampler
    {
        /* 定数 */

        private const string CoinTag = "Coin";
        // 床に落ち着いたコインと積み重なったコインも拾うため、数える範囲の上下へ足す高さ
        private const float ExtraHeight = 3f;

        /* 状態 */

        private readonly Collider[] mBuffer = new Collider[2048];
        private readonly HashSet<Rigidbody> mCounted = new();

        /* プロパティ */

        public int Columns { get; private set; }
        public int Rows { get; private set; }
        // 添字は 行 × 列数 + 列。行 0 が手前
        public int[] Coins { get; private set; } = Array.Empty<int>();
        public int[] Items { get; private set; } = Array.Empty<int>();

        /* 操作 */

        public void Sample(Transform aArea, int aColumns, int aRows)
        {
            Columns = Mathf.Max(1, aColumns);
            Rows = Mathf.Max(1, aRows);
            int cells = Columns * Rows;
            if (Coins.Length != cells)
            {
                Coins = new int[cells];
                Items = new int[cells];
            }
            Array.Clear(Coins, 0, cells);
            Array.Clear(Items, 0, cells);
            mCounted.Clear();
            if (aArea == null) return;

            var halfExtents = aArea.lossyScale * 0.5f;
            halfExtents.y += ExtraHeight;
            int count = Physics.OverlapBoxNonAlloc(aArea.position, halfExtents, mBuffer, aArea.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var body = mBuffer[i].attachedRigidbody;
                if (body == null || !mCounted.Add(body)) continue;

                bool isCoin = body.CompareTag(CoinTag);
                bool isItem = !isCoin && body.TryGetComponent<PPPusherChargeItem>(out _);
                if (!isCoin && !isItem) continue;

                int cell = GetCell(aArea, body.position);
                if (cell < 0) continue;
                if (isCoin) Coins[cell]++;
                else Items[cell]++;
            }
        }

        // 範囲の外なら -1
        public int GetCell(Transform aArea, Vector3 aWorldPosition)
        {
            var local = aArea.InverseTransformPoint(aWorldPosition);
            if (local.x < -0.5f || local.x > 0.5f || local.z < -0.5f || local.z > 0.5f) return -1;

            int column = Mathf.Clamp(Mathf.FloorToInt((local.x + 0.5f) * Columns), 0, Columns - 1);
            int row = Mathf.Clamp(Mathf.FloorToInt((local.z + 0.5f) * Rows), 0, Rows - 1);
            return row * Columns + column;
        }
    }

    // 床の区画の数え上げから列ごとの点数を出し、投入する列を決めてディスペンサーへ投入させる
    // 時間は timeScale に従う。乱数はバトルの乱数供給元を使う
    public sealed class PPCoinDropAI
    {
        /* 定数 */

        private const string LogTag = "Pusher";

        /* 状態 */

        private readonly PPCoinDropAIProfile mProfile;
        private readonly IRandomProvider mRandom;
        // ログに出す持ち主の名前（味方・敵など）
        private readonly string mOwner;
        private float mDropTimer;
        // 0 以下になったら考え直す
        private float mThinkTimer;
        // 最後に考えたときの最良の列。まだ考えていなければ -1
        private int mBestColumn = -1;
        private float[] mScores = Array.Empty<float>();

        /* プロパティ */

        public PPCoinDropAIProfile Profile => mProfile;
        public IReadOnlyList<float> Scores => mScores;
        public int BestColumn => mBestColumn;

        /* 生成 */

        public PPCoinDropAI(PPCoinDropAIProfile aProfile, IRandomProvider aRandom, string aOwner)
        {
            mProfile = aProfile ?? new PPCoinDropAIProfile();
            mRandom = aRandom ?? new DefaultRandomProvider();
            mOwner = aOwner;
            mDropTimer = mProfile.DropInterval;
        }

        /* 操作 */

        // 考え直す間隔ごとに列の点数を出し直し、投入の間隔ごとに 1 枚投入する
        public void Tick(PPCoinDispenser aDispenser, float aDeltaTime)
        {
            mThinkTimer -= aDeltaTime;
            if (mThinkTimer <= 0f || mBestColumn < 0)
            {
                Think(aDispenser);
                mThinkTimer = mProfile.RethinkInterval;
            }

            mDropTimer -= aDeltaTime;
            if (mDropTimer > 0f) return;
            mDropTimer = mProfile.DropInterval;

            int columns = aDispenser.FloorSampler.Columns;
            // 正確性の確率で最良の列へ、外れたら全列からランダムに選ぶ。正確性 0 なら毎回ランダム
            int column = mRandom.NextBool(mProfile.Accuracy) ? mBestColumn : mRandom.NextInt(columns);
            aDispenser.TryDropAt(aDispenser.ColumnToRatio(column, mRandom.NextFloat()));
        }

        // 区画を数え、手前の行ほど重くして列ごとの点数にまとめ、最良の列を決める
        private void Think(PPCoinDispenser aDispenser)
        {
            var sampler = aDispenser.SampleFloor();
            int columns = sampler.Columns;
            if (mScores.Length != columns) mScores = new float[columns];

            mBestColumn = 0;
            for (int column = 0; column < columns; column++)
            {
                float score = 0f;
                for (int row = 0; row < sampler.Rows; row++)
                {
                    int cell = row * columns + column;
                    float cellScore = sampler.Coins[cell] * mProfile.CoinPriority + sampler.Items[cell] * mProfile.ItemPriority;
                    score += cellScore * aDispenser.GetRowWeight(row);
                }
                mScores[column] = score;
                if (score > mScores[mBestColumn]) mBestColumn = column;
            }
            CustomConsoleLog.Verbose(LogTag, Describe(sampler));
        }

        /* 説明文 */

        private string Describe(PPPusherFloorSampler aSampler)
        {
            var text = new StringBuilder();
            text.Append($"[{mOwner}] 列の点数=");
            for (int i = 0; i < mScores.Length; i++) text.Append(i == 0 ? "" : " / ").Append(mScores[i].ToString("0.0"));
            text.Append($" 最良の列={mBestColumn} 正確性={mProfile.Accuracy:0.00} 区画(手前の行から 列ごとのコイン:アイテム)=");
            for (int row = 0; row < aSampler.Rows; row++)
            {
                text.Append(row == 0 ? "" : " | ");
                for (int column = 0; column < aSampler.Columns; column++)
                {
                    int cell = row * aSampler.Columns + column;
                    text.Append(column == 0 ? "" : " ").Append(aSampler.Coins[cell]).Append(':').Append(aSampler.Items[cell]);
                }
            }
            return text.ToString();
        }
    }
}
