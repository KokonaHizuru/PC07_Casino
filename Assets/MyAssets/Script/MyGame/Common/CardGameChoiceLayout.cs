using System;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.Common
{
    /// <summary>選択可能なボタンを中央にまとめ、ラウンド後の操作を右下に配置する。</summary>
    [Serializable]
    public sealed class CardGameChoiceLayout
    {
        [SerializeField] private Vector2 centerOffset = Vector2.zero;
        [SerializeField, Min(0)] private float spacing = 16;
        [SerializeField] private Vector2 cornerMargin = new Vector2(24, 24);
        private Button[] choices;
        private RectTransform[] rects;

        /// <summary>参照を保持し、開始前のボタンを非表示にする。</summary>
        /// <param name="roundButton">ラウンド後の操作ボタン。</param>
        /// <param name="buttons">中央に並べる順の選択肢。</param>
        public void Initialize(Button roundButton, params Button[] buttons)
        {
            choices = buttons;
            rects = new RectTransform[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                rects[i] = (RectTransform)buttons[i].transform;
                SetVisible(buttons[i], false);
            }
            if (roundButton == null) return;
            var corner = (RectTransform)roundButton.transform;
            Vector2 size = corner.rect.size;
            corner.anchorMin = corner.anchorMax = new Vector2(1, 0);
            corner.pivot = new Vector2(1, 0);
            corner.sizeDelta = size;
            corner.anchoredPosition = new Vector2(-cornerMargin.x, cornerMargin.y);
            SetVisible(roundButton, false);
        }

        /// <summary>選択できるボタンだけを表示する。</summary>
        /// <param name="button">対象ボタン。</param>
        /// <param name="available">選択できるか。</param>
        public static void SetVisible(Button button, bool available)
        {
            if (button == null) return;
            button.interactable = available;
            button.gameObject.SetActive(available);
        }

        /// <summary>表示中のボタンを横並びにし、カード操作中は右下へ寄せる。</summary>
        /// <param name="cardPlayActive">カード操作を受け付けているか。</param>
        public void Refresh(bool cardPlayActive = false)
        {
            float width = 0;
            int count = 0;
            for (int i = 0; i < choices.Length; i++)
            {
                if (choices[i] == null || !choices[i].gameObject.activeSelf) continue;
                width += rects[i].rect.width;
                count++;
            }
            float totalWidth = width + Mathf.Max(0, count - 1) * spacing;
            float x = cardPlayActive ? -cornerMargin.x - totalWidth : -totalWidth * .5f;
            for (int i = 0; i < choices.Length; i++)
            {
                if (choices[i] == null || !choices[i].gameObject.activeSelf) continue;
                var rect = rects[i];
                Vector2 size = rect.rect.size;
                rect.anchorMin = rect.anchorMax = cardPlayActive ? new Vector2(1, 0) : new Vector2(.5f, .5f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = size;
                rect.anchoredPosition = cardPlayActive
                    ? new Vector2(x + size.x * .5f, cornerMargin.y + size.y * .5f)
                    : centerOffset + new Vector2(x + size.x * .5f, 0);
                x += size.x + spacing;
            }
        }
    }
}
