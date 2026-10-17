using System;
using System.Collections;
using System.Collections.Generic;
using Casino.Cards;
using UnityEngine;

namespace Casino.Common
{
    /// <summary>各ゲームで共用する3Dカードの配置・整列・Flip。</summary>
    public partial class TableCardPresenter : MonoBehaviour
    {
        [SerializeField] private CardModelCatalog catalog;
        [SerializeField] private Transform playerOrigin;
        [SerializeField] private Transform dealerOrigin;
        [SerializeField] private Transform[] playerSlots = new Transform[3];
        [SerializeField] private Transform[] dealerSlots = new Transform[3];
        [SerializeField, Min(.001f)] private float modelScale = 1;
        [SerializeField, Min(.01f)] private float moveDuration = .4f;
        [SerializeField, Min(.01f)] private float flipDuration = .35f;
        [SerializeField, Min(0)] private float liftHeight = .06f;
        [Tooltip("有効なら枚数に応じて中央揃えし、指定幅の中へ重ねて並べる。")]
        [SerializeField] private bool spreadToFit;
        [SerializeField, Min(.1f)] private float maxRowWidth = .6f;
        private readonly Dictionary<PlayingCard, Placement> cards = new Dictionary<PlayingCard, Placement>();
        private sealed class Placement { public Transform Transform; public bool Player; public int Index; }

        /// <summary>前ラウンドの場札を片付ける。</summary>
        public void Clear()
        {
            ClearTransfers();
            foreach (Placement card in cards.Values)
                if (card.Transform != null) { card.Transform.gameObject.SetActive(false); Destroy(card.Transform.gameObject); }
            cards.Clear();
        }

        /// <summary>指定のカードを一度だけ生成し、同じ列も必要に応じて整列する。</summary>
        /// <param name="card">カード。</param>
        /// <param name="player">プレイヤー側か。</param>
        /// <param name="index">0から始まる配布順。</param>
        /// <param name="faceUp">配置時から表向きにするか。</param>
        /// <param name="animate">移動をアニメーションするか。</param>
        /// <param name="glide">弧を描かず滑るように移動するか。</param>
        public IEnumerator Place(PlayingCard card, bool player, int index, bool faceUp = false, bool animate = true, bool glide = false)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            Transform anchor = Anchor(player, index);
            if (!cards.TryGetValue(card, out Placement placement))
            {
                Transform origin = player ? playerOrigin : dealerOrigin;
                if (origin == null || catalog == null) throw new InvalidOperationException("カードカタログと移動開始位置を設定してください。");
                Transform instance = Instantiate(catalog.GetModel(card), transform).transform;
                instance.localScale = Vector3.one * modelScale;
                instance.SetPositionAndRotation(origin.position, anchor.rotation * (faceUp ? Quaternion.identity : Quaternion.Euler(180, 0, 0)));
                placement = new Placement { Transform = instance, Player = player, Index = index };
                cards.Add(card, placement);
            }
            var row = new List<Placement>();
            foreach (Placement item in cards.Values) if (item.Player == player) row.Add(item);
            var starts = new Vector3[row.Count]; var targets = new Vector3[row.Count];
            for (int i = 0; i < row.Count; i++) { starts[i] = row[i].Transform.position; targets[i] = Position(row[i]); }
            Quaternion initialRotation = placement.Transform.rotation;
            Quaternion rotation = anchor.rotation * (faceUp ? Quaternion.identity : Quaternion.Euler(180, 0, 0));
            float duration = animate ? Mathf.Max(.01f, moveDuration) : 0;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                for (int i = 0; i < row.Count; i++)
                    row[i].Transform.position = Vector3.Lerp(starts[i], targets[i], t)
                        + (row[i] == placement && !glide ? Vector3.up * Mathf.Sin(t * Mathf.PI) * liftHeight : Vector3.zero);
                placement.Transform.rotation = Quaternion.Slerp(initialRotation, rotation, t);
                yield return null;
            }
            for (int i = 0; i < row.Count; i++) row[i].Transform.position = targets[i];
            placement.Transform.rotation = rotation;
        }

        /// <summary>古い札を横へ退避し、同じ位置へ新しい札を配る。再試行でも二重生成しない。</summary>
        /// <param name="previous">交換前の札。</param>
        /// <param name="replacement">交換後の札。</param>
        /// <param name="player">プレイヤー側か。</param>
        /// <param name="index">配布位置。</param>
        /// <param name="faceUp">新しい札を表向きにするか。</param>
        public IEnumerator Replace(PlayingCard previous, PlayingCard replacement, bool player, int index, bool faceUp)
        {
            if (previous.Equals(replacement)) throw new ArgumentException("交換前後は異なるカードを指定してください。");
            if (cards.TryGetValue(previous, out Placement old))
            {
                if (old.Player != player || old.Index != index) throw new InvalidOperationException("交換位置が一致しません。");
                Transform anchor = Anchor(player, index);
                Vector3 start = old.Transform.position;
                Vector3 target = anchor.position - anchor.right * .55f + Vector3.up * liftHeight;
                float duration = Mathf.Max(.01f, moveDuration);
                for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                {
                    old.Transform.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0, 1, elapsed / duration));
                    yield return null;
                }
                old.Transform.gameObject.SetActive(false); Destroy(old.Transform.gameObject); cards.Remove(previous);
            }
            yield return Place(replacement, player, index, faceUp);
        }

        /// <summary>配置済みのカードを持ち上げながら表へ返す。</summary>
        /// <param name="card">公開するカード。</param>
        public IEnumerator Reveal(PlayingCard card)
        {
            if (!cards.TryGetValue(card, out Placement placement) || placement.Transform == null)
                throw new InvalidOperationException("公開するカードが配置されていません。");
            Vector3 position = Position(placement);
            Quaternion start = placement.Transform.rotation, target = Anchor(placement.Player, placement.Index).rotation;
            float duration = Mathf.Max(.01f, flipDuration);
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                placement.Transform.SetPositionAndRotation(position + Vector3.up * Mathf.Sin(t * Mathf.PI) * liftHeight, Quaternion.Slerp(start, target, t));
                yield return null;
            }
            placement.Transform.SetPositionAndRotation(position, target);
        }

        /// <summary>固定スロットまたは可変枚数の中央基準を取得する。</summary>
        /// <param name="player">プレイヤー側か。</param>
        /// <param name="index">配布順。</param>
        private Transform Anchor(bool player, int index)
        {
            Transform[] slots = player ? playerSlots : dealerSlots;
            int target = spreadToFit ? slots.Length / 2 : index;
            if (target < 0 || target >= slots.Length || slots[target] == null) throw new InvalidOperationException("場札の配置位置が未設定です。");
            return slots[target];
        }

        /// <summary>固定位置または最大幅の中へ詰めた配布順の位置を算出する。</summary>
        /// <param name="placement">配置情報。</param>
        private Vector3 Position(Placement placement)
        {
            Transform anchor = Anchor(placement.Player, placement.Index);
            if (!spreadToFit) return anchor.position;
            int count = 0;
            foreach (Placement item in cards.Values) if (item.Player == placement.Player) count++;
            float step = count > 1 ? Mathf.Min(.14f, maxRowWidth / (count - 1)) : 0;
            // 重なるカードの深度を僅かにずらし、Z-fightingを防ぐ。
            return anchor.position + anchor.right * ((placement.Index - (count - 1) * .5f) * step) + anchor.up * (placement.Index * .0005f);
        }
    }
}
