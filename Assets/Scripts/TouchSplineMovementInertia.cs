using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Splines;

public class TouchSplineMovementInertia : MonoBehaviour
{
    public enum Lane { Left, Center, Right } //ผมเพิ่มตัวแปรเงื่อนไขเก็บค่า 3 เลน
    private Lane currentLane = Lane.Center;  // เริ่มต้นที่เลนกลาง

    [Tooltip("Spline สำหรับเลนกลาง")]
    //เปลี่ยนตัวแปร
    public SplineContainer splineCenter;

    [Tooltip("Spline สำหรับเลนซ้าย")]
    public SplineContainer splineLeft;

    [Tooltip("Spline สำหรับเลนขวา")]
    public SplineContainer splineRight;

    // ใช้ SplineContainer ปัจจุบัน
    private SplineContainer currentSpline;

    [Tooltip("Object ที่จะเคลื่อนที่ตาม Spline")]
    public GameObject targetObject;

    [Tooltip("ค่าความเร็วในการเลื่อน t (ปรับให้เหมาะกับ touch)")]
    public float moveSpeed = 0.005f;

    [Tooltip("ค่าปัจจุบันบน Spline (0-1)")]
    public float t = 0f;

    [Tooltip("Damping factor สำหรับ Inertia (0-1), ค่าน้อยจะลดความเร็วได้ไว")]
    public float inertiaDamping = 0.95f;

    [Tooltip("Threshold สำหรับหยุด Inertia")]
    public float inertiaThreshold = 0.001f;

    // Inertia สำหรับการเคลื่อนที่ตาม spline (vertical)
    private float verticalInertia = 0f;
    // Inertia สำหรับการเคลื่อนที่แนวนอน (lateral)
    private float horizontalInertia = 0f;

    // ค่า lateralOffset (แกน x) ที่ใช้เป็นการ offset จากจุดตรงกลางของ spline
    private float lateralOffset = 0f;

    public Text labelLog, labelSpline;

    // สำหรับการคำนวณทัชแนวนอนและแนวตั้ง
    private bool wasHorizontal = false;
    private Vector2 previousTouchPosition;
    private float touchStartTime;

    // ค่าคงที่สำหรับเปรียบเทียบแนวตั้ง vs แนวนอน (√3)
    private float sq3 = Mathf.Sqrt(3f);

    void Start()
    {
        labelLog.text = "";
        labelSpline.text = "Lane:" + currentLane.ToString();
        currentSpline = splineCenter; // เริ่มต้นที่เลนกลาง
        if (currentSpline != null && targetObject != null)
        {
            UpdateTransformPosition();
        }
    }

