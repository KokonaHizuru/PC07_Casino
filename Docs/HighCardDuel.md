# ハイカード・デュエル：ロジックと演出の接続

2026-10-18更新：本編シーンはPreview Mode OFF、基本BET10、5ラウンド終了で専用EDへ遷移する。イベントは指定フォルダ直下からカタログへ自動登録し、Flag Idと終了後フラグ更新に対応。以下の旧プレビュー・手動Events登録の説明より、現在の設定は [StoryFlow.md](StoryFlow.md) を優先する。
2026-10-16：ブラックジャック追加に伴い、資金接続、カードモデル、机上配置、カメラ／手札退避、宴の待機、演出コルーチンの実行を `MyGame/Common` に共通化した。既存のDuelコンポーネントは互換ラッパーとして残し、既存シーンの参照やInspector設定を引き継ぐ。ゲーム固有のルール・SOイベントは分離している。共通部分の一覧は `Blackjack.md` を参照。

スクリプトは `Assets/MyAssets/Script/MyGame/HighCardDuel`、名前空間は `Casino.HighCardDuel`。
ゲームロジック、UI手札、Casinoの3Dカード配置・Flip、Cinemachine視点切替、SOによる宴イベントと演出完了待ちを実装。Scene_HightAndLowから確認用ラウンドを実行できる。

## クラス構成

- `HighCardDuelGame`：Unity描画に依存しない進行、公開情報、支払い、勝敗・精算。
- `HighCardDealer`：公開情報からCheck/Raise、Call/Foldを選ぶ。`IHighCardDealer`の差し替えも可能。
- `HighCardDealerSettings`：有利・同程度・不利のRaise/Fold確率と、有利と判断する点差。ControllerのInspectorから調整でき、次の判断から反映する。現在の数値はバランス未調整の仮値。
- `HighCardDuelController`：共通手札UIのドラッグ結果をゲームへ渡し、登録されたコルーチン演出を待つ。
- `PlayerDataDuelWallet`：PlayerDataの`casinoMoney`とゲームの資金処理を接続。金額は整数longで保持し、Initial Betは正のintで受け取る。
- `DuelPresentationDirector` / `DuelPresentationEvent`：標準演出の前後に、条件に一致するSOイベントを順番に実行する。
- `DuelCameraRig` / `DuelTableCards`：Cinemachineのブレンド完了待ちとCasinoカードの配置・Flipを担当する。
- `DuelSceneUI`：確認用の起動・BETボタン・資金表示。本番データへの接続にも対応する。

## 確定した進行

開始演出 → 双方のInitial Bet → 各3枚を配布（プレイヤー手札は表向き、ディーラー手札は裏向き） → プレイヤーが1枚をドラッグして選択 → プレイヤーとディーラーのカードを場へ公開 → ディーラー先手のBET。自分の手札が見えていても、場へ出す前は公開点数やディーラーの判断情報に含めない。

| ディーラー先手 | プレイヤーの応答 | 続き |
|---|---|---|
| Raise | Call / Fold | Callなら次の公開、Foldなら決着 |
| Check | Check / Raise | Checkなら次の公開 |
| Check後にPlayer Raise | — | Dealer Call / Fold。Re-Raise不可 |

BETは1枚目と2枚目の公開後のみ。3枚目を公開したら合計比較する。A=14、J=11、Q=12、K=13。スートによる順位はない。

プレイヤー勝利では自身の投入分を含む全ポットを受け取り、敗北では受け取らず、Pushでは自身の投入分を返却する。Foldでは投入額が不均等でも実際のポットを勝者へ渡す。例：初期BET10、Player Raise10にDealer FoldならPlayerの受取は30、純利益は10。

## シーンへ接続する手順

1. 既存の手札デバッグ用 `CardHandDebug` を無効化する。本番進行と同時に配布させない。
2. シーンに `HighCardDuelController` を追加し、既存のPlayerHand、DealerHandをInspectorへ設定する。手札単体でもCardTableUIプレハブでもよい。
3. 空UIのRectTransformをPlay Areaへ割り当てるか、`SetPlayArea(area)`で渡す。未設定ならドロップ不可。
4. 下記のように演出を登録し、既存システムが保持する現在のPlayerDataを渡して初期化する。所持金は必要に応じてゲーム開始側で設定する。
5. `StartRound(initialBet)`で開始する。初期資金・掛け金の具体値を共通ロジック内に固定していない。

