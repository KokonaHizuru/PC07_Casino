using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Graphic))]
public class UIGradientImage : BaseMeshEffect
{
	[Header("Corner Colors")]
	public Color topLeft = Color.white;
	public Color topRight = Color.white;
	public Color bottomLeft = Color.white;
	public Color bottomRight = Color.white;

	public override void ModifyMesh(VertexHelper vh)
	{
		if (!IsActive() || vh.currentVertCount == 0)
			return;

		Rect rect = graphic.rectTransform.rect;
		UIVertex vertex = new UIVertex();

		for (int i = 0; i < vh.currentVertCount; i++)
		{
			vh.PopulateUIVertex(ref vertex, i);

			float x = Mathf.InverseLerp(rect.xMin, rect.xMax, vertex.position.x);
			float y = Mathf.InverseLerp(rect.yMin, rect.yMax, vertex.position.y);

			Color bottom = Color.Lerp(bottomLeft, bottomRight, x);
			Color top = Color.Lerp(topLeft, topRight, x);
			Color color = Color.Lerp(bottom, top, y);

			vertex.color *= color;
			vh.SetUIVertex(vertex, i);
		}
	}

#if UNITY_EDITOR
	protected override void OnValidate()
	{
		base.OnValidate();

		if (graphic != null)
			graphic.SetVerticesDirty();
	}
#endif
}