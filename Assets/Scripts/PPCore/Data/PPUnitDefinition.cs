/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitDefinition.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief ユニット定義
 * =====================================*/

using System;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // ユニット定義。属性・追加ステータス・レベル成長曲線・難易度の基本ポイント・コインとバトル画面での性質を持つ
    // 成長は AnimationCurve の倍率で表現し、成長後の値は四捨五入で整数に丸める
    [CreateAssetMenu(fileName = "PPBattleUnitDefinition", menuName = "Project-Pudding/Definition/PPUnitDefinition")]
    public class PPUnitDefinition : UnitDefinition
    {
        /* インスペクタ設定 */

        [Header("ユニット拡張")]
        // 構造体はフィールド初期化子を持てないため、新規アセット用の既定値（きようさ）はここで与える
        // 既存アセットはシリアライズ済みの値が優先される
        [Label("ステータス")]
        [SerializeField]protected PPStatBlock mExpandStatBlock = new()
        {
            Dexterity = PPStatBlock.DefaultDexterity,
        };

        // ユニットの属性。弱点・耐性の判定に使う
        [Label("属性")]
        [SerializeField]protected PPTypeAttribute mTypeAttribute = PPTypeAttribute.Normal;

        // 遭遇の難易度ポイントの元になる値。遭遇は編成のユニットのこの値を合計する
        [Header("レベルデザイン")]
        [Label("難易度の基本ポイント")] [Min(0)]
        [SerializeField]protected int mDifficultyPoint = 10;

        // 編成の上限に数えるコスト。低いほどコインに出やすい
        [Header("コインとバトル画面")]
        [Label("ユニットコスト")] [Min(1)]
        [SerializeField]protected int mUnitCost = 10;
        // 重いほどコインが大きく重くなり、押しづらく落としづらい
        [Label("重さ")] [Min(0f)]
        [SerializeField]protected float mWeight = 1f;
        // バトル画面の道を 1 秒に進む距離
        [Label("移動速度")] [Min(0f)]
        [SerializeField]protected float mMoveSpeed = 3f;

        // 距離はバトル画面の道の単位、時間は秒
        [Header("戦闘")]
        [Label("射程")] [Min(0.1f)]
        [SerializeField]protected float mAttackRange = 2f;
        // 射程に敵が入ってから 1 回目を当てるまでの時間
        [Label("攻撃の溜め")] [Min(0f)]
        [SerializeField]protected float mAttackWindup = 0.3f;
        // 2 回目以降の攻撃の間隔
        [Label("攻撃間隔")] [Min(0.05f)]
        [SerializeField]protected float mAttackInterval = 1.5f;
        // 範囲攻撃なら、射程内で最も近い敵から奥へ範囲の広さまでにいる敵すべてに当てる。単体なら最も近い敵だけ
        [Label("範囲攻撃")]
        [SerializeField]protected bool mIsAreaAttack;
        [Label("範囲の広さ")] [Min(0f)]
        [EditCondition(nameof(mIsAreaAttack), true, false)]
        [SerializeField]protected float mAreaWidth = 3f;
        // HP を回数で等分した区切りを切るたびと、撃破時に押し戻される(3 回なら 2/3・1/3 と撃破時)
        [Label("ノックバックの回数")] [Min(1)]
        [SerializeField]protected int mKnockbackCount = 1;
        [Label("攻撃でノックバックさせる")]
        [SerializeField]protected bool mHasAttackKnockback;
        // 攻撃が当たった相手を押し戻す確率(0〜1)
        [Label("ノックバックの確率")] [Min(0f)]
        [EditCondition(nameof(mHasAttackKnockback), true, false)]
        [SerializeField]protected float mAttackKnockbackChance = 0.3f;

        [Header("成長曲線 (X = レベル, Y = 倍率)")]
        [Label(PPUnitAbilityDefinition.NameHp + PPUnitAbilityDefinition.GrowthCurveSuffix)][SerializeField]protected AnimationCurve mHpGrowth = AnimationCurve.Linear(1, 1, 50, 3);
        [Label(PPUnitAbilityDefinition.NameStrength + PPUnitAbilityDefinition.GrowthCurveSuffix)][SerializeField]protected AnimationCurve mAttackGrowth = AnimationCurve.Linear(1, 1, 50, 3);
        [Label(PPUnitAbilityDefinition.NameGuard + PPUnitAbilityDefinition.GrowthCurveSuffix)][SerializeField]protected AnimationCurve mDefenseGrowth = AnimationCurve.Linear(1, 1, 50, 3);
        [Label(PPUnitAbilityDefinition.NameAgility + PPUnitAbilityDefinition.GrowthCurveSuffix)][SerializeField]protected AnimationCurve mSpeedGrowth = AnimationCurve.Linear(1, 1, 50, 3);
        [Label(PPUnitAbilityDefinition.NameDexterity + PPUnitAbilityDefinition.GrowthCurveSuffix)][SerializeField]protected AnimationCurve mDexterityGrowth = AnimationCurve.Linear(1, 1, 50, 3);

        /* プロパティ */

        public PPStatBlock ExpandStatBlock => mExpandStatBlock;
        public PPTypeAttribute TypeAttribute => mTypeAttribute;
        public int DifficultyPoint => mDifficultyPoint;
        public int UnitCost => mUnitCost;
        public float Weight => mWeight;
        public float MoveSpeed => mMoveSpeed;
        public float AttackRange => mAttackRange;
        public float AttackWindup => mAttackWindup;
        public float AttackInterval => mAttackInterval;
        public bool IsAreaAttack => mIsAreaAttack;
        public float AreaWidth => mAreaWidth;
        public int KnockbackCount => mKnockbackCount;
        public bool HasAttackKnockback => mHasAttackKnockback;
        public float AttackKnockbackChance => Mathf.Clamp01(mAttackKnockbackChance);

        /* static 関数 */

        // 育成値を整数へ丸める。0.5 は 0 から遠い側へ丸める（2.5 → 3、-2.5 → -3）
        // Mathf.Round は偶数丸め（2.5 → 2）になるため使わない
        public static float RoundGrowthValue(float aValue)
            => (float)Math.Round((double)aValue, MidpointRounding.AwayFromZero);

        // 基礎値へ成長倍率を掛けて育成値を求める。表示と生成で共通の計算式
        // 倍率は最低 1 に丸め、曲線が未設定の場合は倍率 1 として扱う
        public static float EvaluateGrowthValue(float aBase, AnimationCurve aCurve, int aLevel)
        {
            float rate = aCurve == null ? 1f : Mathf.Max(1f, aCurve.Evaluate(aLevel));
            return RoundGrowthValue(aBase * rate);
        }

        /* 生成 */

        // レベル 1 でランタイムユニットを生成する。基底のシグネチャに合わせた入口
        public override BattleUnit CreateRuntimeUnit()
            => CreateRuntimeUnit(1);

        // レベルを指定してランタイムユニットを生成する。成長後のパラメータと追加パラメータを組み立てる
        public virtual BattleUnit CreateRuntimeUnit(int aLevel)
        {
            return new PPBattleUnit(mUnitId, DisplayName, CreateParameterSet(aLevel), CreatePPParameterSet(aLevel), mTypeAttribute)
            {
                SourceDefinition = this,
                Level = aLevel,
            };
        }

        // 基礎ステータスへ成長倍率を掛けてパラメータ一式を組み立てる
        // 曲線の設定ミスでレベルアップにより弱くなるのを避けるため、倍率は最低 1 に丸める
        protected virtual ParameterSet CreateParameterSet(int aLevel)
        {
            var b = EvaluateStats(aLevel);
            return new ParameterSet(
                b.MaxHP, b.Attack, b.Defense, b.Speed
                );
        }

        // 追加パラメータ一式を組み立てる
        protected virtual PPParameterSet CreatePPParameterSet(int aLevel)
            => new(EvaluateDexterity(aLevel));

        /* 評価・判定 */

        // ランタイムユニットを生成せず、表示と生成で同じ成長値（育成値）を評価する
        // きようさは基底の StatBlock に含まれないため EvaluateDexterity で別に評価する
        public virtual StatBlock EvaluateStats(int aLevel) => new()
        {
            MaxHP = EvaluateGrowthValue(mBaseStatBlock.MaxHP, mHpGrowth, aLevel),
            Attack = EvaluateGrowthValue(mBaseStatBlock.Attack, mAttackGrowth, aLevel),
            Defense = EvaluateGrowthValue(mBaseStatBlock.Defense, mDefenseGrowth, aLevel),
            Speed = EvaluateGrowthValue(mBaseStatBlock.Speed, mSpeedGrowth, aLevel),
        };

        // きようさの育成値を評価する。他の 4 能力と同じ丸め規則を使う
        public virtual float EvaluateDexterity(int aLevel)
            => EvaluateGrowthValue(mExpandStatBlock.Dexterity, mDexterityGrowth, aLevel);

        // 指定したレベルでの難易度ポイント。今はレベルによる補正をせず、基本ポイントをそのまま返す
        public virtual int EvaluateDifficultyPoint(int aLevel) => mDifficultyPoint;
    }
}
