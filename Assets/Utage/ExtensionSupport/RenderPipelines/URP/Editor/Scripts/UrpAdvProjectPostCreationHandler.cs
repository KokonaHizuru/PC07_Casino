#if UTAGE_URP_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Utage.RenderPipeline.Urp
{
	//「UTAGE」新規プロジェクト作成時の追加処理
	//URP対応処理を行う
	public class UrpAdvProjectPostCreationHandler : IAdvProjectPostCreationHandler
	{
		//エディタ起動時やスクリプトリロード時に、ハンドラーを登録
		[InitializeOnLoadMethod]
		static void Initialize()
		{
			AdvProjectCreator.AddPostCreationHandler(new UrpAdvProjectPostCreationHandler());
		}

		//プロジェクト作成後の処理
		public void OnPostCreateProject(AdvProjectCreator project)
		{
			Debug.Log("OnPostCreateProject " + project.ProjectName);
			if (!UrpRenderPipelineAssetsCreator.CheckTemplateAssets() || !UrpSceneConverter.CheckTemplateAssets())
			{
				Debug.LogError("Not found Urp Template Asset. Please import Extension Package\n Menu/Utage/Extension Package Manager");
				return;
			}

			string projectName = project.ProjectName;

			//URPに必要なアセットを作成
			string renderPipelineAssetsPath = Path.Combine(UtageEditorToolKit.SystemIOFullPathToAssetPath(project.NewProjectDir), "RenderPipeline");
			UrpRenderPipelineAssetsCreator creator = new();
			var assetsDictionary = creator.CreateAssets(renderPipelineAssetsPath, projectName);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			if (project is IAdvProjectCreatorAssetOnly)
			{
				//アセットのみのプロジェクト作成の場合は、ここまで
				return;
			}

			//URPのプロジェクト設定を初期化
			int rendererIndex = -1;
			if (!RenderPipelineUtil.IsUrp())
			{
				//現在のRenderPipelineがURPじゃない
				Debug.LogError("Current RenderPipeline is not URP");
			}
			else
			{
				//デフォルト設定されているRendererDataを取得
				(ScriptableRendererData renderer, int index) = UrpProjectSettingsUtil.GetDefaultRendererData();
				if (renderer != null)
				{
					rendererIndex = index;
					//UTAGEで必要になるRendererを追加する
					new UrpRendererConverter().AddRenderFeatures(renderer);
				}
				
#if URP_17_OR_NEWER
				if (project is IAdvProjectCreatorUrpGraphicSettings urpGraphicSettings)
				{
					if (urpGraphicSettings.AutoClearUrpVolumes)
					{
						//現在のRendererにVolumeProfileが設定されている場合は、VolumeProfileを削除する
						UniversalRenderPipelineAsset currentRendererPipeLine = UrpUtil.GetCurrentRendererPipeLine();
						if (currentRendererPipeLine == null)
						{
							Debug.LogError("Not found UniversalRenderPipelineAsset");
						}
						else if (currentRendererPipeLine.volumeProfile !=null)
						{
							currentRendererPipeLine.volumeProfile = null;
						}
					}
				}
#endif
			}

			//現在のシーンをURPのシーンとしてコンバート
			UrpSceneConverter sceneConverter = new()
			{
				RendererIndex = rendererIndex,
				BaseCameraClearBackGroundColor = project is not IAdvProjectCreatorAddScene, 
				FlexibleScreenSize = project is IAdvProjectCreatorAddScene,
			};
			if (sceneConverter.SetUp())
			{
				sceneConverter.SwapVolumeProfileAssets(assetsDictionary);
			}
		}
	}
}
#endif
