using UnityEngine;
using UnityEngine.Splines;

public class SplineMoveLane : MonoBehaviour
{
    [Header("Spline Container ที่มีหลาย Splines (0=Center, 1=Left, 2=Right)")]
    public SplineContainer container; // มี Spline 0, 1, 2 อยู่ภายใน

    [Header("Object ที่จะเคลื่อนที่ตาม Spline")]
    public GameObject targetObject;

    [Header("ความเร็วการเลื่อน")]
    public float horizontalSpeed = 0.0005f;
    public float verticalSpeed = 0.0005f;

    [Header("TextMesh Legacy สำหรับแสดงผลสถานะเลนและค่า t")]
    public TextMesh debugText;

    private float t = 0f; // ตำแหน่งบน Spline
    private float lateralOffset = 0f;
    private float horizontalInertia = 0f;
    private float inertiaDamping = 0.95f;
    private float inertiaThreshold = 0.001f;
    private bool inputActive = false;
    private Vector2 previousTouchPosition;

    private int currentLaneIndex = 0; // 0 = Center, 1 = Left, 2 = Right
    public float laneSwitchDistanceThreshold = 1.0f;

    void Start()
    {
        if (container == null || container.Splines.Count < 3)
        {
            Debug.LogError("SplineContainer ต้องมีอย่างน้อย 3 Spline (Center, Left, Right)");
            return;
        }

        t = 0f;
        currentLaneIndex = 0;
        UpdateTransformPosition();
        UpdateDebugText();
    }

    void Update()
    {
        if (container == null || container.Splines.Count < 3)
            return;

        inputActive = false;

#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButton(0))
        {
            inputActive = true;
            float mouseDeltaX = Input.GetAxis("Mouse X");
            float mouseDeltaY = Input.GetAxis("Mouse Y");

            if (Mathf.Abs(mouseDeltaX) >= Mathf.Abs(mouseDeltaY))
            {
                float horizontalDelta = mouseDeltaX * horizontalSpeed;
                lateralOffset += horizontalDelta;
                horizontalInertia = horizontalDelta / Time.deltaTime;
            }
            else
            {
                float verticalDelta = -mouseDeltaY * verticalSpeed;
                t = Mathf.Clamp(t + verticalDelta, 0f, 1f);
                //lateralOffset = 0f;
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            TryAutoSwitchByOffset();
        }
#endif

        if (Input.touchCount > 0)
        {
            inputActive = true;
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    previousTouchPosition = touch.position;
                    horizontalInertia = 0f;
                    break;

                case TouchPhase.Moved:
                    Vector2 currentTouchPosition = touch.position;
                    Vector2 delta = currentTouchPosition - previousTouchPosition;

                    if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
                    {
                        float horizontalDelta = delta.x * horizontalSpeed;
                        lateralOffset += horizontalDelta;
                        horizontalInertia = horizontalDelta / Time.deltaTime;
                    }
                    else
                    {
                        float verticalDelta = -delta.y * verticalSpeed;
                        t = Mathf.Clamp(t + verticalDelta, 0f, 1f);
                    }

                    previousTouchPosition = currentTouchPosition;
                    break;

                case TouchPhase.Ended:
                    TryAutoSwitchByOffset();
                    break;
            }
        }

        if (!inputActive)
        {
            if (Mathf.Abs(horizontalInertia) > inertiaThreshold)
            {
                lateralOffset += horizontalInertia * Time.deltaTime;
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


    void TryAutoSwitchByOffset()
    {
        if (lateralOffset <= -0.6f)
        {
            if (currentLaneIndex == 0) // Center -> Left
            {
                TrySwitchLane(1);
            }
            else if (currentLaneIndex == 2) // Right -> Center
            {
                TrySwitchLane(0);
            }
        }
        else if (lateralOffset >= 0.6f)
        {
            if (currentLaneIndex == 0) // Center -> Right
            {
                TrySwitchLane(2);
            }
            else if (currentLaneIndex == 1) // Left -> Center
            {
                TrySwitchLane(0);
            }
        }
        else
        {
            // Snap กลับกลางถ้าไม่ข้าม
            SnapLateralOffset();
        }
    }


    void TrySwitchLaneByInput(float horizontalDelta)
    {
        if (currentLaneIndex == 0) // Center
        {
            if (horizontalDelta < 0f) TrySwitchLane(1); // Left
            else if (horizontalDelta > 0f) TrySwitchLane(2); // Right
        }
        else if (currentLaneIndex == 1 && horizontalDelta > 0f) // Left -> Center
        {
            TrySwitchLane(0);
        }
        else if (currentLaneIndex == 2 && horizontalDelta < 0f) // Right -> Center
        {
            TrySwitchLane(0);
        }
    }

    void TrySwitchLane(int newLaneIndex)
    {
        if (newLaneIndex < 0 || newLaneIndex >= container.Splines.Count)
            return;

        if (newLaneIndex == currentLaneIndex)
            return;

        // คำนวณตำแหน่งปัจจุบันใน spline เดิม
        Spline oldSpline = container.Splines[currentLaneIndex];
        // คำนวณ t ใหม่บน spline ใหม่ โดย sample ทีละจุด
        const int sampleCount = 100;
        float minDist = float.MaxValue;
        float nearestT = 0f;

        for (int i = 0; i <= sampleCount; i++)
        {
            float testT = i / (float)sampleCount;
            Vector3 pos = container.Splines[newLaneIndex].EvaluatePosition(testT);
            float dist = Vector3.Distance(container.Splines[currentLaneIndex].EvaluatePosition(t), pos);

            if (dist < minDist)
            {
                minDist = dist;
                nearestT = testT;
            }
        }

        t = nearestT;
        currentLaneIndex = newLaneIndex;
        lateralOffset = 0f;
    }


    void SnapLateralOffset()
    {
        lateralOffset = 0f;
    }

    void UpdateTransformPosition()
    {
        if (container == null || targetObject == null)
            return;

        Spline spline = container.Splines[currentLaneIndex];
        Vector3 pos = spline.EvaluatePosition(t);
        Vector3 tangent = spline.EvaluateTangent(t);
        Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;

        targetObject.transform.position = pos + lateralOffset * right;

        if (tangent != Vector3.zero)
            targetObject.transform.rotation = Quaternion.LookRotation(tangent);
    }

    void UpdateDebugText()
    {
        if (debugText == null) return;

        string label = currentLaneIndex switch
        {
            0 => "Center (Spline0)",
            1 => "Left (Spline1)",
            2 => "Right (Spline2)",
            _ => "Unknown"
        };

        debugText.text = $"{label} | t:{t:F2} | L:{lateralOffset:F2}";
    }
}
