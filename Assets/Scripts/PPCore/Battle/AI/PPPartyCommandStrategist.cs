/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPPartyCommandStrategist.cs
 * @author hqrse
 * @date 2026/07/16
 * @brief パーティの AI（思考ルーチン）の契約
 * =====================================*/

namespace PPCore
{
    // パーティのユニットに代わって次の行動を決める AI のインターフェース。実装は PPUnitAIStrategist
    // 決めた内容はその場で実行せず、ユニットの「選択中のスキル」と「狙う相手」として予約する
    public interface IPPPartyCommandStrategist
    {
        // バトル進行の管理役と担当する陣営を受け取る
        void BindBattle(BattleManager aManager, BattleSide aSide);

        // バトル進行の管理役との繋がりを断つ
        // 購読を残すと、バトル終了後も古い思考ルーチンがイベントを拾い続ける
        void Unbind();

        // ユニット 1 体の次の行動を決めて予約する。そのユニットの行動が終わるたびに呼ぶ
        void Think(PPBattleUnit aUnit, BattleContext aContext);

        // 担当する陣営の全ユニットについて、次の行動を決めて予約する。バトル開始時や担当を始めたときに呼ぶ
        void ThinkAll(BattleContext aContext);
    }
}
