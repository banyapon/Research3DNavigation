using UnityEngine;
using UnityEngine.Splines;
using UnityEditor;

[ExecuteInEditMode]
public class SplineProcess : MonoBehaviour
{
    [Header("Spline Chain Settings")]
    [Tooltip("จำนวนเส้น Spline ที่จะสร้าง")] public int numSplines = 4;
    [Tooltip("ระยะห่างระหว่างจุดต้น-ปลายของแต่ละ Spline")] public float segmentLength = 5f;
    [Tooltip("ความกว้างซ้ายขวาของจุดปลายสลับ")] public float lateralOffset = 2f;
    [Tooltip("สร้างใหม่ทุกครั้งเมื่อค่าถูกเปลี่ยน")] public bool regenerate = false;

    private void Update()
    {
        if (!Application.isPlaying && regenerate)
        {
            regenerate = false;
            GenerateSplines();
        }
    }

    void GenerateSplines()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        Vector3 startPoint = transform.position;
        Vector3 direction = Vector3.forward * segmentLength;

        for (int i = 0; i < numSplines; i++)
        {
            float lateral = (i % 2 == 0 ? -1 : 1) * lateralOffset;

            Vector3 p0 = startPoint;
            Vector3 p1 = startPoint + direction + Vector3.right * lateral;

            GameObject go = new GameObject($"Spline_{i}", typeof(SplineContainer), typeof(SplineExtrude));
            go.transform.parent = transform;
            go.tag = "spline";

            var container = go.GetComponent<SplineContainer>();
            Spline spline = new Spline();
            spline.Add(new BezierKnot(p0));
            spline.Add(new BezierKnot(p1));
            container.Spline = spline;

            startPoint = p1;
        }

        GameObject goFinal = new GameObject($"Spline_{numSplines}_Loop", typeof(SplineContainer), typeof(SplineExtrude));
        goFinal.transform.parent = transform;
        goFinal.tag = "spline";

        Vector3 lastPoint = startPoint;
        Vector3 backToStart = transform.position;
        Spline loopSpline = new Spline();
        loopSpline.Add(new BezierKnot(lastPoint));
        loopSpline.Add(new BezierKnot(backToStart));
        goFinal.GetComponent<SplineContainer>().Spline = loopSpline;

        Debug.Log($"Generated {numSplines + 1} spline segments including loop.");
    }
}
