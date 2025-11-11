// Assets/Editor/CSVToSplinesImporter.cs
// CSV (x,y,z) -> Unity Splines (grouped by line prefix like L01_S01 -> L01 container)
//
// Usage: Tools > Splines > Import CSV Folder…
//
// CSV rows supported per line:
//   x,y,z
//   i,x,y,z        (index is ignored)
// Optional header is skipped if first token is non-numeric.

using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics; // for float3

public class CSVToSplinesImporter : EditorWindow
{
    // --- User options (editable in window) ---
    float scale = 1.0f;                 // 1.0 for meters CSV, 0.01 if CSV is centimeters
    bool detectClosed = true;           // close spline if first~last within epsilon
    float closeEpsilon = 0.001f;        // meters
    bool autoSmooth = true;             // generate smooth Bezier tangents
    float smoothTension = 0.5f;         // 0..1 (0 = loose, 1 = tighter)
    string parentRootName = "Imported_Splines";

    string folderPath = "";

    [MenuItem("Tools/Splines/Import CSV Folder…")]
    static void Open() => GetWindow<CSVToSplinesImporter>("CSV → Splines");

    void OnGUI()
    {
        EditorGUILayout.LabelField("CSV → Unity Splines (batch importer)", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("CSV Folder (under Assets):", GUILayout.Width(180));
            EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(folderPath) ? "(none)" : folderPath, GUILayout.Height(16));
        }

        if (GUILayout.Button("Choose Folder…"))
        {
            var abs = EditorUtility.OpenFolderPanel("Pick CSV folder (must be inside Assets)", Application.dataPath, "");
            if (!string.IsNullOrEmpty(abs) && abs.StartsWith(Application.dataPath))
                folderPath = "Assets" + abs.Substring(Application.dataPath.Length);
            else if (!string.IsNullOrEmpty(abs))
                EditorUtility.DisplayDialog("Folder must be inside Assets", "Please choose a folder that is INSIDE your project's Assets directory.", "OK");
        }

        EditorGUILayout.Space();
        scale = EditorGUILayout.FloatField("Scale (CSV → meters)", scale);
        detectClosed = EditorGUILayout.Toggle("Detect & Close Loops", detectClosed);
        closeEpsilon = EditorGUILayout.FloatField("Close Epsilon (m)", closeEpsilon);
        autoSmooth = EditorGUILayout.Toggle("Auto Smooth Tangents", autoSmooth);
        if (autoSmooth)
            smoothTension = EditorGUILayout.Slider("Smooth Tension", smoothTension, 0.0f, 1.0f);
        parentRootName = EditorGUILayout.TextField("Root Parent Name", parentRootName);

