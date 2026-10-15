// UTAGE: Unity Text Adventure Game Engine (c) Ryohei Tokimura

using System;
using UnityEngine;
using Utage;
using UtageExtensions;

namespace Utage
{

	//CharacterシートやTextureシートのConditionalをマイフレームチェックして
	//自動的にオブジェクトの表示変更を行うコンポーネント
	public class SampleAutoConditionalGraphicSwitcher : MonoBehaviour
	{
		AdvEngine Engine => this.GetAdvEngineCacheFindIfMissing(ref engine);
		public AdvEngine engine = null;

		public AdvGraphicInfo CurrentGraphicInfo { get; private set; }
		AdvGraphicInfoList CurrentGraphicList { get; set; }
		
		//グラフィックオブジェクトの描画時によばれるイベント。AdvGraphicInfoは、キャラクターシートのパターンごとの情報が入っている
		public void OnDraw(AdvGraphicInfo graphicInfo)
		{
//			Debug.Log("OnDraw");
			CurrentGraphicInfo = graphicInfo;
			CurrentGraphicList = FindGraphicInfoList(graphicInfo);
		}

		//指定の表示を含む、AdvGraphicInfoListを取得
		AdvGraphicInfoList FindGraphicInfoList(AdvGraphicInfo graphicInfo)
		{
			foreach (var item in Engine.DataManager.SettingDataManager.CharacterSetting.List)
			{
				var graphicList = item.Graphic; 
				if (graphicList.InfoList.Contains(graphicInfo))
				{
					return graphicList;
				}
			}
			foreach (var item in Engine.DataManager.SettingDataManager.TextureSetting.List)
			{
				var graphicList = item.Graphic; 
				if (graphicList.InfoList.Contains(graphicInfo))
				{
					return graphicList;
				}
			}
			
			Debug.LogError($"{graphicInfo.Key}　はTextureシートまたは、Characterシート以下にありません");
			return null;
		}
		

		//毎フレームチェック（ほかのコマンド処理などが終わったあとなのでLateUpdateで行う）
		void LateUpdate()
		{
			if(CurrentGraphicList==null) return;
			
			//Mainの内部でConditionalの条件チェック
			var main = CurrentGraphicList.Main; 
			if ( main!= CurrentGraphicInfo)
			{
				if (!CurrentGraphicList.InfoList.Contains(CurrentGraphicInfo))
				{
					Debug.LogError($"{CurrentGraphicInfo.Key} のリストが更新されていません");
					return;
				}
				CurrentGraphicInfo = main;
				OnAutoChangeGraphic();
			}
		}

		void OnAutoChangeGraphic()
		{
//			Debug.Log("OnAutoChangeGraphic");
			AdvGraphicObject graphicObject = this.GetComponent<AdvGraphicObject>();
			if (!graphicObject.Layer.CurrentGraphics.ContainsValue(graphicObject))
			{
				//すでにフェードアウトなどで管理外
				return;
			}

			OnAutoChangeGraphic(graphicObject, CurrentGraphicInfo);
		}

		//表示を変える
		void OnAutoChangeGraphic(AdvGraphicObject graphicObject,AdvGraphicInfo graphic)
		{
			//AdvGraphicInfoはロード済みの前提
			//AvatarやダイシングならFileNameが同じならロード済み。
			//（注）デフォルトのテクスチャを使うだけの表示は、新しいテクスチャロードが必要なので、その処理も追記が必要
			
			//表示を変更
			graphicObject.TargetObject.ChangeResourceOnDraw(graphic,0);
			if (graphicObject.RenderObject != graphicObject.TargetObject)
			{
				//テクスチャ書き込みをしている
				graphicObject.RenderObject.ChangeResourceOnDraw(graphic, 0);
			}
		}
	}
}
