using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>所有シーンの初期化・終了中の入力を停止する。シーンとともに破棄される。</summary>
public sealed class SceneInputGate : MonoBehaviour
{
    sealed class UiState
    {
        public bool enabled;
        public int owners;
    }
    // シーンの破棄と生成が重なる場合も、共有EventSystemを早期に復帰させない。
    static readonly Dictionary<EventSystem, UiState> uiStates = new();
    static readonly HashSet<SceneInputGate> gates = new();
    readonly HashSet<EventSystem> ownedSystems = new();
    public bool IsBlocked { get; private set; }
    public static bool IsAnyBlocked
    {
        get
        {
            foreach (var gate in gates) if (gate != null && gate.IsBlocked) return true;
            return false;
        }
    }

    /// <summary>操作を許可する前の状態で登録する。</summary>
    void Awake()
    {
        gates.Add(this);
        SceneManager.sceneLoaded += OnSceneLoaded;
        Block();
    }

    /// <summary>所有シーンの入力を停止する。</summary>
    public void Block()
    {
        IsBlocked = true;
        CaptureUi();
    }

    /// <summary>UI入力の停止前の状態を保持し、停止する。</summary>
    void CaptureUi()
    {
        foreach (var system in FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!ownedSystems.Add(system)) continue;
            if (!uiStates.TryGetValue(system, out var state))
            {
                state = new UiState { enabled = system.enabled };
                uiStates.Add(system, state);
            }
            state.owners++;
            system.enabled = false;
        }
    }

    /// <summary>追加シーンで生成されたUIも、停止期間中は入力を受け付けないようにする。</summary>
    /// <param name="scene">読み込まれたシーン</param>
    /// <param name="mode">読み込み方式</param>
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsBlocked) CaptureUi();
    }

    /// <summary>初期化と明転の完了後、所有シーンの入力を許可する。</summary>
    public void Release()
    {
        IsBlocked = false;
        foreach (var system in ownedSystems)
        {
            if (!uiStates.TryGetValue(system, out var state)) continue;
            if (--state.owners != 0) continue;
            if (system != null) system.enabled = state.enabled;
            uiStates.Remove(system);
        }
        ownedSystems.Clear();
    }

    /// <summary>指定シーンが所有する入力制御を取得する。</summary>
    /// <param name="scene">対象のシーン</param>
    public static SceneInputGate FindForScene(Scene scene)
    {
        foreach (var gate in gates) if (gate != null && gate.gameObject.scene == scene) return gate;
        return null;
    }

    /// <summary>シーン破棄時に自身の停止状態だけを解除する。</summary>
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Release();
        gates.Remove(this);
    }
}
