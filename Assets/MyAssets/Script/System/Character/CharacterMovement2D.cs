using System;
using UnityEngine;

/// <summary>
/// 2Dキャラクターの移動を制御するクラス
/// </summary>
public class CharacterMovement2D : MonoBehaviour
{
    [SerializeField, Min(0f)] float moveSpeed = 4f;
    Func<Vector2, Vector2> positionConstraint;
    Rigidbody2D body;
    Vector2 moveInput;

    /// <summary>2D物理移動に必要なRigidbodyを取得して設定する。</summary>
    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    /// <summary>移動先に適用する位置制限を設定する。</summary>
    /// <param name="constraint">移動先を制限する関数</param>
    public void SetPositionConstraint(Func<Vector2, Vector2> constraint)
    {
        positionConstraint = constraint;
    }

    /// <summary>次の物理更新で使用する移動方向を設定する。</summary>
    /// <param name="direction">入力方向</param>
    public void SetMoveInput(Vector2 direction)
    {
        moveInput = Vector2.ClampMagnitude(direction, 1f);
    }

    /// <summary>物理座標と表示座標を開始地点へ移動する。</summary>
    /// <param name="position">移動先の座標</param>
    public void Teleport(Vector2 position)
    {
        moveInput = Vector2.zero;
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = position;
        }
        Vector3 current = transform.position;
        transform.position = new Vector3(position.x, position.y, current.z);
    }

    /// <summary>物理演算に沿って移動し、足元を歩行範囲内に収める。</summary>
    void FixedUpdate()
    {
        if (body == null || moveInput == Vector2.zero) return;
        Vector2 position = body.position + moveInput * (moveSpeed * Time.fixedDeltaTime);
        if (positionConstraint != null) position = positionConstraint(position);
        body.MovePosition(position);
    }
}
