using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/Radar Chart")]
[RequireComponent(typeof(CanvasRenderer))]
public class UIRadarChart : MaskableGraphic
{
	const int MinAxisCount = 3;

	[SerializeField, Min(MinAxisCount)] int axisCount = 5;
	[SerializeField] List<float> values = new();
	[SerializeField, Min(0.0001f)] float maxValue = 100f;
	[SerializeField, Range(0f, 1f)] float radiusScale = 0.9f;
	[SerializeField, Min(0)] int gridLevels = 4;
	[SerializeField, Min(0f)] float lineWidth = 2f;
	[SerializeField, Min(0f)] float antiAliasWidth = 1f;
	[SerializeField] Color fillColor = new(0.2f, 0.65f, 1f, 0.35f);
	[SerializeField] Color outlineColor = new(0.2f, 0.65f, 1f, 1f);
	[SerializeField] Color gridColor = new(1f, 1f, 1f, 0.25f);
	[SerializeField] Color axisLineColor = new(1f, 1f, 1f, 0.2f);
	[SerializeField] bool clockwise = true;
	[SerializeField] float startAngle = 90f;
	[Header("Animation")]
	[Tooltip("有効化されたときに、中心（値0）から設定値までレーダーチャートをアニメーションさせます。")]
	[SerializeField] bool animateOnEnable = true;
	[Tooltip("SetValue または SetValues で値を変更したときに、変更前の表示から新しい値までアニメーションさせます。")]
	[SerializeField] bool animateOnValueChanged = true;
	[Tooltip("有効化時および PlayAnimation() 実行時のアニメーション時間（秒）です。0にすると即座に表示されます。")]
	[SerializeField, Min(0f)] float initialAnimationDuration = 0.5f;
	[Tooltip("SetValue または SetValues による値変更時のアニメーション時間（秒）です。0にすると即座に更新されます。")]
	[SerializeField, Min(0f)] float valueAnimationDuration = 0.3f;
	[Tooltip("初期表示と値変更のアニメーションに使用するイージング（加速・減速の仕方）です。")]
	[SerializeField] Ease animationEase = Ease.OutQuad;

	readonly List<float> displayedValues = new();
	Tween valueTween;

	public int AxisCount => axisCount;
	public IReadOnlyList<float> Values => values;

	protected override void Awake()
	{
		base.Awake();
		raycastTarget = false;
		EnsureValueCount();
		CopyTargetToDisplayedValues();
	}

	protected override void OnEnable()
	{
		EnsureValueCount();
		bool shouldAnimate =
			Application.isPlaying && animateOnEnable && initialAnimationDuration > 0f;

		// base.OnEnableで最初のメッシュが作られる前に、全軸を0へ揃える
		if (shouldAnimate)
			ResetDisplayedValuesToZero();
		else
			CopyTargetToDisplayedValues();

		base.OnEnable();

		if (shouldAnimate)
		{
			AnimateToTarget(initialAnimationDuration);
		}
		else
		{
			CopyTargetToDisplayedValues();
			SetVerticesDirty();
		}
	}

	protected override void OnDisable()
	{
		valueTween?.Kill();
		valueTween = null;
		base.OnDisable();
	}

	protected override void OnPopulateMesh(VertexHelper vertexHelper)
	{
		vertexHelper.Clear();

		if (axisCount < MinAxisCount || maxValue <= 0f)
			return;

		EnsureValueCount();

		Rect rect = GetPixelAdjustedRect();
		Vector2 center = rect.center;
		float radius = Mathf.Min(rect.width, rect.height) * 0.5f * radiusScale;
		if (radius <= 0f)
			return;

		DrawFilledChart(vertexHelper, center, radius);

		if (lineWidth > 0f)
		{
			DrawGrid(vertexHelper, center, radius);
			DrawAxisLines(vertexHelper, center, radius);
			DrawChartOutline(vertexHelper, center, radius);
		}
	}

	public void SetValues(IReadOnlyList<float> newValues)
	{
		SetValues(newValues, animateOnValueChanged);
	}

	/// <summary>
	/// 値を変更せず、0から現在の設定値までアニメーションする
	/// </summary>
	public void PlayAnimation()
	{
		PlayAnimation(initialAnimationDuration);
	}

