# Blackjack

`Assets/Scenes/Scene_Blackjack.unity` を開いて再生する。`BlackjackPresentation` の `BlackjackSceneUI` は初期状態で独立した確認用資金（Preview Mode、1000、BET 10）を使用する。

## 操作とルール

- Hit：1枚追加。Stand：ディーラーへ交代。Double Down：初期2枚のときだけ同額を追加し、1枚引いて終了。
- プレイヤーは全札表向き。ディーラーは初期1枚だけ裏向きで、ターン開始または初期Blackjack判定時に公開。
- Aは11／1を調整。ディーラーは16以下でHit、17以上（Soft 17含む）でStand。判断は `BlackjackDealer` に分離。
- BETは正の偶数のみ。初期2枚のBlackjackは配布直後に決着し、双方BlackjackはPush。普通の21より優先し、ディーラーBlackjack時にDouble Downの追加徴収は発生しない。
- 通常勝利はBETの2倍、Blackjack勝利は2.5倍、PushはBETを返却（いずれも元金込み）。Split、Insurance、Surrenderは対象外。
- 資金不足は `WaitingForFunds` で停止する。PreviewのResume Fundsは不足額を補充する。本番は外部で資金を更新してから同ボタンまたは `ResumeAfterFunding()` を呼ぶ。
- Next Roundで次のラウンド。各ラウンドは共通の52枚山札を作り直してシャッフルする。

## Inspectorと演出

`BlackjackSceneUI` の Initial Bet / Preview Money / Preview Modeで起動条件を変更する。本番資金を使う場合はPreview ModeをOFFにし、既存のGameInstanceを先に起動する。既存PlayerDataを初期化・上書きしない。

`BlackjackPresentationDirector` の Eventsへ `BlackjackPresentationEvent` を登録する。ProjectのCreate → Casino → Blackjack → Presentation Eventで追加できる。Timing、Actor、Action、Result、標準演出の前後、Once Per Round、Camera View、Scenario Labelを設定する。None/Anyは条件を限定しない。

`Assets/MyAssets/Blackjack/BeforePlayerDecision.asset` と `BeforeDoubleDown.asset` は無効状態のひな型。実際の宴ラベルを入力してEvent EnabledをONにする。宴本体は既存のAdvEngineController/追加シーンを使用する。会話終了とモーダル解除までゲームを進めない。

コードからはControllerの `TimingReached`（Action通知）、`Presentation`（Func<BlackjackCue, IEnumerator>、完了待機）、`PresentationFailed` を使用する。ゲーム本体の `PendingCue` とID照合で二重進行を防ぐ。演出例外では停止し、Retryで同じ通知から再開する。完了済みSO、配置済みカード、完了済み配当を重複実行しない。

## 共通部分

`Assets/MyAssets/Script/MyGame/Common` に以下を共通化している。

- `ICasinoWallet` / `PlayerDataCasinoWallet`：所持金接続。
- `CardModelCatalog` / `TableCardPresenter`：Casinoの52枚のモデル、3D配置、Flip、枚数に応じた中央揃え。
- `CardTableCameraRig`：Cinemachine切替、机を見る間の手札退避と復帰。
- `AdvScenarioPlayback` / `PresentationRoutine`：宴の完了待機、中断復帰、演出コルーチンの例外捕捉。

山札・手札UIは従来のCardsフォルダーを共用。ブラックジャックの手札はボタン操作のためドラッグを無効化している。机、背景マテリアル、モデルカタログ、UIカードプレハブも既存アセットを参照し、複製していない。

HighCardDuel側の旧コンポーネントは共通クラスの互換ラッパーとして残し、既存シーンとアセットのGUID・Inspector設定を維持する。Blackjack側のTableCardPresenterはSpread To FitをONにしており、Max Row Widthで多枚数時の横幅を調整できる。視点はNormalCamera/TableCamera、人物配置は空のDealerAnchorを使用する。

コピーした未設定シーンを今後再作成する場合は、Casino → Blackjack → Set Up Copied Sceneから接続できる。設定済みシーンへの実行は何も変更しない。

## 検証

`Tests/Blackjack/Run.ps1` はUnity付属のC#コンパイラとMonoを使う描画非依存テスト。6,000の山札順で配当、初期Blackjack、A、Double Down、資金不足、重複通知、複数回Hit、カード重複を検証する。

`Tests/Blackjack/UnityIntegrationTests.cs` は隔離した検証プロジェクト用（本番Assetsへ配置しない）。本番コードとシーンをコピーし、宴接続のみテストダブルに置き換えて、実際のUI・Casinoモデル・Cinemachine・ADV待機を検証する。実際の宴シナリオ内容の再生は、ラベル設定後に本プロジェクトで確認する。
