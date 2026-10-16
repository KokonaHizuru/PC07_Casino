using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Casino.Common
{
    /// <summary>ネストした演出の例外も捕捉し、完了まで待つ共通処理。</summary>
    public static class PresentationRoutine
    {
        /// <summary>演出を実行し、失敗時は通知して停止する。</summary>
        /// <param name="routine">実行する演出。</param>
        /// <param name="onFailure">失敗時の通知。</param>
        public static IEnumerator Run(IEnumerator routine, Action<Exception> onFailure)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            try
            {
                while (stack.Count > 0)
                {
                    object yielded = null;
                    Exception failure = null;
                    try
                    {
                        IEnumerator current = stack.Peek();
                        if (!current.MoveNext())
                        {
                            stack.Pop(); (current as IDisposable)?.Dispose(); continue;
                        }
                        yielded = current.Current;
                    }
                    catch (Exception exception) { failure = exception; }
                    if (failure != null) { onFailure(failure); yield break; }
                    if (yielded is IEnumerator nested && !(yielded is CustomYieldInstruction)) stack.Push(nested);
                    else yield return yielded;
                }
            }
            finally { while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose(); }
        }
    }
}