    void Update()
    {
        bool inputActive = false;

#if UNITY_EDITOR || UNITY_STANDALONE
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

                    if (Mathf.Abs(delta.y) > sq3 * Mathf.Abs(delta.x))
                    {
                        float verticalDelta = -delta.y * moveSpeed;
                        MoveAlongSpline(verticalDelta);
                        verticalInertia = verticalDelta / Time.deltaTime;
                        wasHorizontal = false;
                    }
                    else
                    {
                        float horizontalDelta = delta.x * moveSpeed;
                        lateralOffset += horizontalDelta;
                        lateralOffset = Mathf.Clamp(lateralOffset, -1f, 1f);
                        horizontalInertia = horizontalDelta / Time.deltaTime;
                        wasHorizontal = true;
                    }
                    previousTouchPosition = currentTouchPosition;
                    break;

                case TouchPhase.Ended:
                    // ไม่ต้องรีเซ็ต lateralOffset ที่นี่
                    break;
            }
        }

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

        // ===== Hysteresis Logic =====
        //การทำงานของ Hysteresis เมื่อค่าอินพุตเข้าสู่เกณฑ์เพื่อเปลี่ยนสถานะ
        //ระบบจะไม่เปลี่ยนสถานะกลับทันทีหากค่าอินพุตเล็กน้อยแปรปรวนกลับมาในช่วงขอบเขตเกณฑ์เดิม
        //ต้องการป้องกันการสลับเลนหรือการกระโดดเปลี่ยนสถานะบ่อย ๆ เมื่อค่าอินพุต (เช่น lateralOffset) อยู่ใกล้เกณฑ์ 0.6
        switch (currentLane)
        {
            case Lane.Center:
                // หลังจากคำนวณ lateralOffset และ inertia เสร็จในแต่ละเฟรม
                if (lateralOffset <= -0.6f && currentSpline != splineLeft && IsSplineCloseEnough(currentSpline, splineLeft))
                {
                    currentLane = Lane.Left;
                    SwitchToSpline(splineLeft);
                }
                else if (lateralOffset >= 0.6f && currentSpline != splineRight && IsSplineCloseEnough(currentSpline, splineRight))
                {
                    currentLane = Lane.Right;
                    SwitchToSpline(splineRight);
                }
                labelSpline.text = "Lane:" + currentLane;
                break;
            case Lane.Left:
                if (lateralOffset > -0.3f)
                {
                    SwitchToSpline(splineCenter);
                    currentLane = Lane.Center;
                }
                break;
            case Lane.Right:
                if (lateralOffset < 0.3f)
                {
                    SwitchToSpline(splineCenter);
                    currentLane = Lane.Center;
                }
                break;
        }

        Debug.Log("Distance to Left: " + Mathf.Abs(currentSpline.Spline.EvaluatePosition(t).x - splineLeft.Spline.EvaluatePosition(t).x));
        Debug.Log("Distance to Right: " + Mathf.Abs(currentSpline.Spline.EvaluatePosition(t).x - splineRight.Spline.EvaluatePosition(t).x));

        labelSpline.text = "Lane:" + currentLane;
        UpdateTransformPosition();
        UpdateLabelLog();
    }

    bool IsSplineCloseEnough(SplineContainer from, SplineContainer to)
    {
        if (from == null || to == null) return false;

        Vector3 fromPos = from.Spline.EvaluatePosition(t);
        Vector3 toPos = to.Spline.EvaluatePosition(t);
        float horizontalDistance = Mathf.Abs(fromPos.x - toPos.x); // เฉพาะแกน X

        return horizontalDistance <= 0.7f; // ค่า threshold ที่คุณต้องการ
    }


    void MoveAlongSpline(float distanceDelta)
    {
        if (currentSpline != null && targetObject != null)
        {
            Spline spline = currentSpline.Spline;
            float newT;
            Vector3 newPosition = SplineUtility.GetPointAtLinearDistance(spline, t, distanceDelta, out newT);
            t = newT;
        }
    }

    void UpdateTransformPosition()
    {
        if (currentSpline != null && targetObject != null)
        {
            Spline spline = currentSpline.Spline;
            Vector3 splineCenterPos = spline.EvaluatePosition(t);
            Vector3 tangent = spline.EvaluateTangent(t);
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            targetObject.transform.position = splineCenterPos + lateralOffset * right;
            if (tangent != Vector3.zero)
            {
                targetObject.transform.rotation = Quaternion.LookRotation(tangent);
            }
        }
    }

    void SwitchToSpline(SplineContainer newSpline)
    {
        if (newSpline == null) return;
        currentSpline = newSpline;
        lateralOffset = 0f; // รีเซ็ต lateralOffset ให้กลับเป็นศูนย์ -> อยู่กึ่งกลางเลน
        UpdateTransformPosition();
    }


    void UpdateLabelLog()
    {
        if (targetObject != null && labelLog != null)
        {
            Vector3 pos = targetObject.transform.position;
            Vector3 rot = targetObject.transform.rotation.eulerAngles;
            labelLog.text = string.Format(
                "Position: ({0:F2}, {1:F2}, {2:F2})\nRotation: ({3:F2}, {4:F2}, {5:F2})\nSpline Pos: {6:F2}\nLateral Offset: {7:F2}",
                pos.x, pos.y, pos.z,
                rot.x, rot.y, rot.z,
                t, lateralOffset
            );
        }
    }
}