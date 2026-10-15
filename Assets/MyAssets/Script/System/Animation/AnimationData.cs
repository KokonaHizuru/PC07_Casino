using UnityEngine;

/// <summary>
/// ３軸のアニメーション管理クラス
/// </summary>
[System.Serializable]
public class Vector3Track
{
	public AnimationCurve x;
	public AnimationCurve y;
	public AnimationCurve z;

	public Vector3 Evaluate(float time)
	{
		return new Vector3(
			x.Evaluate(time),
			y.Evaluate(time),
			z.Evaluate(time)
		);
	}
}
