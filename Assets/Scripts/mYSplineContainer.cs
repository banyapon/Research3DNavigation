using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class mYSplineContainer : SplineContainer
{
    public int id = 0;

    // Node เชื่อมโยงแบบกราฟ: จุดเริ่ม (Start) และจุดสิ้นสุด (End)
    public List<float> parentNodeS;
    public List<float> childNodeS;
    public List<float> parentNodeE;
    public List<float> childNodeE;
}
