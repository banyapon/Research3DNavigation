using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Splines;

public class TouchSplineMovementInertia : MonoBehaviour
{
    [Tooltip("Container ของ Spline")]
    public SplineContainer splineContainer;

    [Tooltip("Object ที่จะเคลื่อนที่ตาม Spline")]
    public GameObject targetObject;

    [Tooltip("ค่าความเร็วในการเลื่อน t (ปรับให้เหมาะกับ touch)")]
    public float moveSpeed = 0.005f;

    [Tooltip("ค่าปัจจุบันบน Spline (0-1)")]
    private float t = 0f;

    [Tooltip("Damping factor สำหรับ Inertia (0-1), ค่าน้อยจะลดความเร็วได้ไว")]
    public float inertiaDamping = 0.95f;

    [Tooltip("Threshold สำหรับหยุด Inertia")]
    public float inertiaThreshold = 0.001f;

    [Tooltip("ความเร็วของ Inertia (หน่วย: ระยะทาง/วินาที)")]
    private float inertiaVelocity = 0f;

    public Text labelLog;

    void Start()
    {
        labelLog.text = "";
        if (splineContainer != null && targetObject != null)
        {
            Spline spline = splineContainer.Spline;
            // กำหนดตำแหน่งเริ่มต้นที่ t = 0
            Vector3 startPosition = spline.EvaluatePosition(t);
            targetObject.transform.position = startPosition;
            Vector3 tangent = spline.EvaluateTangent(t);
            if (tangent != Vector3.zero)
                targetObject.transform.rotation = Quaternion.LookRotation(tangent);
        }
    }

    void Update()
    {
        bool inputActive = false;

#if UNITY_EDITOR || UNITY_STANDALONE
        // สำหรับทดสอบใน Editor ด้วย Mouse
        if (Input.GetMouseButton(0))
        {
            inputActive = true;
            float mouseDelta = Input.GetAxis("Mouse Y");
            float distanceDelta = -mouseDelta * moveSpeed;
            MoveAlongSpline(distanceDelta);
            // คำนวณ inertia velocity จากการเปลี่ยนแปลงล่าสุด
            inertiaVelocity = distanceDelta / Time.deltaTime;
        }
        if (Input.GetMouseButtonUp(0))
        {
            UpdateLabelLog();
        }
#endif

        // สำหรับรับค่า touch input บนอุปกรณ์มือถือ
        if (Input.touchCount > 0)
        {
            inputActive = true;
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Moved)
            {
                float distanceDelta = -touch.deltaPosition.y * moveSpeed;
                MoveAlongSpline(distanceDelta);
                // คำนวณ inertia velocity จาก touch delta
                inertiaVelocity = distanceDelta / Time.deltaTime;
            }
            else if (touch.phase == TouchPhase.Ended)
            {
                UpdateLabelLog();
            }
        }

        // เมื่อไม่มี input ให้ใช้ค่า inertia เพื่อเคลื่อนที่ต่อ
        if (!inputActive)
        {
            if (Mathf.Abs(inertiaVelocity) > inertiaThreshold)
            {
                float inertiaDistanceDelta = inertiaVelocity * Time.deltaTime;
                MoveAlongSpline(inertiaDistanceDelta);
                inertiaVelocity *= inertiaDamping;
            }
            else
            {
                inertiaVelocity = 0f;
            }
        }
    }

    // ฟังก์ชันเคลื่อนที่ตาม Spline ด้วยระยะที่กำหนด
    void MoveAlongSpline(float distanceDelta)
    {
        if (splineContainer != null && targetObject != null)
        {
            Spline spline = splineContainer.Spline;
            float newT;
            // คำนวณตำแหน่งใหม่บน Spline โดยเริ่มจากค่า t ปัจจุบันและเลื่อนตามระยะ distanceDelta
            Vector3 newPosition = SplineUtility.GetPointAtLinearDistance(spline, t, distanceDelta, out newT);
            targetObject.transform.position = newPosition;
            // อัพเดทการหมุนให้ Object หันตาม tangent ของ Spline
            Vector3 tangent = spline.EvaluateTangent(newT);
            if (tangent != Vector3.zero)
                targetObject.transform.rotation = Quaternion.LookRotation(tangent);
            // อัพเดทค่า t สำหรับการเคลื่อนที่ในครั้งถัดไป
            t = newT;
        }
    }

    // ฟังก์ชันอัปเดต labelLog ให้แสดงตำแหน่ง, การหมุน และตำแหน่งบน Spline
    void UpdateLabelLog()
    {
        if (targetObject != null && labelLog != null)
        {
            Vector3 pos = targetObject.transform.position;
            Vector3 rot = targetObject.transform.rotation.eulerAngles;
            labelLog.text = string.Format("Position: ({0:F2}, {1:F2}, {2:F2})\nRotation: ({3:F2}, {4:F2}, {5:F2})\nSpline Pos: {6:F2}",
                                          pos.x, pos.y, pos.z, rot.x, rot.y, rot.z, t);
        }
    }
}
