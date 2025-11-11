// Assets/Editor/SplitContainerAndExtrude.cs
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

public static class SplitContainerAndExtrude
{
    [MenuItem("Tools/Splines/Split Container into Children + Extrude")]
    private static void Run()
    {
        var sel = Selection.activeGameObject;
        if (!sel)
        {
            EditorUtility.DisplayDialog("Select a SplineContainer",
                "Select a GameObject that has a SplineContainer (e.g., L01) and run again.", "OK");
            return;
        }

        var srcContainer = sel.GetComponent<SplineContainer>();
        if (!srcContainer)
        {
            EditorUtility.DisplayDialog("No SplineContainer",
                "Selected GameObject does not have a SplineContainer.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(sel, "Split Container into Children + Extrude");

        int splCount = srcContainer.Splines.Count;
        if (splCount == 0)
        {
            EditorUtility.DisplayDialog("Empty Container", "No splines to split.", "OK");
            return;
        }

        // Optional tidy parent
        var holder = sel.transform.Find("__Segments");
        if (!holder)
        {
            var go = new GameObject("__Segments");
            go.transform.SetParent(sel.transform, false);
            holder = go.transform;
        }

        for (int i = 0; i < splCount; i++)
        {
            var childName = $"{sel.name}_S{i:00}";
            var existing = holder.Find(childName);
            GameObject child = existing ? existing.gameObject : new GameObject(childName);
            if (!existing) child.transform.SetParent(holder, false);

            // Ensure a clean child: remove any old components we don't want to duplicate messily
            var oldChildContainer = child.GetComponent<SplineContainer>();
            if (oldChildContainer) Undo.DestroyObjectImmediate(oldChildContainer);
            var oldExtrude = child.GetComponent<SplineExtrude>();
            if (oldExtrude) Undo.DestroyObjectImmediate(oldExtrude);

            // New child container with ONE spline copied from source
            var childContainer = Undo.AddComponent<SplineContainer>(child);
            var dst = childContainer.AddSpline();

            var src = srcContainer.Splines[i];
            dst.Closed = src.Closed;

            // Copy all knots verbatim
            // BezierKnot includes position, rotation, and tangents.
            for (int k = 0; k < src.Count; k++)
            {
                dst.Add(src[k]);
            }

            // Add an extruder that targets THIS child container and its single spline (index 0)
            var extrude = Undo.AddComponent<SplineExtrude>(child);

            // Configure via SerializedObject (version-safe)
            var so = new SerializedObject(extrude);

            // Point to child container
            var pContainer = so.FindProperty("m_Container");
            if (pContainer != null) pContainer.objectReferenceValue = childContainer;

            // Always spline 0 on the child (there is only one spline now)
            var pIndex = so.FindProperty("m_SplineIndex");
            if (pIndex != null) pIndex.intValue = 0;

            // Reasonable defaults for a thin line look
            var pProfileType = so.FindProperty("m_ShapeSettings.m_ProfileType"); // 0=Circle,1=Rectangle
            if (pProfileType != null) pProfileType.enumValueIndex = 0; // circle

            var pRadius = so.FindProperty("m_ShapeSettings.m_Radius");
            if (pRadius != null) pRadius.floatValue = 0.04f;

            var pSides = so.FindProperty("m_ShapeSettings.m_Sides");
            if (pSides != null) pSides.intValue = 12;

            var pStepsPerUnit = so.FindProperty("m_SamplingSettings.m_StepsPerUnit");
            if (pStepsPerUnit != null) pStepsPerUnit.floatValue = 14f;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.DisplayDialog(
            "Done",
            $"Split {splCount} segments under {sel.name} into __Segments/children and extruded each.\n" +
            "You can now delete/disable the original extruder on the parent if it was partially extruding.",
            "OK");
    }
}
