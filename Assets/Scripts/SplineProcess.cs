using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;

public class SplineProcess : MonoBehaviour
{
    [Tooltip("รายการ SplineContainer ที่จะเชื่อมต่อกันแบบต่อเนื่อง")]
    public List<SplineContainer> splineChain;

    [Tooltip("Object ที่จะเคลื่อนที่ตาม Spline")]
    public GameObject targetObject;
    [Tooltip("TextMesh Legacy สำหรับแสดงผล Spline และค่า t")]
    public TextMesh debugText;
    [Tooltip("ความเร็วจาก Touch drag (เมตร/พิกเซล)")]
    public float moveScale = 0.01f;

    private List<float> splineLengths = new();
    private List<Spline> allSplines = new();
    private float totalLength;
    private float walkedDistance = 0f;
    private float normalizedT = 0f;

    private Vector2 previousTouchPosition;
    private float verticalInertia = 0f;
    private float inertiaDamping = 0.95f;
    private float inertiaThreshold = 0.001f;
    private bool inputActive = false;
    private float sq3 = Mathf.Sqrt(3f);

    void Start()
    {
        CalculateSplineLengths();
    }

    void Update()
    {
        inputActive = false;

#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0))
            previousTouchPosition = Input.mousePosition;

        if (Input.GetMouseButton(0))
        {
            Vector2 current = Input.mousePosition;
            float deltaY = current.y - previousTouchPosition.y;
            float deltaDist = -deltaY * moveScale;
            walkedDistance += deltaDist;
            previousTouchPosition = current;
            verticalInertia = deltaDist / Time.deltaTime;
            inputActive = true;
        }
#endif

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                previousTouchPosition = touch.position;
            }
            else if (touch.phase == TouchPhase.Moved)
            {
                Vector2 delta = touch.position - previousTouchPosition;
                float deltaY = delta.y;
                float deltaDist = -deltaY * moveScale;
                walkedDistance += deltaDist;
                previousTouchPosition = touch.position;
                verticalInertia = deltaDist / Time.deltaTime;
                inputActive = true;
            }
        }

        if (!inputActive)
        {
            if (Mathf.Abs(verticalInertia) > inertiaThreshold)
            {
                walkedDistance += verticalInertia * Time.deltaTime;
                verticalInertia *= inertiaDamping;
            }
            else
            {
                verticalInertia = 0f;
            }
        }

        walkedDistance = Mathf.Clamp(walkedDistance, 0f, totalLength);
        normalizedT = walkedDistance / totalLength;

        Vector3 pos = GetPositionAtDistance(walkedDistance);
        targetObject.transform.position = pos;

        if (debugText)
        {
            debugText.text = $"Distance: {walkedDistance:F2} m | t: {normalizedT:F3}";
        }
    }

    void CalculateSplineLengths()
    {
        splineLengths.Clear();
        allSplines.Clear();
        totalLength = 0f;

        foreach (var container in splineChain)
        {
            if (container == null) continue;

            foreach (var spline in container.Splines)
            {
                float len = SplineUtility.CalculateLength(spline, 50);
                splineLengths.Add(len);
                allSplines.Add(spline);
                totalLength += len;
            }
        }
    }

    Vector3 GetPositionAtDistance(float distance)
    {
        float accumulated = 0f;

        for (int i = 0; i < allSplines.Count; i++)
        {
            float len = splineLengths[i];
            if (distance <= accumulated + len)
            {
                float localDistance = distance - accumulated;
                return SplineUtility.GetPointAtLinearDistance(allSplines[i], 0f, localDistance, out _);
            }
            accumulated += len;
        }

        return allSplines[^1].EvaluatePosition(1f);
    }

    public void SetNormalizedDistance(float value)
    {
        normalizedT = Mathf.Clamp01(value);
        walkedDistance = normalizedT * totalLength;
    }

    public float GetNormalizedT()
    {
        return normalizedT;
    }
}