```csharp
controller.Presentation += PlayPresentation;
controller.StateChanged += RefreshButtons;
controller.Initialize(currentPlayerData);
controller.StartRound(initialBet);
```

`Act(DuelAction.Check/Raise/Call/Fold)`を各ボタンから呼ぶ。ボタンの有効状態は`Game.CanAct(action)`で確認する。演出中、資金待ち、終了後の不正入力はfalseを返す。手札の操作可否はControllerが更新する。

`Scene_HightAndLow`には上記の接続を配置済み。以前の`CardHandDebug`は無効化し、`DuelSceneUI`から確認用ラウンドを起動する。Renderer設定はこの演出実装では変更しない。

## 3D演出シーンの使い方

`Scene_HightAndLow`を再生し、表向きの自分の手札から選んだカードを画面中央の`PlayArea`へドラッグする。ディーラーの手札は裏向きのまま。選択カードがCasinoの3Dモデルとして机上へ移動し、俯瞰視点で公開・比較する。BET時は通常視点へ戻る。Check/Raise/Call/Foldのうち現在有効なボタンを押し、決着後はNext Roundで再開する。

確認用の`DuelSceneUI.Preview Mode`は有効。所持金1000・Initial Bet10はInspectorで変更できる独立したテスト値で、保存データへ反映しない。Resume Fundsは不足分だけをテスト資金に補充する。本番に接続するときはPreview Modeを無効にして既存のGameInstance.playerDataを使うか、この起動UIを外して外部からControllerを初期化する。本番モードのResume Fundsは外部で補充された資金での再開だけを試みる。

`HighCardDuelPresentation`以下で次を調整する。

- `NormalCamera` / `TableCamera`：Cinemachine 3のCinemachineCamera。SceneViewでTransformとLensを調整する。Main CameraのCinemachineBrainが視点をブレンドし、描画カメラ自体は維持するため宴のカメラスタックを引き継げる。
- `CardPositions`：両者の移動開始位置と各3枚の配置位置。カードはスロットの上向き面が表になる。移動時間・Flip時間・持ち上げ量・モデル倍率は`TableCards`で調整する。
- `DealerAnchor`：人物モデル配置用の空オブジェクト。今回モデルは入れない。後から子に配置して位置・向きを調整する。
- `DuelPresentationDirector`：標準演出とADVの接続。EventsへSOを登録する。

俯瞰視点への切り替えでは、プレイヤー手札を下、ディーラー手札を上へ画面外まで退避する。通常視点へ戻すと手札の元の位置へ戻る。`HighCardDuelPresentation`の`DuelCameraRig`で`Hand Slide Duration`（初期値0.35秒）と`Hidden Padding`（画面外の余白、初期値40）を調整できる。手札の移動先はCanvasと手札全体の範囲から算出するため、PlayerHandのPos Yを変更しても対応する。SOからの視点変更にも同じ退避処理が適用される。

3Dカード52枚は`Assets/MyAssets/HighCardDuel/CasinoCardModels.asset`でCasinoの既存プレハブを参照する。実行時にパス検索は行わない。机は既存のCRTableOvalを使用する。

### カメラ背景の色と光

`Main Camera`の`Skybox`コンポーネントに`RedCameraBackground`を設定している。Material欄から選択するか、`Assets/MyAssets/HighCardDuel/RedCameraBackground.mat`を選び、Inspectorで調整する。表示確認はGameビューまたはCamera Previewを使う。

- `Bottom Color` / `Top Color`：下側の暗い赤と上側の赤。
- `Gradient Curve`：上下の色の変わり方。1が均等、大きいほど下側の色が長く残る。
- `Top Light Color`：上端に重ねる照明色。
- `Top Light Strength`：光色の強さ。0で光色を重ねない。
- `Top Light Spread`：上端から光色が広がる範囲。大きいほど下まで広がる。
- `Top Light Falloff`：光色の減衰。大きいほど上端に集中する。

カメラ専用の背景として描画する。通常視点・俯瞰視点でも画面上側に光色が載る。これは背景の色調演出であり、机・カードを照らす光源は既存のDirectional Lightで調整する。

## ADVイベントSOの追加

ProjectのCreate → Casino → High Card Duel → Presentation Eventで作成し、DirectorのEventsへ登録する。一致したイベントを配列の上から順に再生し、会話とカメラ移動の完了までゲームを待機させる。

