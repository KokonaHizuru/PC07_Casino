using System;
using System.Collections.Generic;

namespace Casino.Common
{
    /// <summary>通知時のフラグ判定を固定し、再試行でも同じイベントを選ぶ。</summary>
    public sealed class PresentationEventSelection<T> where T : PresentationEventDefinition
    {
        private readonly Dictionary<string, List<T>> selections = new Dictionary<string, List<T>>();

        /// <summary>ラウンドの選択記録を消去する。</summary>
        public void Clear() => selections.Clear();

        /// <summary>指定フラグが有効な全件を選び、該当がない場合だけ未指定の全件を選ぶ。</summary>
        /// <param name="key">通知の識別子。</param>
        /// <param name="entries">エディタで登録されたイベント。</param>
        /// <param name="flags">通知時のプレイヤーフラグ。</param>
        /// <param name="eligible">タイミングと一度限り条件の判定。</param>
        public IReadOnlyList<T> Select(string key, IEnumerable<T> entries, FlagData flags, Func<T, bool> eligible)
        {
            if (selections.TryGetValue(key, out var selected)) return selected;
            selected = new List<T>();
            var fallback = new List<T>();
            foreach (var entry in entries)
            {
                if (entry == null || !eligible(entry)) continue;
                if (!entry.HasFlag) fallback.Add(entry);
                else if (entry.IsFlagEnabled(flags)) selected.Add(entry);
            }
            if (selected.Count == 0) selected.AddRange(fallback);
            selections.Add(key, selected);
            return selected;
        }
    }
}
