using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;

// คลาสที่ใช้สำหรับเชื่อมต่อ Spline หลายเส้น
[System.Serializable]
public class LinkedSpline
{
    public SplineContainer spline;
}

// ตัวหลักที่จัดการการเคลื่อนไหวของวัตถุบน Spline หลายเส้น
public class SplineGenerator : MonoBehaviour
{
    [Tooltip("รายการ SplineContainer ที่จะเชื่อมต่อกันแบบต่อเนื่อง")]
    public List<SplineContainer> splineChain; // รายการของเส้นทาง Spline

    [Tooltip("Object ที่จะเคลื่อนที่ตาม Spline")]
    public GameObject targetObject; // วัตถุที่จะเคลื่อนที่ไปตาม Spline

    [Tooltip("ความเร็วการเลื่อน t")]
    public float moveSpeed = 0.005f;

    [Tooltip("TextMesh Legacy สำหรับแสดงผล Spline และค่า t")]
    public TextMesh debugText;

    public float totalDistance; // ระยะทางรวมของ spline ทั้งหมด
    private float t = 0f; // ค่าตำแหน่งบน spline (0 ถึง 1)
    private int currentSplineIndex = 0; // index ของ spline ปัจจุบัน

    // ค่าที่ใช้ในการคำนวณ inertia และการเคลื่อนที่แบบต่อเนื่อง
    private float verticalInertia = 0f;
    private float horizontalInertia = 0f;
    private float lateralOffset = 0f; // ค่าการขยับเลนซ้ายขวา
    private bool wasHorizontal = false;
    private Vector2 previousTouchPosition;
    private float touchStartTime;
    private float inertiaDamping = 0.95f;
    private float inertiaThreshold = 0.001f;
    private float sq3 = Mathf.Sqrt(3f);
    private bool inputActive = false;

    void Start()
    {
        if (splineChain.Count > 0 && targetObject != null)
        {
            currentSplineIndex = 0;
            t = 0.5f; // ให้อยู่กลางเส้น Spline ตัวแรก
            lateralOffset = 0f; // ให้อยู่ตรงกลางเลน (ไม่เบี่ยงซ้าย/ขวา)
            UpdateTransformPosition(); // อัปเดตตำแหน่งเริ่มต้น
            UpdateDebugText();         // อัปเดตข้อความ debug
        }

        float chainLength = GetTotalSplineChainLength();
        Debug.Log($"ระยะ spline chain รวมทั้งหมด = {chainLength} เมตร");
    }


    void Update()
    {
        inputActive = false;

#if UNITY_EDITOR || UNITY_STANDALONE
        // รองรับการใช้งานเมาส์ใน editor หรือ standalone
        if (Input.GetMouseButton(0))
        {
            inputActive = true;
            float mouseDeltaY = Input.GetAxis("Mouse Y");
            float verticalDelta = -mouseDeltaY * moveSpeed;
            MoveAlongSpline(verticalDelta); // เคลื่อนที่แนวดิ่ง
            verticalInertia = verticalDelta / Time.deltaTime;
            wasHorizontal = false;
        }
        if (Input.GetMouseButtonUp(0))
        {
            touchStartTime = Time.time;
        }
#endif

        // รองรับการใช้งานบนมือถือ
        if (Input.touchCount > 0)
        {
            inputActive = true;
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    // เริ่มสัมผัสหน้าจอ
                    previousTouchPosition = touch.position;
                    touchStartTime = Time.time;
                    verticalInertia = 0f;
                    horizontalInertia = 0f;
                    wasHorizontal = false;
                    break;

                case TouchPhase.Moved:
                    // เคลื่อนไหวระหว่างสัมผัส
                    Vector2 currentTouchPosition = touch.position;
                    Vector2 delta = currentTouchPosition - previousTouchPosition;

                    if (Mathf.Abs(delta.y) > sq3 * Mathf.Abs(delta.x))
                    {
                        // ถ้าแนวตั้งชัดเจน → เลื่อนตาม spline
                        float verticalDelta = -delta.y * moveSpeed;
                        MoveAlongSpline(verticalDelta);
                        verticalInertia = verticalDelta / Time.deltaTime;
                        wasHorizontal = false;
                    }
                    else
                    {
                        // แนวนอน → ขยับเลน
                        float horizontalDelta = delta.x * moveSpeed;
                        lateralOffset += horizontalDelta;
                        lateralOffset = Mathf.Clamp(lateralOffset, -1f, 1f);
                        horizontalInertia = horizontalDelta / Time.deltaTime;
                        wasHorizontal = true;
                    }
                    previousTouchPosition = currentTouchPosition;
                    break;
            }
        }

        // ถ้าไม่มีการสัมผัส → คำนวณแรงเฉื่อย inertia
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

