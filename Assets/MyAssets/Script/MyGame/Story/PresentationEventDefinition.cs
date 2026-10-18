using System;
using UnityEngine;

namespace Casino.Common
{
    /// <summary>イベントのフラグ条件と正常終了後の更新内容を保持する。</summary>
    public abstract class PresentationEventDefinition : ScriptableObject
    {
        [Tooltip("空欄はフラグ指定イベントが該当しない場合の既定イベント。")]
        [SerializeField] private string flagId;
        [SerializeField] private EventFlagChange[] completionFlags = Array.Empty<EventFlagChange>();
        public string FlagId => flagId;
        public bool HasFlag => !string.IsNullOrWhiteSpace(flagId);

        /// <summary>指定フラグが有効か確認する。</summary>
        /// <param name="flags">現在のプレイヤーフラグ。</param>
        public bool IsFlagEnabled(FlagData flags) => !HasFlag || flags != null && flags.Is(flagId, true);

        /// <summary>正常終了したイベントのフラグ変更を登録順に反映する。</summary>
        /// <param name="flags">更新するプレイヤーフラグ。</param>
        public void ApplyCompletionFlags(FlagData flags)
        {
            if (flags == null) throw new InvalidOperationException("イベントのPlayerDataが接続されていません。");
            foreach (var change in completionFlags)
                if (change != null && !string.IsNullOrWhiteSpace(change.flagId)) flags.SetFlag(change.flagId, change.value);
        }
    }

    [Serializable]
    public sealed class EventFlagChange
    {
        public string flagId;
        public bool value = true;
    }
}