	/// <summary>
	/// 値を変更せず、指定時間で0から現在の設定値までアニメーションする
	/// </summary>
	public void PlayAnimation(float duration)
	{
		EnsureValueCount();
		valueTween?.Kill();
		valueTween = null;
		ResetDisplayedValuesToZero();
		SetVerticesDirty();

		if (!Application.isPlaying || duration <= 0f)
		{
			CopyTargetToDisplayedValues();
			SetVerticesDirty();
			return;
		}

		AnimateToTarget(duration);
	}

	public void SetValues(IReadOnlyList<float> newValues, bool animate)
	{
		if (newValues == null)
			return;

		List<float> startValues = GetDisplayedValuesCopy(newValues.Count);
		axisCount = Mathf.Max(MinAxisCount, newValues.Count);
		values.Clear();
		for (int i = 0; i < axisCount; i++)
			values.Add(i < newValues.Count ? newValues[i] : 0f);

		ApplyValues(startValues, animate, valueAnimationDuration);
	}

	public void SetValue(int index, float value)
	{
		SetValue(index, value, animateOnValueChanged);
	}

	public void SetValue(int index, float value, bool animate)
	{
		EnsureValueCount();
		if (index < 0 || index >= values.Count)
			return;

		List<float> startValues = GetDisplayedValuesCopy(axisCount);
		values[index] = value;
		ApplyValues(startValues, animate, valueAnimationDuration);
	}

	void DrawFilledChart(VertexHelper vertexHelper, Vector2 center, float radius)
	{
		int centerIndex = AddVertex(vertexHelper, center, fillColor);
		int firstIndex = vertexHelper.currentVertCount;

		for (int i = 0; i < axisCount; i++)
		{
			float rate = Mathf.Clamp01(GetDisplayedValue(i) / maxValue);
			AddVertex(vertexHelper, GetPoint(center, radius * rate, i), fillColor);
		}

		for (int i = 0; i < axisCount; i++)
		{
			int next = (i + 1) % axisCount;
			vertexHelper.AddTriangle(centerIndex, firstIndex + i, firstIndex + next);
		}
	}

	void DrawGrid(VertexHelper vertexHelper, Vector2 center, float radius)
	{
		for (int level = 1; level <= gridLevels; level++)
		{
			float levelRadius = radius * level / Mathf.Max(1, gridLevels);
			for (int i = 0; i < axisCount; i++)
			{
				Vector2 from = GetPoint(center, levelRadius, i);
				Vector2 to = GetPoint(center, levelRadius, (i + 1) % axisCount);
				AddLine(vertexHelper, from, to, lineWidth, gridColor);
			}
		}
	}

	void DrawAxisLines(VertexHelper vertexHelper, Vector2 center, float radius)
	{
		for (int i = 0; i < axisCount; i++)
			AddLine(vertexHelper, center, GetPoint(center, radius, i), lineWidth, axisLineColor);
	}

	void DrawChartOutline(VertexHelper vertexHelper, Vector2 center, float radius)
	{
		for (int i = 0; i < axisCount; i++)
		{
			float currentRate = Mathf.Clamp01(GetDisplayedValue(i) / maxValue);
			int next = (i + 1) % axisCount;
			float nextRate = Mathf.Clamp01(GetDisplayedValue(next) / maxValue);

			AddLine(
				vertexHelper,
				GetPoint(center, radius * currentRate, i),
				GetPoint(center, radius * nextRate, next),
				lineWidth,
				outlineColor);
		}
	}

	Vector2 GetPoint(Vector2 center, float radius, int index)
	{
		float direction = clockwise ? -1f : 1f;
		float angle = (startAngle + direction * 360f * index / axisCount) * Mathf.Deg2Rad;
		return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
	}

	static int AddVertex(VertexHelper vertexHelper, Vector2 position, Color color)
	{
		int index = vertexHelper.currentVertCount;
		UIVertex vertex = UIVertex.simpleVert;
		vertex.position = position;
		vertex.color = color;
		vertexHelper.AddVert(vertex);
		return index;
	}

