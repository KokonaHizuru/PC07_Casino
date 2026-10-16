using System;
using System.Collections;
using System.Collections.Generic;
using Casino.Cards;
using UnityEngine;

namespace Casino.HighCardDuel
{
    /// <summary>選ばれたカードを3Dの机へ置き、公開時に表へ返す。</summary>
    public sealed class DuelTableCards : MonoBehaviour
    {
        [SerializeField] private CasinoCardModelCatalog catalog;
        [SerializeField] private Transform playerOrigin;
        [SerializeField] private Transform dealerOrigin;
        [SerializeField] private Transform[] playerSlots = new Transform[3];
        [SerializeField] private Transform[] dealerSlots = new Transform[3];
        [SerializeField, Min(0.001f)] private float modelScale = 1;
        [SerializeField, Min(0.01f)] private float moveDuration = 0.4f;
        [SerializeField, Min(0.01f)] private float flipDuration = 0.35f;
        [SerializeField, Min(0)] private float liftHeight = 0.06f;
        private readonly Dictionary<PlayingCard, Transform> instances = new Dictionary<PlayingCard, Transform>();

        /// <summary>前ラウンドの場札を片付ける。</summary>
        public void Clear()
        {
            foreach (Transform card in instances.Values)
                if (card != null) { card.gameObject.SetActive(false); Destroy(card.gameObject); }
            instances.Clear();
        }

        /// <summary>同じカードは重複生成せず、指定位置へ裏向きで移動する。</summary>
        /// <param name="cue">選択されたカード、対象者、フェーズ。</param>
        public IEnumerator Place(DuelCue cue)
        {
            Transform slot = GetSlot(cue);
            PlayingCard card = cue.Card.Value;
            if (!instances.TryGetValue(card, out Transform instance) || instance == null)
            {
                if (catalog == null) throw new InvalidOperationException("カードモデルカタログが未設定です。");
                Transform origin = cue.Actor == DuelActor.Player ? playerOrigin : dealerOrigin;
                if (origin == null) throw new InvalidOperationException("カードの移動開始位置が未設定です。");
                instance = Instantiate(catalog.GetModel(card), transform).transform;
                instance.localScale = Vector3.one * modelScale;
                instance.SetPositionAndRotation(origin.position, slot.rotation * Quaternion.Euler(180, 0, 0));
                instances[card] = instance;
            }
            Vector3 start = instance.position;
            Quaternion startRotation = instance.rotation;
            Quaternion target = slot.rotation * Quaternion.Euler(180, 0, 0);
            float duration = Mathf.Max(0.01f, moveDuration);
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                instance.position = Vector3.Lerp(start, slot.position, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * liftHeight);
                instance.rotation = Quaternion.Slerp(startRotation, target, t);
                yield return null;
            }
            instance.SetPositionAndRotation(slot.position, target);
        }

        /// <summary>机上のカードを持ち上げながら表向きへ返す。</summary>
        /// <param name="cue">公開するカード情報。</param>
        public IEnumerator Reveal(DuelCue cue)
        {
            Transform slot = GetSlot(cue);
            if (!instances.TryGetValue(cue.Card.Value, out Transform instance) || instance == null)
                throw new InvalidOperationException("公開する3Dカードが配置されていません。");
            Quaternion start = instance.rotation;
            float duration = Mathf.Max(0.01f, flipDuration);
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                instance.position = slot.position + Vector3.up * (Mathf.Sin(t * Mathf.PI) * liftHeight);
                instance.rotation = Quaternion.Slerp(start, slot.rotation, t);
                yield return null;
            }
            instance.SetPositionAndRotation(slot.position, slot.rotation);
        }

        /// <summary>対象者と公開順に対応する机上の位置を取得する。</summary>
        /// <param name="cue">対象カード情報。</param>
        private Transform GetSlot(DuelCue cue)
        {
            if (!cue.Card.HasValue || cue.Phase < 1 || cue.Phase > 3 || cue.Actor == DuelActor.None)
                throw new ArgumentException("カード、対象者、公開順が必要です。", nameof(cue));
            Transform[] slots = cue.Actor == DuelActor.Player ? playerSlots : dealerSlots;
            if (slots == null || slots.Length < cue.Phase || slots[cue.Phase - 1] == null)
                throw new InvalidOperationException("机上のカード配置位置が未設定です。");
            return slots[cue.Phase - 1];
        }
    }
}
