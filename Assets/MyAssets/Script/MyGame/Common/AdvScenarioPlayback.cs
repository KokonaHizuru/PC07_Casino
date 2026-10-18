using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Casino.Common
{
    /// <summary>各ゲームのADV再生と中断復帰を共通化する。</summary>
    public sealed class AdvScenarioPlayback
    {
        private AdvEngineController adv;
        private readonly Dictionary<string, RunState> runs = new Dictionary<string, RunState>();
        private sealed class RunState { public bool Finished; public bool Failed; }

        /// <summary>任意の宴管理を保持し、未指定時は初回再生で取得する。</summary>
        /// <param name="controller">宴管理。追加シーンで後から起動する場合はnull。</param>
        public AdvScenarioPlayback(AdvEngineController controller) { adv = controller; }

        /// <summary>ラウンドが終わった再生記録を破棄する。</summary>
        public void Clear() => runs.Clear();

        /// <summary>会話を一度開始し、再試行時も同じ会話の終了と入力解除を待つ。</summary>
        /// <param name="label">シナリオラベル。</param>
        /// <param name="key">再生を識別するキー。</param>
        public IEnumerator Play(string label, string key)
        {
            float deadline = Time.realtimeSinceStartup + 15;
            while (adv == null)
            {
                adv = AdvEngineController.Instance;
                if (adv != null) break;
                if (Time.realtimeSinceStartup > deadline) throw new InvalidOperationException("宴が起動していません。");
                yield return null;
            }
            while (adv.AdvEngine.IsWaitBootLoading)
            {
                if (Time.realtimeSinceStartup > deadline) throw new InvalidOperationException("宴の起動読み込みが完了しません。");
                yield return null;
            }
            if (!runs.TryGetValue(key, out RunState run) || run.Failed)
            {
                while (adv != null && adv.IsPlaying) yield return null;
                if (adv == null || !adv.isActiveAndEnabled) throw new InvalidOperationException("宴が無効です。");
                run = new RunState(); runs[key] = run;
                adv.JumpScenario(label, () => run.Finished = true, () => run.Failed = true);
            }
            while (!run.Finished && !run.Failed)
            {
                if (adv == null || !adv.isActiveAndEnabled || !adv.IsPlaying)
                { run.Failed = true; throw new InvalidOperationException("宴の再生が中断されました。"); }
                yield return null;
            }
            if (run.Failed) throw new InvalidOperationException("宴のシナリオを開始できません: " + label);
            while (adv != null && adv.UiModal != null && adv.UiModal.IsBlocking) yield return null;
        }
    }
}