        EditorGUILayout.Space();
        EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(folderPath));
        if (GUILayout.Button("Import CSVs → Splines"))
            ImportAll();
        EditorGUI.EndDisabledGroup();
    }

    void ImportAll()
    {
        var csvs = AssetDatabase.FindAssets("t:TextAsset", new[] { folderPath })
                                .Select(AssetDatabase.GUIDToAssetPath)
                                .Where(p => p.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase))
                                .OrderBy(p => p)
                                .ToList();

        if (csvs.Count == 0)
        {
            EditorUtility.DisplayDialog("No CSVs", "No .csv files found in the chosen folder.", "OK");
            return;
        }

        // Group by line prefix before first underscore, e.g. L01_S01 → group "L01"
        var groups = new Dictionary<string, List<string>>(); // <-- fixed angle brackets
        foreach (var path in csvs)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            string group = name.Contains("_") ? name.Split('_')[0] : name; // L01_S01 → L01
            if (!groups.ContainsKey(group)) groups[group] = new List<string>();
            groups[group].Add(path);
        }

        // Create a root to keep scene tidy
        var rootGO = new GameObject(parentRootName);

        int splineCount = 0;
        foreach (var kv in groups)
        {
            string groupName = kv.Key;
            var fileList = kv.Value.OrderBy(p => p).ToList();

            // One container per group (line)
            var lineGO = new GameObject(groupName);
            lineGO.transform.SetParent(rootGO.transform, worldPositionStays: false);
            var container = lineGO.AddComponent<SplineContainer>();

            // Optional helper to remember segment names
            var info = lineGO.GetComponent<SplineInfo>() ?? lineGO.AddComponent<SplineInfo>();
            info.EnsureList();

            foreach (var assetPath in fileList)
            {
                var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                if (!textAsset) continue;

                var points = ParseCSVPoints(textAsset.text, scale);
                if (points.Count < 2) continue;

                bool shouldClose = detectClosed && (points[0] - points[points.Count - 1]).magnitude <= closeEpsilon;

                // Create the spline *inside* the container, then fill it
                var spline = container.AddSpline();

                // Add knots (explicit float3 to be version-safe)
                for (int i = 0; i < points.Count; i++)
                    spline.Add(new BezierKnot((float3)points[i]));

                // Close if needed
                spline.Closed = shouldClose;

                // Tangents
                if (autoSmooth)
                    SmoothTangents(spline, shouldClose, smoothTension);
                else
                    ZeroTangents(spline); // "linear-ish" fallback

                // Store the file base name for reference
                var segName = Path.GetFileNameWithoutExtension(assetPath);
                info.SegmentNames.Add(segName);

                splineCount++;
            }
        }

        EditorGUIUtility.PingObject(rootGO);
        EditorUtility.DisplayDialog("CSV → Splines", $"Imported {csvs.Count} CSV files as {splineCount} splines across {groups.Count} line containers.", "Done");
    }

    // ---------- Helpers ----------

    static List<Vector3> ParseCSVPoints(string text, float scale)
    {
        var pts = new List<Vector3>(1024);
        using (var reader = new StringReader(text))
        {
            string line;
            bool first = true;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(new[] { ',', ';', '\t' });
                if (parts.Length < 3) continue;

                // Skip header if first token is non-numeric
                if (first && !IsNumeric(parts[0]))
                {
                    first = false;
                    continue;
                }
                first = false;

                // Accept [x,y,z] or [i,x,y,z]
                int offset = (parts.Length >= 4 && IsNumeric(parts[1])) ? 1 : 0;

                if (TryFloat(parts[offset + 0], out float x) &&
                    TryFloat(parts[offset + 1], out float y) &&
                    TryFloat(parts[offset + 2], out float z))
                {
                    pts.Add(new Vector3(x, y, z) * scale);
                }
            }
        }
        return pts;
    }

    static bool TryFloat(string s, out float f) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f);

    static bool IsNumeric(string s) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    // Sets tangents to zero (corner/linear style)
    static void ZeroTangents(Spline spline)
    {
        for (int i = 0; i < spline.Count; i++)
        {
            var k = spline[i];
            k.TangentIn = float3.zero;
            k.TangentOut = float3.zero;
            spline[i] = k;
        }
    }

    // Simple Catmull-Rom-ish smoothing to Bezier tangents.
    // Works across Splines versions without relying on version-specific APIs.
    static void SmoothTangents(Spline spline, bool closed, float tension)
    {
        int count = spline.Count;
        if (count < 2)
        {
            ZeroTangents(spline);
            return;
        }

        tension = Mathf.Clamp01(tension);

        for (int i = 0; i < count; i++)
        {
            int iPrev = i - 1;
            int iNext = i + 1;

            if (closed)
            {
                if (iPrev < 0) iPrev += count;
                if (iNext >= count) iNext -= count;
            }

            var k = spline[i];
            Vector3 Pi = (Vector3)k.Position;

            Vector3 tangent;

            if (iPrev < 0) // start (open)
            {
                Vector3 Pi1 = (Vector3)spline[iNext].Position;
                tangent = (Pi1 - Pi);
            }
            else if (iNext >= count) // end (open)
            {
                Vector3 Pm1 = (Vector3)spline[iPrev].Position;
                tangent = (Pi - Pm1);
            }
            else // interior (or closed)
            {
                Vector3 Pm1 = (Vector3)spline[iPrev].Position;
                Vector3 Pi1 = (Vector3)spline[iNext].Position;
                tangent = (Pi1 - Pm1) * 0.5f; // central difference
            }

            Vector3 handle = tangent * (tension / 3f); // CR→Bezier baseline

            k.TangentIn = new float3(-handle.x, -handle.y, -handle.z);
            k.TangentOut = new float3(handle.x, handle.y, handle.z);
            spline[i] = k;
        }
    }
}

// Optional helper to track which segment names were added to a line container.
public class SplineInfo : MonoBehaviour
{
    public List<string> SegmentNames;
    public void EnsureList() { if (SegmentNames == null) SegmentNames = new List<string>(); }
}
