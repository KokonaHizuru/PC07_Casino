using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>短く押して離した右クリックだけを受け付ける。</summary>
[Serializable]
public sealed class ShortRightClickInput
{
    [SerializeField, Min(0f), Tooltip("この秒数以上の長押しはクリックとして扱いません。")]
    float maximumPressDuration = 0.3f;
    bool tracking;
    double pressedTime;

    /// <summary>右ボタンを離した際、押下時間が上限未満ならクリックを通知する。</summary>
    public bool Read()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !Application.isFocused) { Reset(); return false; }
        double now = Time.realtimeSinceStartupAsDouble;
        if (mouse.rightButton.wasPressedThisFrame)
        {
            tracking = true;
            pressedTime = now;
        }
        if (mouse.rightButton.wasReleasedThisFrame)
        {
            bool clicked = tracking && now - pressedTime < maximumPressDuration;
            Reset();
            return clicked;
        }
        if (!mouse.rightButton.isPressed) Reset();
        return false;
    }

    /// <summary>操作できない期間に始まった押下を持ち越さないよう追跡を解除する。</summary>
    public void Reset()
    {
        tracking = false;
    }
}
