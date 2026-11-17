/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPPartyConditions.cs
 * @author hqrse
 * @date 2026/07/21
 * @brief パーティ条件（パーティ状態・進行）
 * =====================================*/

using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;
using AttributeUtility;

namespace PPCore
{
    // パーティ状況条件: パーティ全体の HP 割合（合計 HP に対する割合。閾値は 0～1）
    // 全体的に消耗している状況の判定に向く
    [Serializable]
    [PPTypeMenuName("パーティ状態/HP割合")]
    public sealed class PPPartyHpRatioCondition : PPPartyConditionValidator
    {
        /* インスペクタ設定 */

        [Label("比較")] public PPCompareOp Op = PPCompareOp.Equal;
        [PercentLabel("割合")] public float Threshold = 0f;
        // 等値判定の許容誤差（0〜1）。等値・非等値のときのみ表示される
        [Label("許容値")][EditCondition("IsEqualOp", true, false)] public float Tolerance = 0f;

        /* プロパティ */

        // 許容値の入力欄を出すかどうか（等値系の演算子でのみ意味を持つ）
        bool IsEqualOp
        => Op == PPCompareOp.Equal
        || Op == PPCompareOp.NotEqual;

        /* 評価・判定 */

        // パーティ全体の HP 割合を閾値と比較する
        // 双方とも 0〜1 の尺度のためそのまま渡す
        public override bool Evaluate(PPPartyAIContext aSnapShot)
        => PPConditionMath.Compare(aSnapShot.PartyHpRatio, Op, Threshold, Tolerance);

        /* 説明文 */

        // 設定内容から説明文を組み立てる。等値系のときは許容値も併記する
        // 値は 0〜1 で保持しているが、説明文は読みやすさを優先してパーセント表記にする
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            var prefix = "HPが";
            var ratio = $"{Threshold * 100f:0.#}%";
            var op = GetOpString(Op);
            mDescription = prefix + ratio + op;

            if (Op == PPCompareOp.Equal || Op == PPCompareOp.NotEqual)
            {
                mDescription += $" 許容値({Tolerance * 100f:0.#}%)";
            }
        }