| 項目 | 設定内容 |
|---|---|
| Event Enabled | イベントの有効・無効 |
| Timing | 選択前、選択後、行動前など。下表を参照 |
| Stage | 標準カード・カメラ演出の前（BeforeVisual）または後（AfterVisual） |
| Actor | Player / Dealer。Noneなら対象者を限定しない |
| Action | Check / Raise / Call / Fold。Anyなら行動を限定しない |
| Phase | 1～3。0なら全フェーズ |
| Result | 結果条件。Noneなら限定しない |
| Once Per Round | 同じSOを1ラウンドにつき1回だけ実行 |
| Camera View | 通常・俯瞰・変更しない。会話の前に切り替える |
| Scenario Label | 宴でインポート済みのシナリオラベル（先頭の`*`は任意）。空欄なら会話なし |

設定例：カード選択前はBeforeCardSelection＋Player、選択後に机へ置いてから会話するならCardSelected＋Player＋AfterVisual、ディーラーのレイズ前はBeforeAction＋Dealer＋Raise。BeforeBetDecisionは判断する前なのでAction条件はAnyを使う。

`Assets/MyAssets/HighCardDuel/BeforeSelection.asset`と`BeforeDealerRaise.asset`を設定例として登録済み。実在するシナリオラベルが未指定のため無効にしてある。ラベルを設定してEvent Enabledを有効にする。本文のADVシナリオは今回作成していない。

宴は既存の`AdvEngineController`を参照し、追加シーンで起動する場合は初回だけ起動を待って取得する。正常終了と入力遮断解除を待ってから操作を再開する。ラベル不備やカメラ参照不備などはゲームを止めてConsoleへ通知する。修正後はRetryで再開でき、完了済みSO・標準演出は重複実行しない。Controllerを一時無効化しても同じADVを重複起動しない。

未構成の同名シーンに接続を作り直す場合のみ、Casino → High Card Duel → Set Up Presentation Sceneを使用できる。配置済みなら重複追加を拒否する。通常は配置済みオブジェクトをInspectorで調整すればよい。

## 演出の接続口

`TimingReached`はActionによる同期通知。非同期の演出完了を待つ場合は`Presentation`へ `Func<DuelCue, IEnumerator>` を登録する。複数登録した処理は登録順に実行し、全処理が終わるまでゲームを止める。

```csharp
/// <summary>各タイミングに応じた3D演出を実行する。</summary>
/// <param name="cue">対象カード、対象者、フェーズなどの演出情報。</param>
private IEnumerator PlayPresentation(DuelCue cue)
{
    switch (cue.Timing)
    {
        case DuelTiming.CardSelected:
            // cue.Actorとcue.Card.Valueを使い、Casinoの該当3Dプレハブを
            // 裏向きで場へ移動する。移動完了までyield returnで待つ。
            break;
        case DuelTiming.CardRevealed:
            // 場に出した3Dカードを表へ返す。Flip完了まで待つ。
            break;
        case DuelTiming.ActionTaken:
            // cue.Actorとcue.ActionでDealer Raise/Call/Foldなどを分岐する。
            break;
        case DuelTiming.ResultDecided:
            // cue.ResultでDealer Win/Lose、Pushの演出を分岐する。
            break;
    }
    yield break;
}
```

Casinoのモデルは `Assets/Casino/LWRP/Prefabs/PlayingCards` にある。カード番号はRank（A=1～K=13）であり、勝敗点数のA=14とは区別する。演出側でスートとRankから参照済みのモデルへ対応付ける。ゲームロジックはモデル名やロード方法を持たない。

| タイミング | 内容 |
|---|---|
| RoundStarted | ラウンド開始。まだ支払い前 |
| InitialBetPaid | 双方の初期BET確定 |
| CardsDealt | 配布内容が確定。プレイヤーのUI手札は表向き、ディーラーは裏向き |
| BeforeCardSelection | プレイヤーの選択を受け付ける前 |
| CardSelected | 選択確定。手札から除外、まだ公開点数に加算しない |
| BeforeCardReveal | 表に返す前。公開点数への加算前 |
| CardRevealed | 公開カードとして記録。公開演出が終わるまでBETへ進まない |
| CardsCompared | 両者の今回のカードを公開済み。点数比較の演出 |
| BetStarted | 公開後のBET開始 |
| BeforeBetDecision | 指定対象者が行動を判断する前 |
| BeforeAction | 行動を選択済み。支払い・BET加算・Fold確定前 |
| ActionTaken | 対象者とCheck/Raise/Call/Foldを通知。必要な支払いは済んでいる |
| BetEnded | 次のカード選択への移行前 |
| FundsRequired | 支払えない必要額、残高、不足額を通知 |
| ResultDecided | 勝敗とGame.Payoutが確定、まだ入金前 |
| Settled | 配当・返金を反映済み。Amountはプレイヤー受取額 |
| RoundEnded | 終了演出。完了後にCompletedへ移る |

