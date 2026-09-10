/* =====================================
 * Copyright DITGames. All rights reserved.
 * @file PPBattleUnitSelectMenuView.cs
 * @author DITGames
 * @date 2026/09/07
 * @brief 自陣ユニット選択メニュー（入力モード中のみ表示する専用ボタン列）
 * =====================================*/

using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using AttributeUtility;

namespace PPCore
{
    // 「誰が行動するか」を選ばせる専用メニュー
    // 盤面表示（PPBattleUnitView）とは独立しており、入力モードに入ったときだけ生成・表示し、
    // ユニットが決まったら閉じる。盤面側は表示専任のままにできるため、
    // 将来盤面をワールド空間（HD2D）表示へ差し替えても本メニューは影響を受けない
    public class PPBattleUnitSelectMenuView : MonoBehaviour
    {
        [Label("ユニットボタンプレハブ")]
        [SerializeField] private PPBattleCommandButton mButtonPrefab;
        [Label("ユニットボタン表示領域")]
        [SerializeField] private RectTransform mContent;
        [Label("ビジュアルカタログ")]
        [SerializeField] private PPUnitVisualCatalog mIconCatalog;

        // ユニットが選択されたときに発火する
        public event Action<BattleUnit> OnUnitSelected;

        // 生成済みのユニットボタン。閉じるときにまとめて破棄する
        private readonly List<PPBattleCommandButton> mUnitButtons = new();

        // 候補ユニット分のボタンを生成して表示する
        // aUnits : 選択候補のユニット
        // aDirection : 入場演出の向き。Down なら上から、Up なら下から現れる
        public void Show(IEnumerable<BattleUnit> aUnits, PPBattleTransitionDirection aDirection)
        {
            if (mContent == null)
            {
                Debug.LogWarning("mContent is null");
                return;
            }

            Clear(aDirection);
            gameObject.SetActive(true);

            PPBattleCommandButton firstBtn = null;
            foreach (var unit in aUnits)
            {
                var btn = Instantiate(mButtonPrefab, mContent);
                // 複製直後は全ボタンが同名になり階層上で見分けがつかないため、対象ユニットが分かる名前を付ける
                btn.gameObject.name = $"UnitButton_{unit.UnitId}";
                // カタログ未設定、または該当アイコン未登録のどちらもアイコンなしとして扱う
                var icon = mIconCatalog != null
                    ? mIconCatalog.Resolve(unit.UnitId)?.UnitIcon
                    : null;
                btn.Setup(icon, unit.DisplayName, () => OnUnitSelected?.Invoke(unit));
                mUnitButtons.Add(btn);

                // 初期フォーカス設定
                firstBtn ??= btn;
            }

            // 直後にフォーカスを当てるため、レイアウトの反映を次フレームまで待たない
            LayoutRebuilder.ForceRebuildLayoutImmediate(mContent);

            // レイアウト確定後の定位置を基準に、入場演出を開始する
            foreach (var btn in mUnitButtons)
            {
                btn.PlayEnter(aDirection);
            }

            if (firstBtn != null)
            {
                EventSystem.current.SetSelectedGameObject(firstBtn.FocusTarget);
            }
        }

        // メニューを閉じ、生成したボタンを破棄する
        // aDirection : 退場演出の向き。Down なら下へ、Up なら上へ抜ける
        public void Hide(PPBattleTransitionDirection aDirection)
        {
            Clear(aDirection);
        }

        // 生成済みのユニットボタンをすべて退場演出付きで破棄する
        // 表示のたびに作り直すため、開く前と閉じるときの両方から呼ばれる
        // ルートを即座に非アクティブにすると子ボタンの退場コルーチンが Unity 側で強制停止し、
        // フェードアウトが再生されないまま破棄もされず残ってしまうため、非アクティブ化はしない
        // （退場後は子が居なくなるだけで、見た目にも入力にも影響しない）
        // aDirection : 退場演出の向き
        private void Clear(PPBattleTransitionDirection aDirection)
        {
            foreach (var btn in mUnitButtons)
            {
                if (btn == null) continue;
                // mContent（ContentSizeFitter付き）に残したままだと、全ボタン退場でコンテナが
                // 収縮した瞬間に基準位置がずれてワープして見えるため、自身のルートへ退避させてから消す
                btn.PlayExit(aDirection, transform, () => Destroy(btn.gameObject));
            }
            mUnitButtons.Clear();
        }
    }
}
