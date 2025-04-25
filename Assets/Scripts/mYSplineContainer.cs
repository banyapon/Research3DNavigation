using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class mYSplineContainer : MonoBehaviour
{
    public int id = 0;
    public SplineContainer splineContainer;
    // Node เชื่อมโยงแบบกราฟ: จุดเริ่ม (Start) และจุดสิ้นสุด (End)
    public List<float> parentNodeS;
    public List<float> childNodeS;
    public List<float> parentNodeE;
    public List<float> childNodeE;
}

public static class mySplineExtensions
{
    // Adds a custom method to MySealedClass
    public static void parentS(this SplineContainer mySpline)
    {
        Debug.Log("Hello, Parent S is " + mySpline);
    }

    // Adds a custom property to MySealedClass
    public static string GetFormattedName(this SplineContainer mySpline)
    {
        return "Formatted: " + mySpline;
    }
}