        // ตรวจสอบการเปลี่ยนเลนซ้ายขวา
        CheckLaneChangeByOffset();
        UpdateTransformPosition(); // อัปเดตตำแหน่งวัตถุ
        UpdateDebugText(); // อัปเดตข้อความแสดงสถานะ
    }

    void MoveAlongSpline(float distanceDelta)
    {
        // เลื่อนตำแหน่งวัตถุไปบน spline ตามระยะที่เปลี่ยน
        if (splineChain.Count == 0 || targetObject == null) return;

        Spline currentSpline = splineChain[currentSplineIndex].Spline;
        float newT;
        Vector3 newPosition = SplineUtility.GetPointAtLinearDistance(currentSpline, t, distanceDelta, out newT);

        t = newT;

        // เปลี่ยน spline หากสุดปลายเส้น
        if (t >= 1f && currentSplineIndex < splineChain.Count - 1)
        {
            Debug.Log("[Branch] Forward to next spline");
            currentSplineIndex++;
            t = 0.01f;
        }
        else if (t <= 0f && currentSplineIndex > 0)
        {
            Debug.Log("[Branch] Backward to previous spline");
            currentSplineIndex--;
            t = 0.99f;
        }
    }

    void CheckLaneChangeByOffset()
    {
        // ตรวจสอบการเปลี่ยนเลนเมื่อ lateralOffset ถึงค่ากำหนด
        float x = lateralOffset;
        Debug.Log($"[CheckLaneChange] index={currentSplineIndex}, t={t:F2}, offset={x:F2}");

        int leftIndex = currentSplineIndex - 1;
        int rightIndex = currentSplineIndex + 1;

        if (x <= -0.99f && leftIndex >= 0 && leftIndex < splineChain.Count)
        {
            // เปลี่ยนไปเลนซ้าย
            float currentLength = SplineUtility.CalculateLength(splineChain[currentSplineIndex].Spline, splineChain[currentSplineIndex].transform.localToWorldMatrix);
            float newDistance = t * currentLength;
            float newSplineLength = SplineUtility.CalculateLength(splineChain[leftIndex].Spline, splineChain[leftIndex].transform.localToWorldMatrix);
            float newT = Mathf.Clamp01(newDistance / newSplineLength);
            Debug.Log("[LaneChange] Jump left lane at t=" + newT);
            currentSplineIndex = leftIndex;
            t = newT;
            lateralOffset = 1f;
        }
        else if (x >= 0.99f && rightIndex >= 0 && rightIndex < splineChain.Count)
        {
            // เปลี่ยนไปเลนขวา
            float currentLength = SplineUtility.CalculateLength(splineChain[currentSplineIndex].Spline, splineChain[currentSplineIndex].transform.localToWorldMatrix);
            float newDistance = t * currentLength;
            float newSplineLength = SplineUtility.CalculateLength(splineChain[rightIndex].Spline, splineChain[rightIndex].transform.localToWorldMatrix);
            float newT = Mathf.Clamp01(newDistance / newSplineLength);
            Debug.Log("[LaneChange] Jump right lane at t=" + newT);
            currentSplineIndex = rightIndex;
            t = newT;
            lateralOffset = -1f;
        }
    }

    float EstimateLength(Spline spline, float fromT, float toT, int steps = 2)
    {
        // คำนวณระยะทางระหว่าง fromT ถึง toT ของ spline
        totalDistance = 0f;
        Vector3 prev = spline.EvaluatePosition(fromT);
        for (int i = 1; i <= steps; i++)
        {
            float t = Mathf.Lerp(fromT, toT, i / (float)steps);
            Vector3 current = spline.EvaluatePosition(t);
            totalDistance += Vector3.Distance(prev, current);
            prev = current;
        }
        return totalDistance;
    }

    void UpdateTransformPosition()
    {
        // อัปเดตตำแหน่งของวัตถุตาม spline และเลนที่เบี่ยง
        if (splineChain.Count == 0 || targetObject == null) return;

        Spline currentSpline = splineChain[currentSplineIndex].Spline;
        Vector3 splinePos = currentSpline.EvaluatePosition(t);
        Vector3 tangent = currentSpline.EvaluateTangent(t);
        Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
        targetObject.transform.position = splinePos + lateralOffset * right;
        if (tangent != Vector3.zero)
        {
            targetObject.transform.rotation = Quaternion.LookRotation(tangent);
        }
    }

    void UpdateDebugText()
    {
        // แสดงข้อความ debug แสดงตำแหน่ง spline และค่า t
        if (debugText != null && splineChain.Count > 0)
        {
            float lengthUpToNow = GetLengthUntil(currentSplineIndex, t);
            debugText.text = $"Spline {currentSplineIndex}: t = {t:F2} | Distance = {lengthUpToNow:F2} m";
        }
    }

    float GetLengthUntil(int splineIndex, float currentT)
    {
        // คำนวณระยะรวมจนถึงตำแหน่ง t ของ spline ปัจจุบัน
        float length = 0f;

        for (int i = 0; i < splineIndex; i++)
        {
            length += SplineUtility.CalculateLength(splineChain[i].Spline, splineChain[i].transform.localToWorldMatrix);
        }

        float t = Mathf.Clamp01(currentT);
        if (t >= 1f)
        {
            length += SplineUtility.CalculateLength(splineChain[splineIndex].Spline, splineChain[splineIndex].transform.localToWorldMatrix);
        }
        else if (t > 0f)
        {
            length += EstimateLength(splineChain[splineIndex].Spline, 0f, t);
        }

        return length;
    }

    float GetTotalSplineChainLength()
    {
        // คำนวณระยะรวมของ spline ทั้งหมดใน chain
        float totalLength = 0f;
        foreach (var splineContainer in splineChain)
        {
            if (splineContainer != null)
                totalLength += SplineUtility.CalculateLength(splineContainer.Spline, splineContainer.transform.localToWorldMatrix);
        }
        return totalLength;
    }
}