	void AddLine(
		VertexHelper vertexHelper,
		Vector2 from,
		Vector2 to,
		float width,
		Color color)
	{
		Vector2 direction = to - from;
		if (direction.sqrMagnitude <= Mathf.Epsilon)
			return;

		Vector2 lineDirection = direction.normalized;
		float halfWidth = width * 0.5f;
		Vector2 normal = new Vector2(-lineDirection.y, lineDirection.x);

		// 隣接する線分との間に隙間ができないよう、両端を半幅分だけ伸ばす
		Vector2 extendedFrom = from - lineDirection * halfWidth;
		Vector2 extendedTo = to + lineDirection * halfWidth;
		Vector2 innerOffset = normal * halfWidth;
		AddQuad(
			vertexHelper,
			extendedFrom - innerOffset,
			extendedFrom + innerOffset,
			extendedTo + innerOffset,
			extendedTo - innerOffset,
			color,
			color);

		if (antiAliasWidth <= 0f)
			return;

		Vector2 outerOffset = normal * (halfWidth + antiAliasWidth);
		Color transparentColor = color;
		transparentColor.a = 0f;

		// 線の両側を透明へフェードさせ、斜線のギザギザを軽減する
		AddQuad(
			vertexHelper,
			extendedFrom - outerOffset,
			extendedFrom - innerOffset,
			extendedTo - innerOffset,
			extendedTo - outerOffset,
			transparentColor,
			color);
		AddQuad(
			vertexHelper,
			extendedFrom + innerOffset,
			extendedFrom + outerOffset,
			extendedTo + outerOffset,
			extendedTo + innerOffset,
			color,
			transparentColor);
	}

	static void AddQuad(
		VertexHelper vertexHelper,
		Vector2 bottomLeft,
		Vector2 topLeft,
		Vector2 topRight,
		Vector2 bottomRight,
		Color bottomColor,
		Color topColor)
	{
		int startIndex = vertexHelper.currentVertCount;
		AddVertex(vertexHelper, bottomLeft, bottomColor);
		AddVertex(vertexHelper, topLeft, topColor);
		AddVertex(vertexHelper, topRight, topColor);
		AddVertex(vertexHelper, bottomRight, bottomColor);
		vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
		vertexHelper.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
	}

	void EnsureValueCount()
	{
		axisCount = Mathf.Max(MinAxisCount, axisCount);
		values ??= new List<float>();

		while (values.Count < axisCount)
			values.Add(0f);

		if (values.Count > axisCount)
			values.RemoveRange(axisCount, values.Count - axisCount);
	}

	void ApplyValues(List<float> startValues, bool animate, float duration)
	{
		valueTween?.Kill();
		valueTween = null;

		if (!Application.isPlaying || !animate || duration <= 0f)
		{
			CopyTargetToDisplayedValues();
			SetVerticesDirty();
			return;
		}

		displayedValues.Clear();
		displayedValues.AddRange(startValues);
		ResizeDisplayedValues();
		AnimateToTarget(duration);
	}

	void AnimateToTarget(float duration)
	{
		valueTween?.Kill();
		List<float> startValues = GetDisplayedValuesCopy(axisCount);
		List<float> targetValues = new(values);
		for (int i = 0; i < axisCount; i++)
		{
			// 最大値を超える値も、見た目上の終点まで全時間を使って補間する
			startValues[i] = Mathf.Clamp(startValues[i], 0f, maxValue);
			targetValues[i] = Mathf.Clamp(targetValues[i], 0f, maxValue);
		}

		valueTween = DOVirtual.Float(0f, 1f, duration, progress =>
		{
			for (int i = 0; i < axisCount; i++)
				displayedValues[i] = Mathf.LerpUnclamped(startValues[i], targetValues[i], progress);

			SetVerticesDirty();
		})
		.SetEase(animationEase)
		.SetTarget(this)
		.OnComplete(() => valueTween = null);
	}

	float GetDisplayedValue(int index)
	{
		if (!Application.isPlaying || index >= displayedValues.Count)
			return values[index];

		return displayedValues[index];
	}

	List<float> GetDisplayedValuesCopy(int requestedCount)
	{
		int count = Mathf.Max(MinAxisCount, requestedCount);
		List<float> result = new(count);
		for (int i = 0; i < count; i++)
		{
			float value = i < displayedValues.Count
				? displayedValues[i]
				: 0f;
			result.Add(value);
		}

		return result;
	}

	void CopyTargetToDisplayedValues()
	{
		displayedValues.Clear();
		displayedValues.AddRange(values);
	}

	void ResetDisplayedValuesToZero()
	{
		ResizeDisplayedValues();
		for (int i = 0; i < displayedValues.Count; i++)
			displayedValues[i] = 0f;
	}

	void ResizeDisplayedValues()
	{
		while (displayedValues.Count < axisCount)
			displayedValues.Add(0f);

		if (displayedValues.Count > axisCount)
			displayedValues.RemoveRange(axisCount, displayedValues.Count - axisCount);
	}

#if UNITY_EDITOR
	protected override void OnValidate()
	{
		base.OnValidate();
		EnsureValueCount();
		SetVerticesDirty();
	}
#endif
}
