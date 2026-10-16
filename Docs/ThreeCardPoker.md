# スリーカード・ポーカー

`Assets/Scenes/Scene_Poker.unity` を開いて再生する。`PokerPresentation` の `PokerSceneUI` は、既定で独立した確認用資金1000、Ante 10を使う。

## 操作とルール

- Anteを支払い、双方へ3枚配る。プレイヤーは表、ディーラーは3枚とも裏で表示する。
- PlayでAnteと同額を追加し、ディーラーを公開して勝負する。FoldはAnteのみを失って終了し、ディーラーは公開しない。
- 役順はHigh Card < Pair < Flush < Straight < Three of a Kind < Straight Flush。A-K-Qが最高、A-2-3が最低のStraight。K-A-2はStraightにならない。
- 同役は規定の数字で比較し、スートによる優劣は付けない。Pairはペアの数字、次に残る1枚を比較する。
- ディーラーはQueen High以上、またはPair以上の役でQualification成立。`PokerDealer` に判定を分離している。
- Qualification不成立：Ante勝利分と元金でAnteの2倍、さらにPlay Betを返却する（受取合計はAnteの3倍）。
- 成立時：勝利はAnteとPlay Betそれぞれ元金込み2倍（合計Anteの4倍）。同点は両方返却。敗北は両方失う。
- Anteは正の整数。奇数も利用できる。Pair Plus、Ante Bonusなどのサイドベットはない。
- 毎ラウンド、共通の52枚山札を再生成・シャッフルする。同じラウンド内で重複しない。

## Inspector設定

`PokerSceneUI` の Initial BetがAnte、Preview Moneyが確認用資金。Preview ModeをOFFにすると既存の `GameInstance.Instance.playerData` に接続するため、本編側のGameInstanceを先に起動する。

所持金は `PlayerData.casinoMoney`。不足時は強制Foldにせず `WaitingForFunds` で保留する。PreviewのResume Fundsボタンは不足額を補充して再開する。本番では外部システムで資金を増やし、`PokerController.ResumeAfterFunding()` または同ボタンを呼ぶ。支払い待機の取り消しやラウンド途中セーブは今回の対象外。

Next Roundで次のラウンド。確認用数値は本編の初期資金・掛け金を確定するものではない。

## 演出と宴

ProjectのCreate → Casino → Poker → Presentation EventでSOを作り、`PokerPresentationDirector` のEventsへ登録する。登録順に実行する。

- Timing：配布、選択前、行動前後、Play支払い、公開前後、Qualification判定、結果、精算、資金不足など。
- Stage：標準演出の前／後。
- Actor / Action / Result：対象者、Play／Fold、結果条件。None／Anyは限定しない。
- Qualification：Any／Qualified／NotQualified。判定前はQualifiedとNotQualifiedのどちらにも一致しない。
- Once Per Round：ラウンド中の再発を抑制する。
- Camera View：変更なし／通常／机上。
- Scenario Label：宴のシナリオラベル。会話とモーダルの終了までゲーム進行を待つ。

`Assets/MyAssets/Poker/BeforePlayerDecision.asset` と `BeforePlay.asset` は無効状態のひな型。実際のラベルを指定してEvent EnabledをONにする。宴本体・会話内容は既存システムから接続する。

コードからはControllerの `TimingReached`（Action通知）、`Presentation`（Func<PokerCue, IEnumerator>の完了待ち）、`PresentationFailed` を利用できる。演出通知はIDで照合し、二重完了を拒否する。失敗時は進行を止め、Retryで同じ通知から再開する。完了済みSO、配置済みカード、徴収・配当を重複実行しない。

`DealerCards`／`DealerScore`は表示制御や演出で扱う内部情報。非公開中のUIでは参照しない。標準表示はDealerCardsRevealedまで役を隠し、Fold後も伏せたままにする。

## 共通部分とシーン

Cardsの山札・手札UIと、Commonの資金接続、3Dカード、Cinemachine視点切替／手札退避、宴待機、コルーチン例外処理をそのまま使用する。ポーカー用に共通コードの複製や変更はしていない。ゲーム固有のコードは `Assets/MyAssets/Script/MyGame/ThreeCardPoker` に配置。

机、Casinoの52枚のモデル、カタログ、UIプレハブ、背景マテリアルも共有参照。NormalCamera／TableCameraはSceneViewで調整できる。DealerAnchorは人物配置用の空オブジェクト。ポーカーは手札をドラッグせずボタンで操作する。

コピーした未設定シーンにはCasino → Poker → Set Up Copied Sceneで接続を作れる。設定済みの場合は何も変更しない。

## 検証

`Tests/ThreeCardPoker/Run.ps1`：22,100通りの3枚組について役の分布と順序不変性を検証。同役の比較、Aの例外、Qualificationの境界、10,000ラウンドの全勝敗、配当、奇数Ante、資金不足からの再開、古い／二重の完了通知も確認する。

`Tests/ThreeCardPoker/UnityIntegrationTests.cs`：本番Assetsへ配置しない隔離検証用。コピーしたシーンと実際のCasinoモデル・UI・Cinemachineを使用する。宴接続だけテストダブルとして、終了待ち・モーダル待ち・中断復帰を検証する。実際の宴シナリオはラベル設定後に本編で確認する。
