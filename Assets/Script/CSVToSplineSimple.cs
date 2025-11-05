using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Splines;

[ExecuteInEditMode]
public class CSVToSplineSimple : MonoBehaviour
{
    public TextAsset csvFile;
    public char delimiter = ',';
    public bool hasHeader = false;

    public enum AxisMap { XYZ, XZY, YXZ, YZX, ZXY, ZYX }
    public AxisMap axisMap = AxisMap.XYZ;

    public bool flipX = false;
    public bool flipY = false;
    public bool flipZ = true;   // often needed from DCC → Unity
    public float scale = 1f;
    public bool recenterAtFirstPoint = false;

    public bool loop = false;
    public bool smoothTangents = true;
    [Range(0f, 1f)] public float tangentStrength = 0.5f;

    List<Vector3> pts = new List<Vector3>();

    void Start()
    {
        if (csvFile == null)
        {
            Debug.LogError("Assign a CSV file to CSVToSplineSimple.");
            return;
        }

        pts = Parse(csvFile.text);
        if (pts.Count < 2)
        {
            Debug.LogError("Parsed fewer than 2 points. Check delimiter/header/CSV format.");
            return;
        }

        var container = GetComponent<SplineContainer>();
        var spline = new Spline();

        Vector3 origin = recenterAtFirstPoint ? pts[0] : Vector3.zero;
        var knots = new List<BezierKnot>(pts.Count);

        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 p = pts[i] - origin;
            knots.Add(new BezierKnot(p));
        }

        if (smoothTangents && knots.Count > 2)
        {
            for (int i = 0; i < knots.Count; i++)
            {
                int iPrev = Mathf.Max(i - 1, 0);
                int iNext = Mathf.Min(i + 1, knots.Count - 1);

                Vector3 prev = knots[iPrev].Position;
                Vector3 curr = knots[i].Position;
                Vector3 next = knots[iNext].Position;

                Vector3 m = (next - prev) * 0.5f; // simple Catmull-Rom-ish
                var inTangent = -m * (tangentStrength * 0.33f);
                var outTangent = m * (tangentStrength * 0.33f);

                knots[i] = new BezierKnot(curr, inTangent, outTangent);
            }
        }

        // Add knots one by one (works across Unity versions)
        foreach (var k in knots)
            spline.Add(k);

        spline.Closed = loop;
        container.Spline = spline;

        Debug.Log($"[CSVToSplineSimple] Built spline with {pts.Count} points. axis={axisMap} flipZ={flipZ} scale={scale}");
    }

    List<Vector3> Parse(string text)
    {
        var list = new List<Vector3>();
        var lines = text.Split('\n');
        int start = hasHeader ? 1 : 0;

        for (int i = start; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            line = line.Replace("\r", "");
            var parts = line.Split(delimiter);
            if (parts.Length < 3) continue;

            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float a) &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float b) &&
                float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float c))
            {
                Vector3 v = MapAxes(a, b, c);

                if (flipX) v.x = -v.x;
                if (flipY) v.y = -v.y;
                if (flipZ) v.z = -v.z;

                v *= scale;
                list.Add(v);
            }
        }
        return list;
    }

    Vector3 MapAxes(float x, float y, float z)
    {
        switch (axisMap)
        {
            case AxisMap.XYZ: return new Vector3(x, y, z);
            case AxisMap.XZY: return new Vector3(x, z, y);
            case AxisMap.YXZ: return new Vector3(y, x, z);
            case AxisMap.YZX: return new Vector3(y, z, x);
            case AxisMap.ZXY: return new Vector3(z, x, y);
            case AxisMap.ZYX: return new Vector3(z, y, x);
            default: return new Vector3(x, y, z);
        }
    }
}
