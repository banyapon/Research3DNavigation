using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;

[System.Serializable]
public class SplineIdeaB : MonoBehaviour
{
    [Tooltip("รายการ Spline ที่มีโครงสร้างเชื่อมโยง (ถนน)")]
    public List<mYSplineContainer> roadList = new List<mYSplineContainer>();

    [Tooltip("Object ที่จะเคลื่อนที่ตาม Spline")]
    public GameObject targetObject;

    [Tooltip("TextMesh สำหรับ debug")]
    public TextMesh debugText;

    public float moveSpeed = 0.005f;

    private float t = 0.5f;
    private int currentRoadId = 0;
    private float lateralOffset = 0f;

    void Start()
    {
        // ค้นหา GameObject ทั้งหมดที่มี Tag = "spline"
        GameObject[] taggedSplines = GameObject.FindGameObjectsWithTag("spline");

        foreach (GameObject splineGO in taggedSplines)
        {
            var splineComp = splineGO.GetComponent<SplineContainer>();
            if (splineComp != null)
            {
                GameObject holder = new GameObject("RuntimeSplineHolder_" + splineGO.name);
                var mySpline = holder.AddComponent<mYSplineContainer>();
                mySpline.splineContainer = splineComp;
                roadList.Add(mySpline);

                Debug.Log($"[AutoMap] ผูก {splineGO.name} → roadList[{roadList.Count - 1}]");
            }
        }

        if (roadList.Count == 0)
        {
            Debug.LogWarning("[AutoMap] ไม่พบ GameObject ที่มี Tag = spline และมี SplineContainer");
            return;
        }

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
        if (t >= 1f)
        {
            TryFollowChildEnd(currentRoadId);
        }
        else if (t <= 0.0f)
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
            t = 0.01f;
        }
        else
        {
            t = 1f;
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
