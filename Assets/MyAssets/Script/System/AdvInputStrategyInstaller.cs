using UnityEngine;
using Utage;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using Utage.InputSystem;
#endif

/// <summary>
/// ゲーム固有の宴入力方式を登録します。
/// </summary>
public static class AdvInputStrategyInstaller
{
	/// <summary>
	/// ゲーム固有の宴入力方式を登録する
	/// </summary>
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void RuntimeInitialize()
	{
		SetCustomInputEnabled(true);
	}

	/// <summary>
	/// ゲーム固有の宴入力方式と既定の入力方式を切り替える
	/// </summary>
	/// <param name="enabled">ゲーム固有の入力方式を使用する場合はtrue</param>
	public static void SetCustomInputEnabled(bool enabled)
	{
		InputUtil.SetInputStrategy(enabled
			? new AdvInputStrategy()
			: CreateDefaultInputStrategy());
	}

	/// <summary>
	/// 使用中のUnity入力方式に対応する宴の既定入力方式を生成する
	/// </summary>
	static IInputStrategy CreateDefaultInputStrategy()
	{
#if ENABLE_INPUT_SYSTEM
		return new InputSystemStrategy();
#else
		return new InputManagerStrategy();
#endif
	}

#if ENABLE_INPUT_SYSTEM
	private sealed class AdvInputStrategy : InputSystemStrategy
#else
	private sealed class AdvInputStrategy : InputManagerStrategy
#endif
	{
		/// <summary>
		/// 右クリックによるメッセージウィンドウ非表示を無効化する
		/// </summary>
		public override bool IsInputGuiClose()
		{
			return false;
		}

		/// <summary>
		/// 既定キーまたはZキーによる会話送りを判定する
		/// </summary>
		public override bool IsInputNextButton()
		{
			if (base.IsInputNextButton()) return true;
#if ENABLE_INPUT_SYSTEM
			return Keyboard.current != null && Keyboard.current.zKey.wasPressedThisFrame;
#else
			return Input.GetKeyDown(KeyCode.Z);
#endif
		}
	}
}
