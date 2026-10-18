using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>タイトルから新規ゲームを開始する。</summary>
public sealed class TitleManager : MonoBehaviour
{
    [SerializeField] private string openingScene = "OpeningScene";
    [SerializeField] private GameObject freePanel;
    private bool starting;
    /// <summary>プレイヤーデータとBET変更を初期化してオープニングへ進む。</summary>
    public void OnClickNewGame()
    {
        if (starting) return;
        if (GameInstance.Instance == null || !Application.CanStreamedLevelBeLoaded(openingScene))
        { Debug.LogError("GameInstanceとOpeningSceneの登録を確認してください。", this); return; }
        starting = true;
        GameInstance.Instance.playerData.Reset();
        GameInstance.Instance.casinoStory.Reset();
        SceneManager.LoadScene(openingScene);
    }
    /// <summary>設定画面を開く。</summary>
    public void OnClickOption() { if (UISettingWindow.Instance != null) UISettingWindow.Instance.Open(); }
    /// <summary>回想パネルを閉じる。</summary>
    public void OnClickFreePanelClose() { if (freePanel != null) freePanel.SetActive(false); }
    /// <summary>アプリケーションを終了する。</summary>
    public void OnClickExit() => Application.Quit();
}
