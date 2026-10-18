using System.Collections;
using System.Collections.Generic;
using Casino.Cards;
using UnityEngine;

namespace Casino.Common
{
    public partial class TableCardPresenter
    {
        [Header("Draw / Discard")]
        [SerializeField] private Transform deckAnchor;
        [SerializeField] private Transform discardAnchor;
        [SerializeField, Min(.01f)] private float transferDuration = .45f;
        [Tooltip("プレイヤーへ3Dカードを配る時間（秒）。捨て札の移動時間はTransfer Durationで指定する。")]
        [SerializeField, Min(.01f)] private float drawTransferDuration = .25f;
        [Tooltip("配布カードを場札の基準面から浮かせる高さ。配布の開始・終了の両方に適用する。")]
        [SerializeField, Min(0)] private float drawSurfaceClearance = .008f;
        [Tooltip("捨てる際の弧の高さ。配布は直進する。")]
        [SerializeField, Min(0)] private float transferArc = .15f;
        [SerializeField, Min(.0001f)] private float discardThickness = .002f;
        private CardTableCameraRig transferCamera;
        private readonly Dictionary<PlayingCard, Transfer> draws = new Dictionary<PlayingCard, Transfer>();
        private readonly Dictionary<PlayingCard, Transfer> discards = new Dictionary<PlayingCard, Transfer>();
        private sealed class Transfer
        {
            public Transform Model;
            public Vector3 Start, End;
            public Quaternion Rotation;
            public float Elapsed;
            public bool Finished;
        }
        public int DiscardCount => discards.Count;
        public int TableCardCount => cards.Count;

        /// <summary>移動位置とカメラを一度取得し、未指定なら机上の既定位置を生成する。</summary>
        protected virtual void Awake()
        {
            transferCamera = GetComponentInParent<CardTableCameraRig>();
            Transform center = Anchor(true, 0);
            if (deckAnchor == null) deckAnchor = CreateTransferAnchor("DeckAnchor", dealerOrigin.position, center.rotation);
            if (discardAnchor == null) discardAnchor = CreateTransferAnchor("DiscardAnchor", center.position + center.right * .55f + center.forward * .15f, center.rotation);
        }

        /// <summary>SceneViewから調整できる移動位置を生成する。</summary>
        /// <param name="name">位置の名前。</param>
        /// <param name="position">ワールド位置。</param>
        /// <param name="rotation">机面の向き。</param>
        private Transform CreateTransferAnchor(string name, Vector3 position, Quaternion rotation)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(transform.parent, false);
            anchor.SetPositionAndRotation(position, rotation);
            return anchor;
        }

        /// <summary>3Dで裏向きに配った後、UI手札への追加演出を待つ。再試行で重複しない。</summary>
        /// <param name="hand">追加先の手札。</param>
        /// <param name="card">配るカード。</param>
        /// <param name="player">プレイヤー側か。</param>
        /// <param name="index">追加位置。</param>
        /// <param name="faceUp">UI上で表向きにするか。</param>
        public IEnumerator DrawToHand(CardHandUI hand, PlayingCard card, bool player, int index, bool faceUp)
        {
            if (!player)
            {
                if (hand != null) yield return hand.AddCardAnimated(card, faceUp, index, false);
                yield break;
            }
            if (transferCamera != null) yield return transferCamera.SwitchTo(CardCameraView.Normal);
            if (!draws.TryGetValue(card, out Transfer motion))
            {
                Transform destination = player ? playerOrigin : dealerOrigin;
                Transform surface = Anchor(player, 0);
                Vector3 normal = surface.up;
                Vector3 start = deckAnchor.position;
                Vector3 end = destination.position;
                start += normal * (drawSurfaceClearance - Vector3.Dot(start - surface.position, normal));
                end += normal * (drawSurfaceClearance - Vector3.Dot(end - surface.position, normal));
                motion = CreateTransfer(card, start, end, surface.rotation);
                draws.Add(card, motion);
            }
            yield return MoveTransfer(motion, 0, drawTransferDuration);
            if (motion.Model != null) { motion.Model.gameObject.SetActive(false); Destroy(motion.Model.gameObject); motion.Model = null; }
            if (hand != null) yield return hand.AddCardAnimated(card, faceUp, index, player);
        }

        /// <summary>UIから直ちに取り除き、3Dの捨て札を裏向きで積む。</summary>
        /// <param name="hand">取り除く手札。</param>
        /// <param name="card">捨てるカード。</param>
        /// <param name="player">プレイヤー側か。</param>
        public IEnumerator DiscardFromHand(CardHandUI hand, PlayingCard card, bool player)
        {
            hand?.RemoveCard(card);
            if (transferCamera != null) yield return transferCamera.SwitchTo(CardCameraView.Normal);
            if (!discards.TryGetValue(card, out Transfer motion))
            {
                Transform origin = player ? playerOrigin : dealerOrigin;
                motion = CreateTransfer(card, origin.position,
                    discardAnchor.position + discardAnchor.up * (discards.Count * discardThickness), discardAnchor.rotation);
                discards.Add(card, motion);
            }
            yield return MoveTransfer(motion, transferArc, transferDuration);
        }

        /// <summary>通常の場札とは別に、裏向きの移動モデルを生成する。</summary>
        /// <param name="card">対象カード。</param>
        /// <param name="start">開始位置。</param>
        /// <param name="end">終了位置。</param>
        /// <param name="rotation">机面の向き。</param>
        private Transfer CreateTransfer(PlayingCard card, Vector3 start, Vector3 end, Quaternion rotation)
        {
            var model = Instantiate(catalog.GetModel(card), transform).transform;
            model.localScale = Vector3.one * modelScale;
            Quaternion back = rotation * Quaternion.Euler(180, 0, 0);
            model.SetPositionAndRotation(start, back);
            return new Transfer { Model = model, Start = start, End = end, Rotation = back };
        }

        /// <summary>中断時の経過時間を保持し、指定した弧の高さで移動する。</summary>
        /// <param name="motion">再開可能な移動情報。</param>
        /// <param name="arc">弧の高さ。0なら直進する。</param>
        /// <param name="seconds">移動にかける秒数。</param>
        private IEnumerator MoveTransfer(Transfer motion, float arc, float seconds)
        {
            if (motion.Finished) yield break;
            float duration = Mathf.Max(.01f, seconds);
            while (motion.Elapsed < duration)
            {
                float t = Mathf.SmoothStep(0, 1, motion.Elapsed / duration);
                motion.Model.position = Vector3.Lerp(motion.Start, motion.End, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * arc);
                motion.Elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            motion.Model.SetPositionAndRotation(motion.End, motion.Rotation);
            motion.Finished = true;
        }

        /// <summary>次ラウンドに備えて配布モデルと捨て札を片付ける。</summary>
        private void ClearTransfers()
        {
            foreach (var motion in draws.Values) if (motion.Model != null) { motion.Model.gameObject.SetActive(false); Destroy(motion.Model.gameObject); }
            foreach (var motion in discards.Values) if (motion.Model != null) { motion.Model.gameObject.SetActive(false); Destroy(motion.Model.gameObject); }
            draws.Clear(); discards.Clear();
        }
    }
}
