# StatusEffect の SO 一元化

## 引継ぎサマリ（2026/09/02 時点）

- **現在の状態**: 調査・方針立案のみ完了。**実装コードの変更は一切行っていない**（コミット済みなのはこの記録ファイルのみ）。
- **作業ブランチ**: `claude/statuseffect-unification-scope-vyp5h7`（origin にプッシュ済み。PRは未作成）
- **進め方**: `simple-work-flow` スキルに従い判定した結果、本タスクはスケール **L**（影響ファイル6件以上・新しいデータ構造の導入）。L はフェーズ3（合意）が必須ゲートで、ユーザー承認を待ってから実装フェーズへ進む運用。
- **ユーザーからの回答（設計判断3点、対応方針は下記「要件」節に反映済み）**:
  1. 効果値は全スキル共通でSO側に一元化。ただし「毒」「猛毒」のような同系統バリエーションは別アセットとして許容し、両者を同じ状態として識別できる分類軸が必要 → 既存の `PPEffectCategory` で代替可能と判断（新規enumは追加しない）。
  2. AI条件はSOへの直接参照でよい。
  3. 既存2アセットの移行は低コストなので手動でよい。
- **直近の実装方針は下の「実装方針」節を参照。ユーザーへ提示済みだが、以下2点は未回答のまま保留中**:
  1. `mEffectId`（新設予定の手入力IDフィールド）の命名規則は、既存の `mSkillId` に倣い英語表記（例: `"AttackBuff"`）でよいか。
  2. `PSBT_BuffDefense.asset` など、`PSBT_BuffAttack.asset` 以外にも同様の状態異常AI条件アセットがあれば実装フェーズで洗い出して併せて修正する、という進め方でよいか。
- **次にやること（実装フェーズ再開時の手順）**:
  1. 上記の未回答2点をユーザーに確認（または合理的な既定値で進めてよいか再確認）。
  2. 「実装方針」節の1〜5を順に実施。
  3. `PPUnitStatusEffectCondition` / `PPFindHasStatusEffectUnit` を使っている他のAI条件アセットを `grep` で洗い出し（`PSBT_BuffAttack.asset` 以外にも存在する可能性、未調査）、併せて参照方式に付け替える。
  4. Unity Editor 上でのコンパイル確認・該当バトルサンプル（`BattleSample.unity` 等）での動作確認（自動テストは存在しないプロジェクトのため手動確認が必須）。
  5. フェーズ6セルフレビュー後、コミット・プッシュ。

## 要件（完了条件）

1. `PPEffectDefinition`（毒・パラメータ変動）を ScriptableObject 化し、プロジェクトに「毒」「猛毒」のように1エフェクト=1アセットとして存在させる。値はアセット側に一元化し、スキル側は参照するだけで独自の値を持たない。
   - ただし同系統の別バリエーション（例: 毒 / 猛毒）は別アセットとして共存してよい。両者を「毒状態」として共通に識別できる分類軸が必要。
   - → 調査の結果、既存の `PPEffectCategory`（`PPCore/Battle/StatusEffect/PPEffectCategory.cs`）が bitmask ベースの「ゲーム固有の細分類」として既にこの役割を持っている（`PPPoisonEffectDefinition.Category => PPEffectCategory.Poison` はクラス単位で固定なので、同じクラスの別アセット（毒／猛毒）は自動的に同一 Category になる）。新しい分類軸（`PPStatusEffectType` 等）は追加せず、既存の `PPEffectCategory` を流用する。
2. AI条件（`PPUnitStatusEffectCondition` / `PPFindHasStatusEffectUnit`）は文字列IDの手入力ではなく、`PPEffectDefinition` アセットへの直接参照に変更する。
3. 既存の埋め込みデータ（`PSD_AttackBuff.asset`, `PSD_HasteSelf.asset` の2件）は手動でアセット化して移行する。

## 完了条件（観測可能な形）

- `PPEffectDefinition` およびその派生（`PPPoisonEffectDefinition`, `PPParameterEffectDefinition`）が `ScriptableObject` になり、`CreateAssetMenu` からアセットとして作成できる。
- `PPStatusApplySkillEffectDefinition.mEffect` が通常の Object 参照フィールドになり、既存アセットを選ぶ／ドラッグ＆ドロップで設定できる。
- `PSD_AttackBuff.asset` / `PSD_HasteSelf.asset` が、埋め込みデータではなく新規作成した `PPParameterEffectDefinition` アセットを参照する状態になる。
- `PPUnitStatusEffectCondition` / `PPFindHasStatusEffectUnit` が `mEffectId`（string）ではなく `PPEffectDefinition` への参照フィールドを持ち、`PSBT_BuffAttack.asset`（および同様の `PSBT_BuffDefense.asset` 等）がこの参照で判定するよう更新される。
- Editor 上でコンパイルが通り、既存の該当シーン/バトルサンプルで攻撃バフ・加速スキルが従来通り動作する。

## 非目標

- 麻痺・火傷など、まだ実データの無い状態異常の実装追加は行わない（アーキテクチャとしてクラスを増やせば対応可能な状態にするに留める）。
- `PPBattleUnitView.AddStatusIcon/RemoveStatusIcon` へのアイコン実装（現状未実装のまま）。
- `PPEffectDefinition.SourceDefinition` を使った新規のAI/UIロジック追加。

## 調査結果（サマリ、詳細はチャット履歴参照）

- `PPEffectDefinition` は現状 `[Serializable]` プレーンクラスで `PPStatusApplySkillEffectDefinition.mEffect` に `[SerializeReference]` インライン埋め込みされている。
- `EffectId` は `BuildAutoEffectId()` が数値の組み合わせから自動生成（例: `Param_Attack_Increase_Multiply_1.5_5`）。SO化後はアセット単位の安定した ID が必要（`SkillDefinition.mSkillId` と同様の手入力フィールド方式を踏襲する）。
- 実データでインライン埋め込みされているのは `PSD_AttackBuff.asset` と `PSD_HasteSelf.asset` の2件のみ（どちらも `PPParameterEffectDefinition`）。`PPPoisonEffectDefinition` はまだどのアセットにも実データが無い。
- `PSBT_BuffAttack.asset` の `PPUnitStatusEffectCondition.mEffectId = "AttackBuf"`（タイポ）と `PPFindHasStatusEffectUnit.mEffectId = "AttackBuff"` は、実際に生成される ID (`Param_Attack_Increase_Multiply_1.5_5`) と一致せず機能していない。SO直接参照化で構造的に解消される。
- `PPSkillEffectDefinitionDrawer` は現在「型選択ツリーで `PPEffectDefinition` 派生の葉を選ぶと `PPStatusApplySkillEffectDefinition` でラップしてインスタンス生成する」という橋渡しロジックを持つ。SO化後はこの橋渡しは不要になり、`PPStatusApplySkillEffectDefinition` 自体に `[PPTypeMenuName]` を付与してツリーから直接選べるようにし、選択後にインスペクタで Object 参照を設定する形に変える。
- `PPEffectDefinitionDrawer`（SerializeReference専用ドロワー）は Object 参照化に伴い不要になり削除する。
- `Assets/GameData/Effect/StatusEffect/` と `Assets/GameData/Effect/ParameterEffect/` フォルダが既に（空で）用意されている。新規アセットの置き場として利用する。
- `StatusEffect.SourceDefinition`（Core層）は現状どこからも `as PPEffectDefinition` で読まれていない。SO参照化してもここへの影響はない。
