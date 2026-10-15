#if UTAGE_URP_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Utage.RenderPipeline.Urp
{
	//UTAGEで必要になるRenderFeaturesを追加する
	public class UrpRendererConverter
	{
		// プロジェクトウィンドウの右クリックメニューに追加
		public class ContextMenu : EditorWindow
		{
			const string MenuPath = "Assets/Utage/AddRenderFeatures";

			[MenuItem(MenuPath)]
			static void GetFilePath()
			{
				if (Selection.activeObject is ScriptableRendererData rendererData)
				{
					UrpRendererConverter converter = new UrpRendererConverter();
					converter.AddRenderFeatures(rendererData);
				}
			}

			[MenuItem(MenuPath, true)]
			static bool IsValidate()
			{
				return Selection.activeObject is ScriptableRendererData;
			}
		}
		
		//UTAGEで必要になるRenderFeatureを追加する
		public void AddRenderFeatures(ScriptableRendererData rendererData)
		{
			AddRenderFeatureIfMissing<CaptureRenderFeature>(rendererData);
			AddRenderFeatureIfMissing<GrayScaleRenderFeature>(rendererData);
			AddRenderFeatureIfMissing<MosaicRenderFeature>(rendererData);
			AddRenderFeatureIfMissing<NegaPosiRenderFeature>(rendererData);
			AddRenderFeatureIfMissing<SepiaRenderFeature>(rendererData);
			AddRenderFeatureIfMissing<FishEyeRenderFeature>(rendererData);
			AddRenderFeatureIfMissing<TwirlRenderFeature>(rendererData);
			AddRenderFeatureIfMissing<VortexRenderFeature>(rendererData);
			AddRenderFeatureIfMissing<BlurRenderFeature>(rendererData);
			AddRenderFeatureIfMissing<ColorFadeRenderFeature>(rendererData);
			AddRenderFeatureIfMissing<RuleFadeRenderFeature>(rendererData);
		
			//リフレクションを使ってValidateRendererFeaturesを呼び出す
			const string MethodName = "ValidateRendererFeatures";
			Type rendererDataType = rendererData.GetType();
			MethodInfo validateMethod = rendererDataType.GetMethod(MethodName, BindingFlags.NonPublic | BindingFlags.Instance);
			if (validateMethod != null)
			{
				validateMethod.Invoke(rendererData, null);
			}
			else
			{
				Debug.LogError($"{MethodName}method not found");
			}

			EditorUtility.SetDirty(rendererData);
		}
		
		//RendererDataにT型のRenderFeatureがなければ追加
		void AddRenderFeatureIfMissing<T>(ScriptableRendererData rendererData)
			where T: ScriptableRendererFeature
		{
			foreach (var feature in rendererData.rendererFeatures)
			{
				if(feature!=null && feature.GetType() == typeof(T))
				{
					return;
				}
			}
			var renderFeature = ScriptableObject.CreateInstance<T>();
			renderFeature.name = typeof(T).Name;
			rendererData.rendererFeatures.Add(renderFeature);
			if (EditorUtility.IsPersistent(rendererData))
			{
				AssetDatabase.AddObjectToAsset(renderFeature, rendererData);
			}
		}
	}
}
#endif
