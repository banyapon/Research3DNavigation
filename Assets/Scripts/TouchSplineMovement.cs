using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Splines;

public class TouchSplineMovement : MonoBehaviour
{
    [Tooltip("Container ของ Spline")]
    public SplineContainer splineContainer;

    [Tooltip("Object ที่จะเคลื่อนที่ตาม Spline")]
    public GameObject targetObject;

    [Tooltip("ค่าความเร็วในการเลื่อน t (ปรับให้เหมาะกับ touch)")]
    public float moveSpeed = 0.005f;

    [Tooltip("ค่าปัจจุบันบน Spline (0-1)")]
    private float t = 0f;

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
        // สำหรับรับค่า touch input
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            // เมื่อมีการเคลื่อนที่ (Moved) ของนิ้ว
            if (touch.phase == TouchPhase.Moved)
            {
                // คำนวณระยะการเคลื่อนที่ในแนวตั้ง (Y)
                // หากเลื่อนจากบนลงล่าง (delta.y น้อย) จะได้ค่าลบ => -(-delta) = +distance => ไปข้างหน้า
                // หากเลื่อนจากล่างขึ้นบน (delta.y บวก) จะได้ค่าลบ => -(+delta) = -distance => ถอยหลัง
                float distanceDelta = -touch.deltaPosition.y * moveSpeed;
                MoveAlongSpline(distanceDelta);
            }
            // เมื่อยกนิ้วออก (Ended) ให้แสดงค่าใน labelLog
            else if (touch.phase == TouchPhase.Ended)
            {
                UpdateLabelLog();
            }
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        // สำหรับทดสอบใน Editor ด้วย Mouse
        if (Input.GetMouseButton(0))
        {
            // จำลองการใช้ Mouse เลื่อนขึ้นลง แทนการ Touch กรณีไม่มี Touch Screen
            float mouseDelta = Input.GetAxis("Mouse Y");
            float distanceDelta = -mouseDelta * moveSpeed;
            MoveAlongSpline(distanceDelta);
        }
        // เมื่อปล่อย Mouse (คล้าย Touch Ended)
        if (Input.GetMouseButtonUp(0))
        {
            UpdateLabelLog();
        }
#endif
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
