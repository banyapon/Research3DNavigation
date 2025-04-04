using UnityEngine;
using UnityEngine.Splines;

public class SplineGenerator : MonoBehaviour
{
    [Tooltip("Spline เส้นที่ 1 (Spline A)")]
    public SplineContainer spline1;
    [Tooltip("Spline เส้นที่ 2 (Spline B)")]
    public SplineContainer spline2;
    [Tooltip("Object ที่จะเคลื่อนที่ตาม Spline")]
    public GameObject targetObject;
    [Tooltip("ความเร็วการเลื่อน t")]
    public float moveSpeed = 0.005f;
    [Tooltip("TextMesh Legacy สำหรับแสดงผล Spline และค่า t")]
    public TextMesh debugText;

    // Parameter t ของ spline (0-1)
    private float t = 0f;
    // Spline ปัจจุบัน
    private SplineContainer currentSpline;

    // ตัวแปรสำหรับจัดการ touch input และ inertia
    private float verticalInertia = 0f;
    private float horizontalInertia = 0f;
    private float lateralOffset = 0f; // สำหรับ offset แนวนอน (แกน X)
    private bool wasHorizontal = false;
    private Vector2 previousTouchPosition;
    private float touchStartTime;
    private float inertiaDamping = 0.95f;
    private float inertiaThreshold = 0.001f;
    private float sq3 = Mathf.Sqrt(3f);
    private bool inputActive = false;

    void Start()
    {
        // เริ่มต้นที่ spline เส้นที่ 1 (Spline A)
        currentSpline = spline1;
        if (currentSpline != null && targetObject != null)
        {
            UpdateTransformPosition();
            UpdateDebugText();
        }
    }

    void Update()
    {
        inputActive = false;

#if UNITY_EDITOR || UNITY_STANDALONE
        // รับค่า input ผ่านเมาส์ (สำหรับทดสอบใน Editor)
        if (Input.GetMouseButton(0))
        {
            inputActive = true;
            float mouseDeltaY = Input.GetAxis("Mouse Y");
            float verticalDelta = -mouseDeltaY * moveSpeed;
            MoveAlongSpline(verticalDelta);
            verticalInertia = verticalDelta / Time.deltaTime;
            wasHorizontal = false;
        }
        if (Input.GetMouseButtonUp(0))
        {
            touchStartTime = Time.time;
        }
#endif

        // รับค่า input จาก touch
        if (Input.touchCount > 0)
        {
            inputActive = true;
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    previousTouchPosition = touch.position;
                    touchStartTime = Time.time;
                    verticalInertia = 0f;
                    horizontalInertia = 0f;
                    wasHorizontal = false;
                    break;

                case TouchPhase.Moved:
                    Vector2 currentTouchPosition = touch.position;
                    Vector2 delta = currentTouchPosition - previousTouchPosition;

                    // ตรวจสอบทิศทางการเลื่อน: ถ้าแนวตั้งมากกว่าแนวนอน (มีสัดส่วนมากกว่า √3)
                    if (Mathf.Abs(delta.y) > sq3 * Mathf.Abs(delta.x))
                    {
                        float verticalDelta = -delta.y * moveSpeed;
                        MoveAlongSpline(verticalDelta);
                        verticalInertia = verticalDelta / Time.deltaTime;
                        wasHorizontal = false;
                    }
                    else
                    {
                        // เคลื่อนที่แนวนอน (สำหรับ lateral offset)
                        float horizontalDelta = delta.x * moveSpeed;
                        lateralOffset += horizontalDelta;
                        lateralOffset = Mathf.Clamp(lateralOffset, -1f, 1f);
                        horizontalInertia = horizontalDelta / Time.deltaTime;
                        wasHorizontal = true;
                    }
                    previousTouchPosition = currentTouchPosition;
                    break;

                case TouchPhase.Ended:
                    // เมื่อ touch จบ ไม่ต้องรีเซ็ต lateralOffset ที่นี่
                    break;
            }
        }

        // เมื่อไม่มีการ input ให้ใช้ Inertia เพื่อเคลื่อนที่ต่อ
        if (!inputActive)
        {
            if (Mathf.Abs(verticalInertia) > inertiaThreshold)
            {
                float verticalDelta = verticalInertia * Time.deltaTime;
                MoveAlongSpline(verticalDelta);
                verticalInertia *= inertiaDamping;
            }
            else
            {
                verticalInertia = 0f;
            }

            if (Mathf.Abs(horizontalInertia) > inertiaThreshold)
            {
                lateralOffset += horizontalInertia * Time.deltaTime;
                lateralOffset = Mathf.Clamp(lateralOffset, -1f, 1f);
                horizontalInertia *= inertiaDamping;
            }
            else
            {
                horizontalInertia = 0f;
            }
        }

        UpdateTransformPosition();
        UpdateDebugText();
    }

    void MoveAlongSpline(float distanceDelta)
    {
        if (currentSpline != null && targetObject != null)
        {
            Spline spline = currentSpline.Spline;
            float newT;
            // คำนวณตำแหน่งใหม่และค่า parameter ใหม่ตามระยะทางที่เลื่อนไป
            Vector3 newPosition = SplineUtility.GetPointAtLinearDistance(spline, t, distanceDelta, out newT);

            // กรณีเลื่อนไปข้างหน้า (จาก Spline A ไป B) เมื่อ t ≥ 1
            if (newT >= 1f && currentSpline == spline1)
            {
                float overflow = newT - 1f;
                currentSpline = spline2;
                t = 0f;
                if (overflow > 0f)
                {
                    MoveAlongSpline(overflow);
                }
            }
            // กรณีเลื่อนถอยหลัง (จาก Spline B ไป A) เมื่อ t ≤ 0
            else if (newT <= 0f && currentSpline == spline2)
            {
                float overflow = 0f - newT;
                currentSpline = spline1;
                t = 1f;
                if (overflow > 0f)
                {
                    // เรียก MoveAlongSpline ด้วยค่า overflow ในทิศทางถอยหลัง (negative)
                    MoveAlongSpline(-overflow);
                }
            }
            else
            {
                t = newT;
            }
        }
    }

    void UpdateTransformPosition()
    {
        if (currentSpline != null && targetObject != null)
        {
            Spline spline = currentSpline.Spline;
            Vector3 splinePos = spline.EvaluatePosition(t);
            // คำนวณ tangent และ right vector สำหรับ lateral offset
            Vector3 tangent = spline.EvaluateTangent(t);
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            // ปรับตำแหน่ง targetObject โดยรวม lateral offset ด้วย
            targetObject.transform.position = splinePos + lateralOffset * right;
            if (tangent != Vector3.zero)
            {
                targetObject.transform.rotation = Quaternion.LookRotation(tangent);
            }
        }
    }

    void UpdateDebugText()
    {
        if (debugText != null)
        {
            // แสดงผล Spline A หรือ B ตาม currentSpline
            string splineLabel = (currentSpline == spline1) ? "A" : "B";
            debugText.text = splineLabel + ":" + t.ToString("F2");
        }
    }
}