演出なしの場合、Controllerはそのタイミングを即完了する。UI表示は配布・選択タイミングで同期し、選択カードはUIから取り除く。場札の保持・表示は演出側が担当する。

Controllerを無効化するとコルーチンを停止し、ゲームは現在の演出IDで待機する。再有効化すると同じ演出を先頭から再実行する。演出の例外でも勝手に進行せず、`PresentationFailed`を通知する。原因を直した後に`RetryPresentation()`で再実行できる。**演出側はcue.Idを使い、再実行時に3Dカードを重複生成しないようにする。** ゲーム内の支払い・精算は演出の再実行では重複しない。

PresentationではIEnumeratorをyieldして待つ。独立にStartCoroutineした処理の例外はこのControllerでは捕捉できない。Action通知だけで開始したTweenは完了待ちにならないので、待機が必要ならPresentation内で完了までyieldする。

## ロジック単体で使用する場合

```csharp
var game = new HighCardDuelGame(wallet, dealerPolicy, seed: 123);
game.CueRaised += cue => RequestPresentation(cue);
game.StartRound(initialBet);
// RequestPresentationから依頼した演出が完了したときだけ呼ぶ。
game.CompletePresentation(cueId);
```

単体ロジックには自動完了がない。必ず各cueを完了する。古いID・二重完了はfalseになり、複数回の精算を防ぐ。Controller使用時はControllerが完了を管理するため、ゲーム側からCompletePresentationを直接呼ばない。

PlayerHand/DealerHandは表示を行う信頼されたコード用の読み取り専用リスト。ディーラー判断にはこれらを渡さず、公開点数・公開枚数・投入額だけを含むDealerDecisionContextを渡す。ディーラーのカード選択も隠れた値に依存しない。

## 資金不足と保存

初期BET、Raise、Callの支払いで資金不足になると、支払いや行動を未成立のまま保留する。FundsRequired演出後にWaitingForFundsへ移る。外部でPlayerData.casinoMoneyへ資金を追加した後、`ResumeAfterFunding()`を呼ぶと保留した支払いから再開する。不足したままならfalseを返す。強制Foldや勝敗確定は行わない。

初期所持金、資金補充を断念する操作、ラウンド途中のセーブ復元は今回の対象外。所持金はPlayerDataに保持するが、進行中の山札・ポット・演出待ちを保存する処理は実装していない。現段階ではセーブをIdleまたはCompletedに限定する。ラウンド途中でControllerを破棄して作り直すと進行は復元されない。

## 検証

`Tests/HighCardDuel/Run.ps1`でロジックの回帰テストを実行できる。Unityの場所が異なる場合は `-UnityDataPath` でEditor/Dataフォルダーを指定する。テスト出力はTemp以下。

テスト内容：300シードでの勝利・敗北・Push、A=14、カード選択順、重複なし、ディーラー先手、両者Fold、不均等ポット、各支払いの資金不足と再開、二重操作と古い演出ID拒否、次ラウンドの初期化、AI確率調整。

`Tests/HighCardDuel/UnityIntegrationTests.cs`は一時Unity検証プロジェクト用。既存手札プレハブとのドラッグ接続、プレイヤー手札の表向き表示・ディーラー手札の裏向き表示、コルーチン完了待ち、演出例外と再試行、Controller無効化と再開、資金補充、精算を確認する。

`PresentationIntegrationTests.cs`は同じ隔離環境で使用する演出テスト。実際のCasinoモデルとCinemachineを使い、ADVのテストダブルで完了待ち・中断復帰・ラウンドごとの実行回数を確認する。実際の宴の台詞・音声・シナリオ内容は、使用するラベルを設定して本プロジェクトで確認する。
