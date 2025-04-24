using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;

public class SplineMovementAutoSpline : MonoBehaviour
{
    [Tooltip("รายการ SplineContainer ที่จะเชื่อมต่อกันแบบต่อเนื่อง")]
    public List<SplineContainer> splineChain = new List<SplineContainer>();

    [Tooltip("Object ที่จะเคลื่อนที่ตาม Spline")]
    public GameObject targetObject;
    [Tooltip("ความเร็วการเลื่อน t")]
    public float moveSpeed = 0.005f;
    [Tooltip("TextMesh Legacy สำหรับแสดงผล Spline และค่า t")]
    public TextMesh debugText;

    public float totalDistance;
    private float t = 0f;
    private int currentSplineIndex = 0;

    private float verticalInertia = 0f;
    private float horizontalInertia = 0f;
    private float lateralOffset = 0f;
    private bool wasHorizontal = false;
    private Vector2 previousTouchPosition;
    private float touchStartTime;
    private float inertiaDamping = 0.95f;
    private float inertiaThreshold = 0.001f;
    private float sq3 = Mathf.Sqrt(3f);
    private bool inputActive = false;

    void Start()
    {
        AutoDetectSplines();

        if (splineChain.Count > 0 && targetObject != null)
        {
            UpdateTransformPosition();
            UpdateDebugText();
        }

        float chainLength = GetTotalSplineChainLength();
        Debug.Log($"ระยะ spline chain รวมทั้งหมด = {chainLength} เมตร");
    }

    void AutoDetectSplines()
    {
        splineChain.Clear();
        SplineContainer[] allSplines = GameObject.FindObjectsOfType<SplineContainer>();
        foreach (var sc in allSplines)
        {
            if (sc.CompareTag("spline"))
            {
                splineChain.Add(sc);
            }
        }
    }

    void Update()
    {
        inputActive = false;

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

        UpdateTransformPosition();
        UpdateDebugText();
    }

    void MoveAlongSpline(float distanceDelta)
    {
        if (splineChain.Count == 0 || targetObject == null) return;

        Spline currentSpline = splineChain[currentSplineIndex].Spline;
        float newT;
        Vector3 newPosition = SplineUtility.GetPointAtLinearDistance(currentSpline, t, distanceDelta, out newT);

        if (newT >= 1f && currentSplineIndex < splineChain.Count - 1)
        {
            float overflow = newT - 1f;
            currentSplineIndex++;
            t = 0f;
            if (overflow > 0f) MoveAlongSpline(overflow);
        }
        else if (newT <= 0f && currentSplineIndex > 0)
        {
            float overflow = -newT;
            currentSplineIndex--;
            t = 1f;
            if (overflow > 0f) MoveAlongSpline(-overflow);
        }
        else
        {
            t = newT;
        }
    }

    float EstimateLength(Spline spline, float fromT, float toT, int steps = 2)
    {
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
        if (debugText != null && splineChain.Count > 0)
        {
            float lengthUpToNow = GetLengthUntil(currentSplineIndex, t);
            debugText.text = $"Spline {currentSplineIndex}: t = {t:F2} | Distance = {lengthUpToNow:F2} m";
        }
    }

    float GetLengthUntil(int splineIndex, float currentT)
    {
        float length = 0f;

        for (int i = 0; i < splineIndex; i++)
        {
            length += SplineUtility.CalculateLength(splineChain[i].Spline, 50);
        }

        float t = Mathf.Clamp01(currentT);
        if (t >= 1f)
        {
            length += SplineUtility.CalculateLength(splineChain[splineIndex].Spline, 50);
        }
        else if (t > 0f)
        {
            length += EstimateLength(splineChain[splineIndex].Spline, 0f, t);
        }

        return length;
    }

    float GetTotalSplineChainLength()
    {
        float totalLength = 0f;
        foreach (var splineContainer in splineChain)
        {
            if (splineContainer != null)
                totalLength += SplineUtility.CalculateLength(splineContainer.Spline, 50);
        }
        return totalLength;
    }
}