        // 説明文の語尾を自然な日本語にするため、等値系のみ表記を差し替える
        // aOp : 比較演算子
        protected override string GetOpString(PPCompareOp aOp)
            => aOp switch
            {
                PPCompareOp.Equal => "と等しい",
                PPCompareOp.NotEqual => "と等しくない",
                _ => base.GetOpString(aOp)
            };
    }

    [Serializable]
    [PPTypeMenuName("パーティ状態/HP割合を満たすユニットが〇体")]
    public sealed class PPAliveMemberHealthRatio : PPPartyConditionValidator
    {
        /* インスペクタ設定 */

        [PercentLabel("HP割合")] public float HpThreshold = 0.5f;
        [Label("HP比較")] public PPCompareOp HpOp = PPCompareOp.GreaterOrEqual;
        [Label("許容値")][EditCondition(nameof(IsEqualOp), true, false)] public float Torelance = 0f;
        [Label("ユニット数")] public int UnitCount = 1;
        [Label("ユニット比較")] public PPCompareOp UnitOp = PPCompareOp.GreaterOrEqual;

        /* プロパティ */

        private bool IsEqualOp
            => HpOp == PPCompareOp.Equal || HpOp == PPCompareOp.NotEqual;

        /* 評価・判定 */

        // パーティの生存メンバー各ユニットに対して指定条件を満たしているか
        public override bool Evaluate(PPPartyAIContext aSnapShot)
        {
            int count = 0;
            // HP条件を満たしているユニット数をカウント
            foreach (var unit in aSnapShot.AliveMembers)
            {
                if (PPConditionMath.Compare(unit.Parameters.Hp.Ratio, HpOp, HpThreshold, Torelance))
                {
                    count++;
                }
            }
            
            return PPConditionMath.Compare(count, UnitOp, UnitCount, 0);
        }

        /* 説明文 */

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            var prefix = "HP割合が" + $"{HpThreshold * 100f:0.#}%";
            var op = GetOpString(HpOp);
            var unit = $"ユニットが{UnitCount}体" + GetOpString(UnitOp);
            mDescription = $"{prefix} {op} {unit}";
            if (IsEqualOp)
            {
                mDescription += $" HP許容値({Torelance * 100f:0.#}%)";
            }
        }

        // 説明文の語尾を自然な日本語にするため、等値系のみ表記を差し替える
        // aOp : 比較演算子
        protected override string GetOpString(PPCompareOp aOp)
            => aOp switch
            {
                PPCompareOp.Equal => "と等しい",
                PPCompareOp.NotEqual => "と等しくない",
                _ => base.GetOpString(aOp)
            };
    }

    // 生存数を数える対象
    public enum PPAliveUnitCountConditionType
    {
        // 味方の生存数
        [InspectorName("味方")]
        Ally,
        // 敵の生存数
        [InspectorName("敵")]
        Enemy,
        // 敵味方を合わせた生存数
        [InspectorName("全体")]
        All,
    }

    // パーティ状況条件: 生存ユニット数。頭数に基づく戦術の切り替えに使う
    // 人数は整数のため厳密に比較する
    [Serializable]
    [PPTypeMenuName("パーティ状態/ユニット生存数")]
    public sealed class PPAliveUnitCountCondition : PPPartyConditionValidator
    {
        /* インスペクタ設定 */

        [Label("対象")] public PPAliveUnitCountConditionType ConditionType = PPAliveUnitCountConditionType.Ally;
        [Label("比較")] public PPCompareOp Op = PPCompareOp.GreaterOrEqual;
        [Label("ユニット数")] public int Threshold = 2;

        /* 評価・判定 */

        // 対象の生存数を閾値と比較する
        // スナップショットが保持する生存リストの件数を使うため、思考中に数が変わることはない
        public override bool Evaluate(PPPartyAIContext aSnapShot)
        => ConditionType switch
        {
            PPAliveUnitCountConditionType.Ally => PPConditionMath.Compare(aSnapShot.AliveMembers.Count, Op, Threshold),
            PPAliveUnitCountConditionType.Enemy => PPConditionMath.Compare(aSnapShot.AliveEnemies.Count, Op, Threshold),
            PPAliveUnitCountConditionType.All => PPConditionMath.Compare(aSnapShot.AliveMembers.Count + aSnapShot.AliveEnemies.Count, Op, Threshold),
            _ => false
        };

        /* 説明文 */

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            var tgt = GetTargetString();
            var op = GetOpString(Op);
            var num = Threshold + "体";
            mDescription = tgt + num + op;
        }

        // 説明文の語尾を調整する
        // 「〇体」で終わる文なので、等値のときは語尾を付けない方が自然になる
        protected override string GetOpString(PPCompareOp aOp)
            => aOp switch
            {
                PPCompareOp.Equal => "",
                PPCompareOp.NotEqual => "ではない",
                _ => base.GetOpString(aOp),
            };

        // 対象種別を説明文用の日本語へ変換する
        private string GetTargetString()
            => ConditionType switch
            {
                PPAliveUnitCountConditionType.Ally => "味方の生存ユニット数が",
                PPAliveUnitCountConditionType.Enemy => "敵の生存ユニット数が",
                PPAliveUnitCountConditionType.All => "全体の生存ユニット数が",
                _ => ""
            };
    }

    // パーティ状況条件: 指定の状態異常・バフを持つ味方が 1 体でもいるか
    // 判定のみを行い、対象の絞り込みはしない（誰を狙うかは PPUnitAISearchNode が決める）
    [Serializable]
    [PPTypeMenuName("パーティ状態/状態異常を所有してるユニットがいる")]
    public class PPFindHasStatusEffectUnit : PPPartyConditionValidator
    {
        /* インスペクタ設定 */

        // 判定用エフェクト。未設定なら特定のエフェクトでは絞り込まない
        [Label("エフェクト")]
        [SerializeField] private PPEffectDefinition mEffect;
        // 判定用タグ。None なら タグでは絞り込まない
        [Label("エフェクトタグ")]
        [SerializeField] private StatusEffectTag mTags = StatusEffectTag.None;
        // 反転で付与されていないユニットがいるかの判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        /* 評価・判定 */

        // 条件に合致する味方が 1 体でもいるかを判定する
        public override bool Evaluate(PPPartyAIContext aSnapShot)
        {
            foreach (var unit in aSnapShot.AliveMembers)
            {
                if (HasMatchingEffect(unit) != mIsInvert) return true;
            }
            return false;
        }

        /* 問い合わせ */

        // 条件に合致する状態異常を持っているかを走査する
        // ID とタグはどちらも未指定なら素通しになるため、両方未指定だと付与の有無だけを見る
        private bool HasMatchingEffect(PPBattleUnit aUnit)
        {
            foreach (var eff in aUnit.ActiveStatusEffects)
            {
                if(eff == null) continue;
                if (mEffect != null && eff.EffectId != mEffect.EffectId) continue;
                if(mTags != StatusEffectTag.None && (eff.Tags & mTags) == 0) continue;

                return true;
            }
            return false;
        }

        /* 説明文 */

        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string target = mEffect == null
                ? (mTags == StatusEffectTag.None ? "何らかの状態異常" : $"{mTags} の効果")
                : mEffect.DisplayName;
            mDescription = mIsInvert ? $"{target}が付与されているユニットがいない" : $"{target}が付与されているユニットがいる";
        }
    }

    // パーティ状況条件: バトル開始からの経過時間（秒）。進行度で切り替わる戦術に使う
    // バトル専用の時計で測るため、コマンド入力中の時間は含まない
    [Serializable]
    [PPTypeMenuName("進行/経過時間")]
    [MovedFrom(false, null, null, "PPTurnCountCondition")]
    public sealed class PPElapsedTimeCondition : PPPartyConditionValidator
    {
        /* インスペクタ設定 */

        [Label("比較")] public PPCompareOp Op = PPCompareOp.GreaterOrEqual;
        [Label("経過時間(秒)")] public float Threshold = 15f;

        /* 評価・判定 */

        // 経過時間を閾値と比較する
        public override bool Evaluate(PPPartyAIContext aSnapShot)
         => PPConditionMath.Compare(aSnapShot.Context.Clock.Now, Op, Threshold);

        /* 説明文 */

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
            => mDescription = $"経過時間が{Threshold}秒{GetOpString(Op)}";
    }

    // パーティ状況条件: 経過時間が指定周期の中の指定範囲にあたるか（「15 秒ごとに 5 秒間だけ全体攻撃を狙う」など）
    // 開始からの絶対時間で判定するため、撃てなかった周期があっても周期がずれない
    [Serializable]
    [PPTypeMenuName("進行/経過時間の周期")]
    [MovedFrom(false, null, null, "PPTurnCycleCondition")]
    public sealed class PPElapsedTimeCycleCondition : PPPartyConditionValidator
    {
        /* インスペクタ設定 */

        // 何秒ごとに成立させるか。0 以下なら常に成立する
        [Label("周期(秒)")]
        [FormerlySerializedAs("mCycle")]
        [SerializeField] private float mCycleSeconds = 15f;
        // 周期の頭から何秒間成立させるか。周期以上なら常に成立する
        [Label("成立させる長さ(秒)")]
        [SerializeField] private float mWindowSeconds = 5f;
        // 反転すると「範囲にあたらない時間」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        /* 評価・判定 */

        // 経過時間が周期の中の範囲にあたるかを判定する
        public override bool Evaluate(PPPartyAIContext aSnapShot)
        {
            if (mCycleSeconds <= 0f || mWindowSeconds >= mCycleSeconds) return !mIsInvert;

            bool isOnCycle = aSnapShot.Context.Clock.Now % mCycleSeconds < mWindowSeconds;
            return isOnCycle != mIsInvert;
        }

        /* 説明文 */

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string body = mCycleSeconds <= 0f ? "常に" : $"{mCycleSeconds}秒ごとに{mWindowSeconds}秒間";
            mDescription = mIsInvert ? $"{body}ではない" : body;
        }
    }
}