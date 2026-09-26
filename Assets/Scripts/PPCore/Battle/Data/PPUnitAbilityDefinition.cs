/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitAbilityDefinition.cs
 * @author hqrse
 * @date 2026/09/26
 * @brief ユニットの基礎能力の日本語表示名の定数群
 * =====================================*/

namespace PPCore
{
    // ユニットの基礎能力（育成・成長の対象になる 5 能力）の日本語表示名を集約した定数群
    // ユニット定義のインスペクタとユニット作成ウィンドウから参照する
    // 対応するパラメータは HP → MaxHP、ちから → Attack、まもり → Defense、はやさ → Speed、きようさ → Dexterity
    // バトル中のパラメータ名（攻撃力など）やバフ・デバフの対象名（PPParameterEffectCategoryDefinition）とは別物として扱う
    public static class PPUnitAbilityDefinition
    {
        // 最大 HP の元になる能力
        public const string NameHp = "HP";
        // 攻撃力の元になる能力
        public const string NameStrength = "ちから";
        // 防御力の元になる能力
        public const string NameGuard = "まもり";
        // 素早さの元になる能力
        public const string NameAgility = "はやさ";
        // 会心率の元になる能力
        public const string NameDexterity = "きようさ";

        // 成長曲線のラベルに付ける接尾辞
        public const string GrowthCurveSuffix = "成長曲線";
    }
}
