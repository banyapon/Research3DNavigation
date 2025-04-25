using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;
using System.Xml.Linq;

public class SplineAutoLinker
{
    [MenuItem("Tools/Spline/Auto Link Graph")]
    public static void AutoLinkAllSplines()
    {
        float maxDistance = 2.0f; // ระยะห่างสูงสุดที่ถือว่า "เชื่อมกัน"
        var allSplines = GameObject.FindObjectsOfType<mYSplineContainer>();
        int linkCount = 0;

        foreach (var splineA in allSplines)
        {
            if (splineA == null || splineA.splineContainer == null) continue;

            splineA.parentNodeS = new List<float>();
            splineA.childNodeS = new List<float>();
            splineA.parentNodeE = new List<float>();
            splineA.childNodeE = new List<float>();

            Vector3 startA = splineA.splineContainer.Spline.EvaluatePosition(0f);
            Vector3 endA = splineA.splineContainer.Spline.EvaluatePosition(1f);

            foreach (var splineB in allSplines)
            {
                if (splineB == null || splineB == splineA || splineB.splineContainer == null) continue;

                Vector3 startB = splineB.splineContainer.Spline.EvaluatePosition(0f);
                Vector3 endB = splineB.splineContainer.Spline.EvaluatePosition(1f);

                if (Vector3.Distance(endA, startB) <= maxDistance)
                {
                    splineA.childNodeE.Add(splineB.id + 0.0f);
                    splineB.parentNodeS.Add(splineA.id + 1.0f);
                    linkCount++;
                }

                if (Vector3.Distance(startA, endB) <= maxDistance)
                {
                    splineA.childNodeS.Add(splineB.id + 1.0f);
                    splineB.parentNodeE.Add(splineA.id + 0.0f);
                    linkCount++;
                }

                Debug.Log($"[Check] {splineA.name} ➜ {splineB.name} = EndA→StartB = {Vector3.Distance(endA, startB):F3}");
            }

            EditorUtility.SetDirty(splineA);
        }


        Debug.Log($"[SplineAutoLinker] เชื่อมสำเร็จทั้งหมด {linkCount} connections");
    }

}
