using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;

public class SplineMovementController : MonoBehaviour
{
    [Header("ชุด Splines")]
    [Tooltip("ลาก SplineContainer ทั้งหมดในลำดับเชนแบบที่ต้องการเชื่อมต่อ")]
    public List<SplineContainer> splineChain;

    [Header("Branch Indices")]
    [Tooltip("สำหรับแต่ละ spline: เมื่อ lateralOffset < 0 ให้ไป splineChain[index] ที่นี่")]
    public List<int> leftBranch;
    [Tooltip("สำหรับแต่ละ spline: เมื่อ lateralOffset ≥ 0 ให้ไป splineChain[index] ที่นี่")]
    public List<int> rightBranch;

    [Header("เคลื่อนที่ & Inertia")]
    public GameObject targetObject;
    public float moveSpeed = 0.005f;
    public float inertiaDamping = 0.95f;
    public float inertiaThreshold = 0.001f;
    private float lateralOffset;
    private float verticalInertia, horizontalInertia;
    private bool inputActive;
    private Vector2 prevTouch;
    private const float SQ3 = 1.73205f;

    [Header("ตำแหน่งบน Spline")]
    [Range(0f, 1f)] public float t = 0f;
    public int currentSplineIndex = 0;

    [Header("ระยะทางที่เดินทาง (เมตร)")]
    public float traveledDistance = 0f;  // ถ้าต้องการแสดงใน Inspector

    [Header("Debug (ถ้ามี)")]
    public TextMesh debugText;

    // กำหนด threshold ว่าเข้าปลาย spline เมื่อ t ≥ 1 - eps
    private const float BRANCH_THRESHOLD = 0.98f;

    void Update()
    {
        HandleInput();
        ApplyInertiaWhenIdle();
        MoveAndBranch();
        UpdateTransform();
        UpdateDebug();
    }

    void HandleInput()
    {
        inputActive = false;
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButton(0))
        {
            inputActive = true;
            Vector2 d = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            ProcessSwipe(d);
        }
#endif
        if (Input.touchCount > 0)
        {
            var tc = Input.GetTouch(0);
            if (tc.phase == TouchPhase.Began) prevTouch = tc.position;
            if (tc.phase == TouchPhase.Moved)
            {
                inputActive = true;
                Vector2 d = tc.position - prevTouch;
                prevTouch = tc.position;
                ProcessSwipe(d);
            }
        }
    }

    void ProcessSwipe(Vector2 delta)
    {
        bool vert = Mathf.Abs(delta.y) > SQ3 * Mathf.Abs(delta.x);
        if (vert)
        {
            float dT = -delta.y * moveSpeed;
            t = Mathf.Clamp01(t + dT);
            verticalInertia = dT / Time.deltaTime;
        }
        else
        {
            float dO = delta.x * moveSpeed;
            lateralOffset = Mathf.Clamp(lateralOffset + dO, -1f, 1f);
            horizontalInertia = dO / Time.deltaTime;
        }
    }

    void ApplyInertiaWhenIdle()
    {
        if (inputActive) return;

        // แนวดิ่ง
        if (Mathf.Abs(verticalInertia) > inertiaThreshold)
        {
            t = Mathf.Clamp01(t + verticalInertia * Time.deltaTime);
            verticalInertia *= inertiaDamping;
        }
        else verticalInertia = 0f;

        // แนวนอน
        if (Mathf.Abs(horizontalInertia) > inertiaThreshold)
        {
            lateralOffset = Mathf.Clamp(lateralOffset + horizontalInertia * Time.deltaTime, -1f, 1f);
            horizontalInertia *= inertiaDamping;
        }
        else horizontalInertia = 0f;
    }

    void MoveAndBranch()
    {
        // เดินตาม spline ปัจจุบัน
        var spline = splineChain[currentSplineIndex];
        Vector3 prevPos = spline.Spline.EvaluatePosition(t);
        Vector3 newPos = spline.Spline.EvaluatePosition(t);
        traveledDistance += Vector3.Distance(prevPos, newPos);

        // ถ้าถึงปลาย spline
        if (t >= BRANCH_THRESHOLD)
        {
            int nextIndex = currentSplineIndex;

            // --- เงื่อนไขสลับเส้นตามตำแหน่งแนวนอน ---
            float x = lateralOffset;        // -1..+1
            switch (currentSplineIndex)
            {
                case 1: // จาก Spline(1) → ถ้าชิดขวาไป Spline(2)
                    if (x > 0.5f) nextIndex = 2;
                    break;

                case 2: // บน Spline(2)
                    if (Mathf.Abs(x) < 0.3f)
                        nextIndex = 3;        // ตรงกลาง → Spline(3)
                    else if (x < -0.5f)
                        nextIndex = 4;        // ชิดซ้าย → เลี้ยวซ้ายสุด (Spline(4))
                    break;

                    // เพิ่มกรณีอื่นๆ ตามต้องการ
            }

            // ถ้าเลือกเส้นใหม่ได้ ให้กระโดดไปและรีเซ็ต t
            if (nextIndex != currentSplineIndex
             && nextIndex >= 0 && nextIndex < splineChain.Count)
            {
                currentSplineIndex = nextIndex;
                t = 0f;
            }
        }
    }


    void UpdateTransform()
    {
        var spline = splineChain[currentSplineIndex].Spline;
        Vector3 pos = spline.EvaluatePosition(t);
        Vector3 tan = spline.EvaluateTangent(t);
        Vector3 right = Vector3.Cross(Vector3.up, tan).normalized;
        targetObject.transform.position = pos + lateralOffset * right;
        if (tan != Vector3.zero)
            targetObject.transform.rotation = Quaternion.LookRotation(tan);
    }

    void UpdateDebug()
    {
        if (debugText != null)
            debugText.text = $"Spline:{currentSplineIndex} t={t:F2}\nDist={traveledDistance:F1}m Off={lateralOffset:F2}";
    }
}
