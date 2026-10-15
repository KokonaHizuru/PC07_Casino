using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CharacterTouchArea : MonoBehaviour,
	IPointerDownHandler,
	IDragHandler,
	IPointerUpHandler,
	IPointerEnterHandler,
	IPointerExitHandler
{
	/// <summary>
	/// 詳細ドラッグ開始イベント
	///
	/// int：
	/// 対象になったDragDataのインデックス
	///
	/// Vector2：
	/// ドラッグを開始したスクリーン座標
	/// </summary>
	[Serializable]
	public class DragStartUnityEvent : UnityEvent<int, Vector2>
	{
	}

	/// <summary>
	/// 詳細ドラッグ更新イベント
	///
	/// int：
	/// 現在操作しているDragDataのインデックス
	///
	/// Vector2：
	/// ドラッグ開始位置からの総移動量
	///
	/// Vector2：
	/// 前フレームからの移動量
	/// </summary>
	[Serializable]
	public class DragUpdateUnityEvent
		: UnityEvent<int, Vector2, Vector2>
	{
	}

	/// <summary>
	/// 詳細ドラッグ終了イベント
	///
	/// bool：
	/// ドラッグが成功したか
	///
	/// int：
	/// 対象になったDragDataのインデックス
	///
	/// Vector2：
	/// ドラッグ開始位置からの最終的な総移動量
	/// </summary>
	[Serializable]
	public class DragEndUnityEvent
		: UnityEvent<bool, int, Vector2>
	{
	}

	[Serializable]
	private class DragData
	{
		[Tooltip("受け付けるドラッグ方向")]
		[SerializeField]
		public Vector2 targetDirection = Vector2.down;

		[Tooltip("成功と判定するために必要な最小ドラッグ量")]
		[SerializeField]
		public float minDistance = 80f;

		[Tooltip("指定方向から許容する角度")]
		[SerializeField]
		public float angleThreshold = 30f;
	}

	[Header("ドラッグ判定")]

	[SerializeField]
	private List<DragData> dragData = new();

	[Tooltip("どのDragDataかを特定するために必要な最低移動量")]
	[SerializeField]
	private float dragRecognitionDistance = 5f;

	[Header("基本設定")]

	[Tooltip("クリック、ホールド、チャージ、ドラッグに使用するマウスボタン")]
	[SerializeField]
	private PointerEventData.InputButton operationButton =
		PointerEventData.InputButton.Left;

	[SerializeField]
	private float chargeTime = 1.0f;

	[SerializeField]
	private bool isDefaultActive = true;

	[Header("方向ドラッグイベント")]

	[Tooltip("DragDataの成功条件を満たした瞬間に呼ばれます")]
	[SerializeField]
	private UnityEvent<int> onDirectionDrag;

	[Header("詳細ドラッグイベント")]

	[Tooltip("移動方向からDragDataを特定できた瞬間に呼ばれます")]
	[SerializeField]
	private DragStartUnityEvent onDragStart;

	[Tooltip("ドラッグ成功前まで、毎フレーム呼ばれます")]
	[SerializeField]
	private DragUpdateUnityEvent onDragUpdate;

	[Tooltip("成功時は即座に、失敗時はボタンを離したときに呼ばれます")]
	[SerializeField]
	private DragEndUnityEvent onDragEnd;

	[Header("クリックイベント")]

	[SerializeField]
	private UnityEvent onClick;

	[Header("ホールドイベント")]

	[SerializeField]
	private UnityEvent onHold;

	[SerializeField]
	private UnityEvent onHoldStart;

	[SerializeField]
	private UnityEvent onHoldEnd;

	[Header("チャージイベント")]

	[SerializeField]
	private UnityEvent onCharge;

	// ドラッグ開始位置
	private Vector2 startPos;

	// 現在のポインター位置
	private Vector2 currentPointerPos;

	// 前フレームのポインター位置
	private Vector2 previousPointerPos;

	// 現在操作対象になっているDragDataのインデックス
	// 未決定の場合は-1
	private int activeDragDataIndex = -1;

	// このUI上で指定ボタンの操作を開始し、
	// 現在も操作を追跡しているか
	private bool isPointerPressed;

	// 詳細ドラッグを開始したか
	private bool isDetailDragStarted;

	// 詳細ドラッグの終了イベントを呼び出したか
	private bool isDetailDragEnded;

	// チャージ中か
	private bool isChargeHold;

	// 押下時間
	private float holdTimer;

	// チャージ完了済みか
	private bool isCharge;

	// ホールド中か
	private bool isHold;

	void Awake()
	{
		Image image = GetComponent<Image>();

		if (image != null)
		{
			// 判定用Imageを透明にする場合に使用してください。
			//image.color = new Color(0, 0, 0, 0);
		}
	}

	void Start()
	{
	}

	/// <summary>
	/// 初期化
	/// </summary>
	public void Initialize()
	{
		gameObject.SetActive(isDefaultActive);
	}

	/// <summary>
	/// 更新処理
	/// </summary>
	private void Update()
	{
		if (AdvEngineController.IsUiInputBlocked)
		{
			if (isPointerPressed) EndPointerOperation(currentPointerPos, false);
			return;
		}
		UpdateContinuousDrag();
		UpdateCharge();
		UpdateHold();
	}

	/// <summary>
	/// 詳細ドラッグを毎フレーム更新
	/// </summary>
	private void UpdateContinuousDrag()
	{
		if (!isPointerPressed)
			return;

		if (isDetailDragEnded)
			return;

		if (Mouse.current == null)
			return;

		/*
		 * PointerUpを受け取れなかった場合の保険。
		 * 詳細ドラッグ開始済みなら失敗として終了します。
		 */
		if (!IsOperationButtonPressed())
		{
			EndPointerOperation(
				Mouse.current.position.ReadValue(),
				false
			);

			return;
		}

		currentPointerPos =
			Mouse.current.position.ReadValue();

		Vector2 totalDelta =
			currentPointerPos - startPos;

		Vector2 frameDelta =
			currentPointerPos - previousPointerPos;

		/*
		 * まだ対象のDragDataが決まっていない場合、
		 * 現在の移動方向から対象を決定します。
		 */
		if (activeDragDataIndex < 0)
		{
			activeDragDataIndex =
				FindMatchingDragDataIndex(totalDelta);

			if (activeDragDataIndex >= 0)
			{
				StartDetailDrag(
					activeDragDataIndex
				);
			}
		}

		/*
		 * 対象のDragDataが決まっていなければ、
		 * 詳細ドラッグ更新イベントは呼びません。
		 */
		if (activeDragDataIndex < 0)
		{
			previousPointerPos =
				currentPointerPos;

			return;
		}

		/*
		 * 成功判定を先に行います。
		 *
		 * 成功したフレームではonDragUpdateを呼ばず、
		 * そのままonDragEndを呼びます。
		 */
		if (IsDragSuccess(
			activeDragDataIndex,
			totalDelta))
		{
			CompleteSuccessfulDrag(
				activeDragDataIndex,
				totalDelta
			);

			return;
		}

		/*
		 * 成功していない間だけ、
		 * 毎フレーム更新イベントを呼びます。
		 */
		onDragUpdate?.Invoke(
			activeDragDataIndex,
			totalDelta,
			frameDelta
		);

		previousPointerPos =
			currentPointerPos;
	}

	/// <summary>
	/// 現在の移動方向に一致するDragDataを検索
	/// </summary>
	private int FindMatchingDragDataIndex(
		Vector2 totalDelta)
	{
		if (dragData == null ||
			dragData.Count == 0)
		{
			return -1;
		}

		if (totalDelta.magnitude <
			dragRecognitionDistance)
		{
			return -1;
		}

		if (totalDelta.sqrMagnitude <=
			Mathf.Epsilon)
		{
			return -1;
		}

		int bestIndex = -1;
		float bestAngle = float.MaxValue;

		for (int i = 0; i < dragData.Count; i++)
		{
			DragData data = dragData[i];

			if (data == null)
				continue;

			if (data.targetDirection.sqrMagnitude
				<= Mathf.Epsilon)
			{
				continue;
			}

			float angle = Vector2.Angle(
				data.targetDirection.normalized,
				totalDelta.normalized
			);

			if (angle > data.angleThreshold)
				continue;

			/*
			 * 複数のDragDataに一致した場合は、
			 * 最も角度が近いものを使用します。
			 */
			if (angle < bestAngle)
			{
				bestAngle = angle;
				bestIndex = i;
			}
		}

		return bestIndex;
	}

	/// <summary>
	/// DragDataに紐づいた詳細ドラッグを開始
	/// </summary>
	private void StartDetailDrag(
		int dragDataIndex)
	{
		if (isDetailDragStarted)
			return;

		isDetailDragStarted = true;

		onDragStart?.Invoke(
			dragDataIndex,
			startPos
		);

		Debug.Log(
			$"Detail Drag Start: " +
			$"{gameObject.name}, " +
			$"DragData Index = {dragDataIndex}"
		);
	}

	/// <summary>
	/// 指定したDragDataの成功条件を満たしたか
	/// </summary>
	private bool IsDragSuccess(
		int dragDataIndex,
		Vector2 totalDelta)
	{
		if (dragDataIndex < 0)
			return false;

		if (dragDataIndex >= dragData.Count)
			return false;

		DragData data =
			dragData[dragDataIndex];

		if (data == null)
			return false;

		if (data.targetDirection.sqrMagnitude
			<= Mathf.Epsilon)
		{
			return false;
		}

		// 必要距離に達していない
		if (totalDelta.magnitude <
			data.minDistance)
		{
			return false;
		}

		float angle = Vector2.Angle(
			data.targetDirection.normalized,
			totalDelta.normalized
		);

		// 許容角度を超えている
		if (angle > data.angleThreshold)
			return false;

		return true;
	}

	/// <summary>
	/// ドラッグ成功処理
	/// </summary>
	private void CompleteSuccessfulDrag(
		int dragDataIndex,
		Vector2 totalDelta)
	{
		if (isDetailDragEnded)
			return;

		/*
		 * 従来の方向ドラッグイベントを呼び出します。
		 */
		onDirectionDrag?.Invoke(
			dragDataIndex
		);

		Debug.Log(
			$"Drag Success: " +
			$"{gameObject.name}, " +
			$"DragData Index = {dragDataIndex}"
		);

		/*
		 * 成功した瞬間に詳細ドラッグを終了します。
		 */
		EndDetailDrag(
			true,
			dragDataIndex,
			totalDelta
		);

		/*
		 * 成功後は、ボタンを離すまで待たずに
		 * 操作の追跡を終了します。
		 */
		StopCharge();
		StopHold();

		isPointerPressed = false;
	}

	/// <summary>
	/// チャージ処理
	/// </summary>
	private void UpdateCharge()
	{
		if (!isChargeHold)
			return;

		holdTimer += Time.deltaTime;

		if (isCharge)
			return;

		if (chargeTime <= 0.0f ||
			holdTimer >= chargeTime)
		{
			onCharge?.Invoke();

			// medicalCursor.SetGauge(0.0f);

			isCharge = true;
		}
		else
		{
			// medicalCursor.SetGauge(
			//     Mathf.Clamp01(
			//         holdTimer / chargeTime
			//     )
			// );
		}
	}

	/// <summary>
	/// ホールド中の処理
	/// </summary>
	private void UpdateHold()
	{
		if (!isHold)
			return;

		onHold?.Invoke();
	}

	/// <summary>
	/// UI範囲内で指定したマウスボタンの操作を開始
	/// </summary>
	public void OnPointerDown(
		PointerEventData eventData)
	{
		if (AdvEngineController.IsUiInputBlocked) return;
		if (eventData.button != operationButton)
		{
			return;
		}

		startPos = eventData.position;
		currentPointerPos = eventData.position;
		previousPointerPos = eventData.position;

		activeDragDataIndex = -1;

		isPointerPressed = true;
		isDetailDragStarted = false;
		isDetailDragEnded = false;

		/*
		 * onDragStartはここでは呼びません。
		 *
		 * 移動方向からDragDataが決まった時点で
		 * 呼び出します。
		 */

		// チャージ開始
		isChargeHold = true;
		holdTimer = 0.0f;
		isCharge = false;

		// ホールド開始
		if (!isHold)
		{
			Debug.Log("ホールド開始");
			onHoldStart?.Invoke();
		}

		isHold = true;

		// medicalCursor.SetGauge(0.0f);
	}

	/// <summary>
	/// EventSystemのドラッグ処理
	/// </summary>
	public void OnDrag(
		PointerEventData eventData)
	{
		if (eventData.button != operationButton)
		{
			return;
		}

		if (!isPointerPressed)
			return;

		if (isDetailDragEnded)
			return;

		currentPointerPos =
			eventData.position;

		Vector2 totalDelta =
			currentPointerPos - startPos;

		/*
		 * Updateより先にOnDragが呼ばれた場合にも、
		 * DragDataを特定できるようにします。
		 */
		if (activeDragDataIndex < 0)
		{
			activeDragDataIndex =
				FindMatchingDragDataIndex(
					totalDelta
				);

			if (activeDragDataIndex >= 0)
			{
				StartDetailDrag(
					activeDragDataIndex
				);
			}
		}

		if (activeDragDataIndex < 0)
			return;

		/*
		 * 成功した場合はUpdateを待たずに、
		 * その場で終了イベントを呼びます。
		 */
		if (IsDragSuccess(
			activeDragDataIndex,
			totalDelta))
		{
			CompleteSuccessfulDrag(
				activeDragDataIndex,
				totalDelta
			);
		}
	}

	/// <summary>
	/// 指定したマウスボタンを離したとき
	/// </summary>
	public void OnPointerUp(
		PointerEventData eventData)
	{
		if (eventData.button != operationButton)
		{
			return;
		}

		/*
		 * 成功時はCompleteSuccessfulDragで
		 * isPointerPressedがfalseになっているため、
		 * ここでは何も起きません。
		 */
		if (!isPointerPressed)
			return;

		EndPointerOperation(
			eventData.position,
			false
		);
	}

	/// <summary>
	/// ボタンを離したことによる操作終了
	/// </summary>
	private void EndPointerOperation(
		Vector2 endPosition,
		bool isSuccess)
	{
		if (!isPointerPressed)
			return;

		currentPointerPos = endPosition;

		Vector2 totalDelta =
			currentPointerPos - startPos;

		Debug.Log(
			$"Hold Time = {holdTimer}"
		);

		Debug.Log(
			$"Drag Total Delta = {totalDelta}"
		);

		/*
		 * 詳細ドラッグを一度でも開始していれば、
		 * 失敗として終了イベントを呼びます。
		 */
		if (isDetailDragStarted &&
			!isDetailDragEnded)
		{
			EndDetailDrag(
				isSuccess,
				activeDragDataIndex,
				totalDelta
			);
		}

		/*
		 * ドラッグが成功しておらず、
		 * 短時間の操作ならクリックとして扱います。
		 */
		if (!isSuccess &&
			holdTimer < 0.5f &&
			!isDetailDragStarted)
		{
			onClick?.Invoke();
		}

		StopHold();
		ResetPointerState();
	}

	/// <summary>
	/// 詳細ドラッグ終了イベントを呼び出す
	/// </summary>
	private void EndDetailDrag(
		bool isSuccess,
		int dragDataIndex,
		Vector2 totalDelta)
	{
		if (isDetailDragEnded)
			return;

		isDetailDragEnded = true;

		onDragEnd?.Invoke(
			isSuccess,
			dragDataIndex,
			totalDelta
		);

		Debug.Log(
			$"Detail Drag End: " +
			$"Success = {isSuccess}, " +
			$"DragData Index = {dragDataIndex}, " +
			$"Total Delta = {totalDelta}"
		);
	}

	/// <summary>
	/// UI範囲内にポインターが入ったとき
	/// </summary>
	public void OnPointerEnter(
		PointerEventData eventData)
	{
		// medicalCursor.ImageActive(true);

		if (Mouse.current == null)
			return;

		if (IsOperationButtonPressed())
		{
			if (!isHold)
			{
				Debug.Log("ホールド開始");
				onHoldStart?.Invoke();
			}

			isHold = true;
		}
	}

	/// <summary>
	/// UI範囲外へポインターが出たとき
	/// </summary>
	public void OnPointerExit(
		PointerEventData eventData)
	{
		// medicalCursor.ImageActive(false);

		/*
		 * 詳細ドラッグは範囲外へ出ても継続します。
		 * isPointerPressedはfalseにしません。
		 */

		StopCharge();

		if (isHold)
		{
			Debug.Log("ホールド終了");
			onHoldEnd?.Invoke();
		}

		isHold = false;
	}

	/// <summary>
	/// Inspectorで指定したマウスボタンが押されているか
	/// </summary>
	private bool IsOperationButtonPressed()
	{
		if (Mouse.current == null)
			return false;

		switch (operationButton)
		{
			case PointerEventData.InputButton.Right:
				return Mouse.current.rightButton.isPressed;

			case PointerEventData.InputButton.Middle:
				return Mouse.current.middleButton.isPressed;

			default:
				return Mouse.current.leftButton.isPressed;
		}
	}

	/// <summary>
	/// ホールド停止
	/// </summary>
	private void StopHold()
	{
		if (isHold)
		{
			Debug.Log("ホールド終了");
			onHoldEnd?.Invoke();
		}

		isHold = false;
	}

	/// <summary>
	/// チャージ停止
	/// </summary>
	private void StopCharge()
	{
		isChargeHold = false;
		holdTimer = 0.0f;
		isCharge = false;

		// medicalCursor.SetGauge(0.0f);
	}

	/// <summary>
	/// 操作状態を初期化
	/// </summary>
	private void ResetPointerState()
	{
		isPointerPressed = false;

		activeDragDataIndex = -1;

		isDetailDragStarted = false;
		isDetailDragEnded = false;

		isChargeHold = false;
		holdTimer = 0.0f;
		isCharge = false;

		isHold = false;

		startPos = Vector2.zero;
		currentPointerPos = Vector2.zero;
		previousPointerPos = Vector2.zero;

		// medicalCursor.SetGauge(0.0f);
	}
}
