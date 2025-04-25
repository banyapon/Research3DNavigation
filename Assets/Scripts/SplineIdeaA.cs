using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;

[System.Serializable]
public class SplineIdeaA : MonoBehaviour
{
    [Tooltip("รายการ Spline ที่มีโครงสร้างเชื่อมโยง (ถนน)")]
    public List<mYSplineContainer> roadList;

    [Tooltip("Object ที่จะเคลื่อนที่ตาม Spline")]
    public GameObject targetObject;

    [Tooltip("TextMesh สำหรับ debug")]
    public TextMesh debugText;

    public float moveSpeed = 0.005f;

    private float t = 0.5f; // เริ่มกลางเส้น
    private int currentRoadId = 0;
    private float lateralOffset = 0f;
    public GameObject splineLine;

    void Start()
    {
        // ตรวจสอบว่ามี roadList หรือยัง
        if (roadList.Count == 0)
        {
            GameObject go = new GameObject("AutoCreatedSpline");
            var container = go.AddComponent<mYSplineContainer>(); //ใช้ AddComponent แทน new
            roadList.Add(container);
        }

        // ค้นหา spline1 แล้วผูกเข้า splineContainer
        splineLine = GameObject.Find("spline1");
        if (splineLine != null)
        {
            var splineComp = splineLine.GetComponent<SplineContainer>(); // ไม่ใช่ mYSplineContainer
            if (splineComp != null)
            {
                roadList[0].splineContainer = splineComp;
                Debug.Log("[Init] ผูก spline1 เข้ากับ roadList[0] เรียบร้อย");
            }
            else
            {
                Debug.LogWarning("[Init] 'spline1' ไม่มี SplineContainer");
            }
        }
        else
        {
            Debug.LogWarning("[Init] ไม่พบ GameObject ชื่อ 'spline1'");
        }

        // ตั้งค่าตำแหน่งเริ่มต้น
        if (targetObject != null)
        {
            t = 0.5f;
            currentRoadId = 0;
            lateralOffset = 0f;

            UpdateTransformPosition();
            UpdateDebugText();
        }
    }


    void Update()
    {
        float delta = Input.GetAxis("Vertical") * moveSpeed;

        if (Mathf.Abs(delta) > 0.0001f)
        {
            MoveAlongSpline(delta);
        }

        UpdateTransformPosition();
        UpdateDebugText();
    }

    void MoveAlongSpline(float delta)
    {
        if (roadList.Count == 0) return;

        t += delta;
        if(t >= 1f)
        {
            TryFollowChildEnd(currentRoadId);
        }
        else if(t <= 0.0f)
        {
            TryFollowChildStart(currentRoadId);
        }
    }

    void TryFollowChildEnd(int roadId)
    {
        var road = roadList[roadId];
        if (road.childNodeE != null && road.childNodeE.Count > 0)
        {
            float encoded = road.childNodeE[0];
            int nextRoadId = Mathf.FloorToInt(encoded);
            int nodeIndex = Mathf.RoundToInt((encoded - Mathf.Floor(encoded)) * 10);

            Debug.Log($"[SwitchRoad] ต่อจากปลายทาง {roadId} → {nextRoadId} ที่ node {nodeIndex}");

            currentRoadId = nextRoadId;
            t = 0.01f; // เริ่มต้นนิดหน่อยใน spline ถัดไป
        }
        else
        {
            t = 1f; // ค้างไว้ที่ปลายเส้น
        }
    }

    void TryFollowChildStart(int roadId)
    {
        var road = roadList[roadId];
        if (road.childNodeS != null && road.childNodeS.Count > 0)
        {
            float encoded = road.childNodeS[0];
            int nextRoadId = Mathf.FloorToInt(encoded);
            int nodeIndex = Mathf.RoundToInt((encoded - Mathf.Floor(encoded)) * 10);

            Debug.Log($"[SwitchRoad] ต่อจากจุดเริ่ม {roadId} → {nextRoadId} ที่ node {nodeIndex}");

            currentRoadId = nextRoadId;
            t = 0.99f;
        }
        else
        {
            t = 0f;
        }
    }

    void UpdateTransformPosition()
    {
        if (roadList.Count == 0 || targetObject == null) return;

        var current = roadList[currentRoadId];
        if (current.splineContainer == null)
        {
            Debug.LogWarning($"SplineContainer ของ roadList[{currentRoadId}] ยังไม่ถูกกำหนด");
            return;
        }

        var spline = current.splineContainer.Spline;
        Vector3 pos = spline.EvaluatePosition(t);
        Vector3 tangent = spline.EvaluateTangent(t);
        Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;

        targetObject.transform.position = pos + lateralOffset * right;

        if (tangent != Vector3.zero)
            targetObject.transform.rotation = Quaternion.LookRotation(tangent);
    }


    void UpdateDebugText()
    {
        if (debugText != null)
        {
            debugText.text = $"Road {currentRoadId} | t = {t:F2}";
        }
    }
}
