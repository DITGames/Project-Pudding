/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPPartyConditions.cs
 * @author hqrse
 * @date 2026/07/21
 * @brief パーティ条件（パーティ状態・進行）
 * =====================================*/

using System;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // パーティ状況条件: パーティ全体の HP 割合
    // 個々の残 HP ではなく合計 HP に対する割合で見るため、
    // 「全体的に消耗している」状況の判定に向く。閾値は 0～1 で保持する
    [Serializable]
    [PPTypeMenuName("パーティ状態/HP割合")]
    public sealed class PPPartyHpRatioCondition : PPPartyConditionValidator
    {
        [Label("比較")] public PPCompareOp Op = PPCompareOp.Equal;
        [PercentLabel("割合")] public float Threshold = 0f;
        // 等値判定の許容誤差（0〜1）。等値・非等値のときのみ表示される
        [Label("許容値")][EditCondition("IsEqualOp", true, false)] public float Tolerance = 0f;

        // 許容値の入力欄を出すかどうか（等値系の演算子でのみ意味を持つ）
        bool IsEqualOp
        => Op == PPCompareOp.Equal
        || Op == PPCompareOp.NotEqual;

        // パーティ全体の HP 割合を閾値と比較する
        // 双方とも 0〜1 の尺度のためそのまま渡す
        // aSnapShot : 評価対象のパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPPartyAIContext aSnapShot)
        => PPConditionMath.Compare(aSnapShot.PartyHpRatio, Op, Threshold, Tolerance);

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
        [PercentLabel("HP割合")] public float HpThreshold = 0.5f;
        [Label("HP比較")] public PPCompareOp HpOp = PPCompareOp.GreaterOrEqual;
        [Label("許容値")][EditCondition(nameof(IsEqualOp), true, false)] public float Torelance = 0f;
        [Label("ユニット数")] public int UnitCount = 1;
        [Label("ユニット比較")] public PPCompareOp UnitOp = PPCompareOp.GreaterOrEqual;
        
        private bool IsEqualOp
            => HpOp == PPCompareOp.Equal || HpOp == PPCompareOp.NotEqual;

        // パーティの生存メンバー各ユニットに対して指定条件を満たしているか
        // aSnapShot : 評価対象のパーティ状況スナップショット
        // return : 条件を満たす場合 true
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

    // パーティ状況条件: 生存ユニット数
    // 「味方が 1 体になったら総攻撃」「敵が残り 1 体なら回復より攻撃」といった
    // 頭数に基づく戦術の切り替えに使う。人数は整数のため厳密に比較する
    [Serializable]
    [PPTypeMenuName("パーティ状態/ユニット生存数")]
    public sealed class PPAliveUnitCountCondition : PPPartyConditionValidator
    {
        [Label("対象")] public PPAliveUnitCountConditionType ConditionType = PPAliveUnitCountConditionType.Ally;
        [Label("比較")] public PPCompareOp Op = PPCompareOp.GreaterOrEqual;
        [Label("ユニット数")] public int Threshold = 2;

        // 対象の生存数を閾値と比較する
        // スナップショットが保持する生存リストの件数を使うため、思考中に数が変わることはない
        // aSnapShot : 評価対象のパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPPartyAIContext aSnapShot)
        => ConditionType switch
        {
            PPAliveUnitCountConditionType.Ally => PPConditionMath.Compare(aSnapShot.AliveMembers.Count, Op, Threshold),
            PPAliveUnitCountConditionType.Enemy => PPConditionMath.Compare(aSnapShot.AliveEnemies.Count, Op, Threshold),
            PPAliveUnitCountConditionType.All => PPConditionMath.Compare(aSnapShot.AliveMembers.Count + aSnapShot.AliveEnemies.Count, Op, Threshold),
            _ => false
        };

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            var tgt = GetTargetString();
            var op = GetOpString(Op);
            var num = Threshold + "体";
            mDescription = tgt + num + op;
        }

        // 対象種別を説明文用の日本語へ変換する
        private string GetTargetString()
            => ConditionType switch
            {
                PPAliveUnitCountConditionType.Ally => "味方の生存ユニット数が",
                PPAliveUnitCountConditionType.Enemy => "敵の生存ユニット数が",
                PPAliveUnitCountConditionType.All => "全体の生存ユニット数が",
                _ => ""
            };

        // 説明文の語尾を調整する
        // 「〇体」で終わる文なので、等値のときは語尾を付けない方が自然になる
        // aOp : 比較演算子
        protected override string GetOpString(PPCompareOp aOp)
            => aOp switch
            {
                PPCompareOp.Equal => "",
                PPCompareOp.NotEqual => "ではない",
                _ => base.GetOpString(aOp),
            };
    }

    // パーティ状況条件: 指定の状態異常・バフを持つ味方が 1 体でもいるか
    // 「バフが切れている味方がいるなら支援に回る」のような、盤面全体の状況判断に使う
    //
    // 判定のみを行い、対象の絞り込みは行わない
    // 誰を狙うかを決めるのはターゲット検索ノード（PPUnitAISearchNode）の役割で、
    // 条件が候補リストまで書き換えると、判定に使っただけのつもりが対象を変えていた、という事故が起きる
    [Serializable]
    [PPTypeMenuName("パーティ状態/状態異常を所有してるユニットがいる")]
    public class PPFindHasStatusEffectUnit : PPPartyConditionValidator
    {
        // 判定用エフェクト。未設定なら特定のエフェクトでは絞り込まない
        [Label("エフェクト")]
        [SerializeField] private PPEffectDefinition mEffect;

        // 判定用タグ。None なら タグでは絞り込まない
        [Label("エフェクトタグ")]
        [SerializeField] private StatusEffectTag mTags = StatusEffectTag.None;

        // 反転で付与されていないユニットがいるかの判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 条件に合致する味方が 1 体でもいるかを判定する
        // aSnapShot : 評価対象のパーティ状況スナップショット
        // return : 条件を満たすユニットがいれば true
        public override bool Evaluate(PPPartyAIContext aSnapShot)
        {
            foreach (var unit in aSnapShot.AliveMembers)
            {
                if (HasMatchingEffect(unit) != mIsInvert) return true;
            }
            return false;
        }

        // 条件に合致する状態異常を持っているかを走査する
        // ID とタグはどちらも未指定なら素通しになるため、両方未指定だと付与の有無だけを見る
        // aUnit : 判定対象のユニット
        // return : 合致する状態異常があれば true
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

        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string target = mEffect == null
                ? (mTags == StatusEffectTag.None ? "何らかの状態異常" : $"{mTags} の効果")
                : mEffect.DisplayName;
            mDescription = mIsInvert ? $"{target}が付与されているユニットがいない" : $"{target}が付与されているユニットがいる";
        }
    }

    // パーティ状況条件: バトル開始からの経過ターン数
    // 「序盤は溜めて中盤から攻める」のような、盤面ではなく進行度で切り替わる戦術に使う
    // ターン数は整数のため許容誤差を取らず厳密に比較する
    [Serializable]
    [PPTypeMenuName("進行/経過ターン数")]
    public sealed class PPTurnCountCondition : PPPartyConditionValidator
    {
        [Label("比較")] public PPCompareOp Op = PPCompareOp.Equal;
        [Label("ターン数")] public int Threshold = 3;

        // 経過ターン数を閾値と比較する
        // aSnapShot : 評価対象のパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPPartyAIContext aSnapShot)
         => PPConditionMath.Compare(aSnapShot.Context.TurnCount, Op, Threshold);

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            var prefix = $"経過ターン数が{Threshold}ターン";
            var op = GetOpString(Op);
            mDescription = prefix + op;
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

    // パーティ状況条件: 経過ターン数が指定周期にあたるか
    // 「3 ターンごとに全体攻撃」のような、一定間隔で繰り返す行動を書くためのもの
    //
    // ノード側のクールダウンでも似た形は作れるが、あちらは「前回そのノードで確定してから」を数えるため
    // 実際に撃てた時刻に引きずられて周期がずれていく
    // こちらはバトル開始からの絶対ターン数で判定するので、撃てなかったターンがあっても周期は保たれる
    [Serializable]
    [PPTypeMenuName("進行/経過ターン数の周期")]
    public sealed class PPTurnCycleCondition : PPPartyConditionValidator
    {
        // 何ターンごとに成立させるか。1 以下なら毎ターン成立する
        [Label("周期(ターン)")]
        [SerializeField] private int mCycle = 3;
        // 周期の中のどの位置で成立させるか。0 なら周期の頭で成立する
        [Label("周期内の位置")]
        [SerializeField] private int mOffset = 0;
        // 反転すると「周期にあたらないターン」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 経過ターン数が周期にあたるかを判定する
        // aSnapShot : 評価対象のパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPPartyAIContext aSnapShot)
        {
            if (mCycle <= 1) return !mIsInvert;

            // 位置が周期をはみ出していても意味のある値へ丸めておく
            int offset = ((mOffset % mCycle) + mCycle) % mCycle;
            bool isOnCycle = aSnapShot.Context.TurnCount % mCycle == offset;
            return isOnCycle != mIsInvert;
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string body = mCycle <= 1 ? "毎ターン" : $"{mCycle}ターンごと(位置{mOffset})";
            mDescription = mIsInvert ? $"{body}ではない" : body;
        }
    }
}
