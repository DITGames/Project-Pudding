/* =====================================
 * Copyright DITGames. All rights reserved.
 * @file BattleCommandInput.cs
 * @author DITGames
 * @date 2026/08/31
 * @brief 入力システム試作。自陣ユニットの取得と、モンスターボタンの生成を確認する
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;
using CustomConsole;
using PPCore;
using UnityEngine;
using UnityEngine.InputSystem;
using AttributeUtility;

namespace BattleInput
{
    // 十字キー入力でユニット選択 → コマンド発行を行う、新しい入力システムの試作
    // PPBattleCommandInputController とは独立した別系統として、BattleManager を直接 Bind して読みに行く
    // 現段階では、自陣（Ally）のユニット取得と、対応するモンスターボタンの生成・アイコン反映までを確認する
    public class BattleCommandInput : MonoBehaviour
    {
        // モンスターボタンの親。あらかじめ位置マーカー（空のRectTransform）をユニット順
        // （0:左 1:中央 2:右 …）で子に並べておく。ボタン自身もここへ生成される
        [Label("モンスターボタン配置先(AllyButtonRow)")]
        [SerializeField] private RectTransform mAllyButtonRow;
        // 自陣ユニット1体につき1つ生成するボタンのプレハブ
        [Label("モンスターボタンプレハブ")]
        [SerializeField] private BattleUnitButtonElement mUnitButtonPrefab;
        // ユニットID からアイコンを解決するためのカタログ
        [Label("ビジュアルカタログ")]
        [SerializeField] private PPUnitVisualCatalog mUnitVisualCatalog;
        // Vキーで開閉する際に表示する背景の半透明パネル。既に用意済みのものを参照するだけ
        [Label("背景パネル")]
        [SerializeField] private GameObject mBackgroundPanel;

        // コマンドの投入先・状態の読み取り元。Bind で外部（Runner側）から受け取る
        private BattleManager mManager;
        // 現在パネル（背景＋モンスターボタン）を開いているか
        private bool mIsOpen;

        // mAllyButtonRow の子から最初に読み取った位置マーカー
        // ボタン自身も同じ親へ生成されるため、生成後の childCount で読み直すとマーカーと区別できなくなる
        // 初回だけ記録して使い回すことで、この問題を避けている
        private Transform[] mSpawnPointCache;
        // 前回 SpawnAllyButtons() で生成したボタン。再生成時にこれだけを片付け、位置マーカーは残す
        private readonly List<BattleUnitButtonElement> mSpawnedButtons = new();

        // バインドされているバトルマネージャ
        public BattleManager Manager => mManager;

        // バトルマネージャを紐づけ、バトル開始直後の自陣ユニットでモンスターボタンを生成する
        // aManager : 参照するバトルマネージャ
        public void Bind(BattleManager aManager)
        {
            mManager = aManager;
            SpawnAllyButtons();

            // 開始直後は閉じた状態にしておく（timeScaleはこの時点で1のはずだが念のため明示する）
            mIsOpen = false;
            ApplyOpenState();
        }

        // Vキーの押下を監視し、パネルの開閉をトグルする
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
            {
                ToggleOpen();
            }
        }

        // パネル（背景＋モンスターボタン）の開閉をトグルする
        // 開くと背景パネルとモンスターボタンを表示して timeScale を止め、閉じると両方隠して戻す
        public void ToggleOpen()
        {
            mIsOpen = !mIsOpen;
            ApplyOpenState();
        }

        // 現在の開閉状態を、背景パネル・モンスターボタン・timeScale へ反映する
        private void ApplyOpenState()
        {
            if (mBackgroundPanel != null) mBackgroundPanel.SetActive(mIsOpen);
            if (mAllyButtonRow != null) mAllyButtonRow.gameObject.SetActive(mIsOpen);
            Time.timeScale = mIsOpen ? 0f : 1f;
        }

        // 自陣（Ally）の生存アクティブメンバーを取得する
        // PPUnitSelectState と同じ取得元（Context.GetParty(Ally).GetAliveActiveMembers()）をなぞっている
        // return : 味方パーティの生存アクティブユニット一覧
        public List<BattleUnit> GetAllyUnits()
        {
            if (mManager == null || mManager.Context == null)
                return new List<BattleUnit>();

            var result = new List<BattleUnit>();
            foreach (var unit in mManager.Context.GetParty(BattleSide.Ally).GetAliveActiveMembers())
            {
                result.Add(unit);
            }
            return result;
        }

        // 自陣ユニット分だけモンスターボタンを並べ、それぞれのアイコンを設定する
        // 配置先・プレハブのいずれかが未設定なら何もしない
        // ビジュアルカタログが未設定の場合はアイコンなしでボタンだけ生成する
        public void SpawnAllyButtons()
        {
            if (mAllyButtonRow == null || mUnitButtonPrefab == null) return;

            // 位置マーカーはボタンを1つも生成していない状態でだけ正しく読み取れるため、初回のみキャプチャする
            mSpawnPointCache ??= CaptureSpawnPoints();

            // 前回生成したボタンだけを片付ける（位置マーカーは mAllyButtonRow の子のまま残す）
            foreach (var button in mSpawnedButtons)
            {
                if (button != null) Destroy(button.gameObject);
            }
            mSpawnedButtons.Clear();

            var units = GetAllyUnits();
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                var element = Instantiate(mUnitButtonPrefab, mAllyButtonRow);
                var visual = mUnitVisualCatalog != null ? mUnitVisualCatalog.Resolve(unit.UnitId) : null;
                element.SetUnit(unit, visual != null ? visual.UnitIcon : null);

                PlaceAt((RectTransform)element.transform, i);
                element.OnClicked += HandleButtonClicked;
                mSpawnedButtons.Add(element);
            }
        }

        // モンスターボタンがクリックされたときの処理
        // 現段階では確認のためログを出すだけ。ユニット選択への反映は今後ここへ足していく
        // aElement : クリックされたボタン
        private void HandleButtonClicked(BattleUnitButtonElement aElement)
        {
            CustomConsoleLog.Log("UI", $"モンスターボタンがクリックされました: {aElement.BattleUnit?.DisplayName}");
        }

        // mAllyButtonRow の子を、ボタン生成前の時点で位置マーカーとして記録する
        // return : 記録した位置マーカーの配列（子の並び順）
        private Transform[] CaptureSpawnPoints()
        {
            var points = new Transform[mAllyButtonRow.childCount];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = mAllyButtonRow.GetChild(i);
            }
            return points;
        }

        // インデックスに対応する位置マーカーへ座標を合わせる
        // マーカーは同じ親の子だが階層が異なる可能性もあるため、アンカーではなくワールド座標を直接コピーする
        // 対応するマーカーが無ければ警告を出すだけにして、生成直後の初期位置のまま残す
        // aRect : 配置するボタンの RectTransform
        // aIndex : 何番目に生成したか（0始まり）
        private void PlaceAt(RectTransform aRect, int aIndex)
        {
            if (mSpawnPointCache == null || aIndex >= mSpawnPointCache.Length || mSpawnPointCache[aIndex] == null)
            {
                CustomConsoleLog.Warning("UI", $"{aIndex}番目のモンスターボタンの生成位置マーカーが見つかりません。");
                return;
            }
            aRect.position = mSpawnPointCache[aIndex].position;
        }

        // 動作確認用。取得できた自陣ユニットの名前をコンソールへ出力する
        [ContextMenu("自陣ユニットを取得してログ出力")]
        private void DebugLogAllyUnits()
        {
            var units = GetAllyUnits();
            CustomConsoleLog.Log("UI", $"自陣ユニット取得: {units.Count}体");
            foreach (var unit in units)
            {
                CustomConsoleLog.Log("UI", $"- {unit.DisplayName}");
            }
        }
    }
}
