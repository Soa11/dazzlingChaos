// This is third party package DebugGUI downloaded from: https://assetstore.unity.com/packages/tools/utilities/debuggui-graph-139275
// This package is licensed under the Unity Asset Store Terms of Service: https://unity3d.com/legal/as_terms

// The package shouldn't be able to be used outside of coherence, so we are keeping it internal.

namespace Coherence.Toolkit.Debugging
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    internal class DebugGUI : MonoBehaviour
    {
        const int graphWidth = 600;
        const int graphHeight = 100;
        const float temporaryLogLifetime = 5f;

        // Show logs and graphs in build?
        [SerializeField] bool drawInBuild = false;

        [SerializeField] bool displayGraphs = true;
        [SerializeField] bool displayLogs = true;

        [SerializeField] Color backgroundColor = new Color(0f, 0f, 0f, 0.7f);

        [Header("Runtime Debugging Only")]
        [SerializeField] List<GraphContainer> graphs = new List<GraphContainer>();

        Dictionary<object, string> persistentLogs = new Dictionary<object, string>();
        Queue<TransientLog> transientLogs = new Queue<TransientLog>();
        Dictionary<object, GraphContainer> graphDictionary = new Dictionary<object, GraphContainer>();

        GUIStyle minMaxTextStyle;
        GUIStyle boxStyle;

        // On mouse down on graph, stop moving it
        bool freezeGraphs;

        bool LogsEnabled { get { return displayLogs && (drawInBuild || Application.isEditor); } }
        bool GraphsEnabled { get { return displayGraphs && (drawInBuild || Application.isEditor); } }

        public bool IsOnRight = true;

        public void InstanceLogPersistent(object key, object message)
        {
            if (persistentLogs.ContainsKey(key))
                persistentLogs[key] = message.ToString();
            else
                persistentLogs.Add(key, message.ToString());
        }

        public void InstanceRemovePersistent(object key)
        {
            if (persistentLogs.ContainsKey(key))
                persistentLogs.Remove(key);
        }

        public void InstanceClearPersistent()
        {
            persistentLogs.Clear();
        }

        public void InstanceRemoveGraph(object key)
        {
            if (graphDictionary.ContainsKey(key))
            {
                var graph = graphDictionary[key];
                graph.DestroyTextures();
                graphs.Remove(graph);
                graphDictionary.Remove(key);
            }
        }

        public void InstanceRemoveAllGraphs()
        {
            foreach (var graph in graphs)
            {
                graph.DestroyTextures();
            }

            graphs.Clear();
            graphDictionary.Clear();
        }

        public void InstanceClearGraph(object key)
        {
            if (graphDictionary.ContainsKey(key))
                graphDictionary[key].Clear();
        }

        public void InstanceClearAllGraphs()
        {
            foreach (var graph in graphs)
            {
                graph.Clear();
            }
        }

        public void InstanceLog(string str)
        {
            transientLogs.Enqueue(new TransientLog(str, temporaryLogLifetime));
        }

        public void InstanceGraph(object key, float val)
        {
            if (!graphDictionary.ContainsKey(key))
            {
                InstanceCreateGraph(key);
            }

            if (freezeGraphs) return;

            graphDictionary[key].Push(val);
        }

        public void InstanceSetGraphProperties(object key, string label, float min, float max, int group, Color color, bool autoScale)
        {
            if (!graphDictionary.ContainsKey(key))
                InstanceCreateGraph(key);

            var graph = graphDictionary[key];
            graph.name = label;
            graph.SetMinMax(min, max, true);
            graph.group = Mathf.Max(0, group);
            graph.color = color;
            graph.autoScale = autoScale;
        }

        public bool InstanceGetGraphExists(object key)
        {
            return graphDictionary.ContainsKey(key);
        }

        public void InstanceCreateGraph(object key)
        {
            graphDictionary.Add(key, new GraphContainer(graphWidth, graphHeight));
            graphs.Add(graphDictionary[key]);
        }

        void Awake()
        {
            if (!drawInBuild && !Application.isEditor) return;

            InitializeGUIStyles();
        }

        void Update()
        {
            if (LogsEnabled)
            {
                // Clean up expired logs
                while (transientLogs.Count > 0 && transientLogs.Peek().expiryTime <= Time.realtimeSinceStartup)
                {
                    transientLogs.Dequeue();
                }
            }
        }

        void OnGUI()
        {
            GUI.color = Color.white;

            if (LogsEnabled)
                DrawLogs();

            if (GraphsEnabled)
                DrawGraphs();
        }

        Texture2D boxTexture;
        void InitializeGUIStyles()
        {
            minMaxTextStyle = new GUIStyle();
            minMaxTextStyle.fontSize = 10;
            minMaxTextStyle.fontStyle = FontStyle.Bold;

            Color[] pix = new Color[4];
            for (int i = 0; i < pix.Length; ++i)
            {
                pix[i] = Color.white;
            }
            boxTexture = new Texture2D(2, 2);
            boxTexture.SetPixels(pix);
            boxTexture.Apply();

            boxStyle = new GUIStyle();
            boxStyle.normal.background = boxTexture;
        }

        const float minMaxTextHeight = 8f;
        const float nextLineHeight = 15f;
        GUIContent labelGuiContent = new GUIContent();
        float textWidth;
        Rect textRect;
        void DrawLogs()
        {
            GUI.backgroundColor = backgroundColor;
            GUI.Box(new Rect(0, 0, textWidth + 10, textRect.y + 5), "", boxStyle);

            textRect = new Rect(5, 0, Screen.width, Screen.height);
            textWidth = 0;

            foreach (var log in persistentLogs.Values)
            {
                DrawLabel(log);
            }

            if (textRect.y != 0 && transientLogs.Count != 0)
            {
                DrawLabel("-------------------");
            }

            foreach (var log in transientLogs)
            {
                DrawLabel(log.text);
            }

            // Clear up transient logs going off screen
            while (textRect.y > Screen.height && transientLogs.Count > 0)
            {
                transientLogs.Dequeue();
                textRect.y -= nextLineHeight;
            }
        }

        void DrawLabel(string label)
        {
            labelGuiContent.text = label;
            GUI.Label(textRect, labelGuiContent);
            textRect.y += nextLineHeight;
            textWidth = Mathf.Max(textWidth, GUIStyle.none.CalcSize(labelGuiContent).x);
        }

        HashSet<int> graphGroupBoxesDrawn = new HashSet<int>();
        float graphLabelWidth;

        void DrawGraphs()
        {
            float graphBlockHeight = (graphHeight + 3);
            float graphLeft = IsOnRight ? Screen.width - graphWidth - graphLabelWidth - 5 : 0;
            float lastGraphLabelWidth = graphLabelWidth;
            GUI.backgroundColor = backgroundColor;

            // Boxes for the graph labels
            foreach (var group in graphGroupBoxesDrawn)
            {
                GUI.Box(new Rect(
                    graphLeft,
                    group * graphBlockHeight,
                    graphLabelWidth,
                    graphHeight
                ), "", boxStyle);
            }

            graphLabelWidth = 0;
            graphGroupBoxesDrawn.Clear();

            // Boxes for the graphs themselves
            foreach (GraphContainer graph in graphDictionary.Values)
            {
                if (graphGroupBoxesDrawn.Add(graph.group))
                {
                    GUI.Box(new Rect(graphLeft + lastGraphLabelWidth + 5, 0 + graphBlockHeight * graph.group, graphWidth, graphHeight), "", boxStyle);
                }

                graph.Draw(new Rect(graphLeft + lastGraphLabelWidth + 5, 0 + graphBlockHeight * graph.group, graphWidth, graphHeight));
            }

            foreach (int group in graphGroupBoxesDrawn)
            {
                float groupOrigin = group * graphBlockHeight;
                float yOffset = groupOrigin + minMaxTextHeight;
                float minMaxXOffset = 0;
                foreach (GraphContainer graph in graphDictionary.Values)
                {
                    labelGuiContent.text = "";
                    if (graph.group == group)
                    {
                        minMaxTextStyle.normal.textColor = graph.color;
                        GUI.color = Color.white;

                        string minText = graph.min.ToString("F2");
                        string maxText = graph.max.ToString("F2");
                        labelGuiContent.text = minText;
                        Vector2 textSize = minMaxTextStyle.CalcSize(labelGuiContent);
                        float width = textSize.x;
                        labelGuiContent.text = maxText;
                        width = Mathf.Max(width, minMaxTextStyle.CalcSize(labelGuiContent).x);

                        // Max
                        labelGuiContent.text = graph.max.ToString("F2");
                        minMaxXOffset += width + 5;
                        GUI.Label(new Rect(graphLeft + lastGraphLabelWidth + 5 - minMaxXOffset, groupOrigin, minMaxXOffset, graphHeight), labelGuiContent, minMaxTextStyle);
                        // Min
                        labelGuiContent.text = graph.min.ToString("F2");
                        GUI.Label(new Rect(graphLeft + lastGraphLabelWidth + 5 - minMaxXOffset, groupOrigin + graphHeight - textSize.y, minMaxXOffset, graphHeight), labelGuiContent, minMaxTextStyle);

                        GUI.color = graph.color;
                        // Name
                        labelGuiContent.text = $"{graph.name}: {graph.GetLastValue():F5}";
                        float xOffset = GUIStyle.none.CalcSize(labelGuiContent).x + 5;

                        graphLabelWidth = Mathf.Max(xOffset, graphLabelWidth, minMaxXOffset);

                        GUI.Label(new Rect(graphLeft + lastGraphLabelWidth + 5 - xOffset, yOffset, xOffset, graphHeight), labelGuiContent);
                        yOffset += nextLineHeight;
                    }
                }
            }

            // Draw value at mouse position
            var mousePos = Input.mousePosition;
            mousePos.y = Screen.height - mousePos.y;

            if (freezeGraphs && !Input.GetMouseButton(0))
                freezeGraphs = false;

            var onGroup = false;

            foreach (var group in graphGroupBoxesDrawn)
            {
                // aabb check for mouse
                if (
                    mousePos.x < graphLeft + graphWidth + lastGraphLabelWidth + 5 && mousePos.x > graphLeft + lastGraphLabelWidth + 5 &&
                    mousePos.y > group * graphBlockHeight && mousePos.y < group * graphBlockHeight + graphHeight
                )
                {
                    onGroup = true;
                    break;
                }
            }

            if (onGroup)
            {
                if (Input.GetMouseButtonDown(0))
                    freezeGraphs = true;

                foreach (var group in graphGroupBoxesDrawn)
                {

                    // Line
                    GUI.backgroundColor = new Color(1, 1, 0, 0.75f);
                    GUI.color = new Color(1, 1, 0, 0.75f);
                    GUI.Box(new Rect(mousePos.x, group * graphBlockHeight, 1, graphHeight), "", boxStyle);

                    // Background box
                    GUI.backgroundColor = new Color(0, 0, 0, 0.5f);
                    GUI.color = Color.white;
                    GUI.Box(new Rect(mousePos.x - 60, group * graphBlockHeight, 55, 55), "", boxStyle);

                    //int graphMousePos = (int)(graphWidth - (Screen.width - mousePos.x));
                    int graphMousePos = (int)(mousePos.x - (graphLeft + lastGraphLabelWidth + 5));

                    float yOffset = 0;
                    foreach (GraphContainer graph in graphDictionary.Values)
                    {
                        if (group == graph.group)
                        {

                            minMaxTextStyle.normal.textColor = graph.color;
                            GUI.color = Color.white;
                            labelGuiContent.text = graph.GetValue(graphMousePos).ToString("F5");
                            GUI.Label(new Rect(mousePos.x + -55, graph.group * graphBlockHeight + yOffset, 45, 50), labelGuiContent, minMaxTextStyle);
                            yOffset += 8f;
                        }
                    }
                }
            }
        }

        struct TransientLog
        {
            public string text;
            public float expiryTime;

            public TransientLog(string text, float duration)
            {
                this.text = text;
                expiryTime = Time.realtimeSinceStartup + duration;
            }
        }

        [Serializable]
        class GraphContainer
        {
            public string name;

            // Value at the top of the graph
            public float max = 1;
            private float defaultMax = 1;
            // Value at the bottom of the graph
            public float min = 0;
            private float defaultMin = 0;
            // Should min/max scale to values outside of min/max?
            public bool autoScale;
            public Color color;
            // Graph order on screen
            public int group;

            Texture2D tex0;
            Texture2D tex1;
            bool texFlipFlop;

            int currentIndex;
            float[] values;

            public void SetDefaultMinMax()
            {
                SetMinMax(defaultMin, defaultMax, false);
            }

            public void SetMinMax(float min, float max, bool isDefault)
            {
                if (isDefault)
                {
                    this.defaultMin = min;
                    this.defaultMax = max;
                }

                if (this.min == min && this.max == max) return;

                this.min = min;
                this.max = max;

                RegenerateGraph();
            }

            static Color32[] clearColorArray = new Color32[graphWidth * graphHeight];

            public GraphContainer(int width, int height)
            {
                values = new float[width];

                tex0 = new Texture2D(width, height);
                tex0.SetPixels32(clearColorArray);
                tex1 = new Texture2D(width, height);
                tex1.SetPixels32(clearColorArray);
            }

            // Add a data point to the beginning of the graph
            public void Push(float val)
            {
                // Scale up if needed
                if (autoScale && (val > max || val < min))
                {
                    SetMinMax(Mathf.Min(val, min), Mathf.Max(val, max), false);
                }

                currentIndex = (currentIndex + 1) % values.Length;
                var oldestValue = values[currentIndex];

                values[currentIndex] = val;

                // Scale down if oldest value is at min or max
                if (autoScale && (oldestValue == max || oldestValue == min))
                {
                    // Recalculate min/max
                    var newMin = float.MaxValue;
                    var newMax = float.MinValue;
                    for (var i = 0; i < values.Length; i++)
                    {
                        if (values[i] < newMin) newMin = values[i];
                        if (values[i] > newMax) newMax = values[i];
                    }

                    SetMinMax(Mathf.Min(newMin, defaultMin), Mathf.Max(newMax, defaultMax), false);
                }

                Texture2D source = texFlipFlop ? tex0 : tex1;
                Texture2D target = texFlipFlop ? tex1 : tex0;
                texFlipFlop = !texFlipFlop;

                Graphics.CopyTexture(
                    source, 0, 0, 0, 0, source.width - 1, source.height,
                    target, 0, 0, 1, 0
                );

                // Clear column
                for (int i = 0; i < target.height; i++)
                {
                    target.SetPixel(0, i, Color.clear);
                }

                DrawValue(target, 0, currentIndex);
            }

            private void DrawValue(Texture2D target, int x, int index)
            {
                // Read from index backwards
                var value = values[Mod(index, values.Length)];

                // Flip the y coordinate to start at the bottom
                var y0 = (int)(Mathf.InverseLerp(min, max, value) * graphHeight);

                // Prevent wraparound to zero
                y0 = y0 >= graphHeight ? graphHeight - 1 : y0;

                var color = this.color;
                if (value < min || value > max)
                    color = Color.red;

                target.SetPixel(x, y0, color);
            }

            public void Clear()
            {
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = 0;
                }
                currentIndex = 0;

                tex0.SetPixels32(clearColorArray);
                tex1.SetPixels32(clearColorArray);

                SetDefaultMinMax();
            }

            // Draw this graph on the given texture
            public void Draw(Rect rect)
            {
                Texture2D target = texFlipFlop ? tex1 : tex0;

                target.Apply();

                //var renderTexture = new RenderTexture(target.width, target.height, 10);
                //Graphics.Blit(target, renderTexture, new Vector2(1, 1), new Vector2(0, 0));
                //renderTexture.

                rect = new Rect(rect.x + rect.width, rect.y, -rect.width, rect.height);

                GUI.DrawTexture(rect, target);
            }

            public float GetLastValue()
            {
                return values[Mod(currentIndex - 1, values.Length)];
            }

            public float GetValue(int index)
            {
                return values[Mod(currentIndex + index, values.Length)];
            }

            // Redraw graph using data points
            private void RegenerateGraph()
            {
                Texture2D source = texFlipFlop ? tex0 : tex1;
                tex0.SetPixels32(clearColorArray);
                tex1.SetPixels32(clearColorArray);

                for (int i = 0; i < values.Length; i++)
                {
                    DrawValue(source, i, currentIndex - i);
                }
            }

            private static int Mod(int n, int m)
            {
                return ((n % m) + m) % m;
            }

            // Modified version of:
            // Method Author: Eric Haines (Eric5h5) 
            // Creative Common's Attribution-ShareAlike 3.0 Unported (CC BY-SA 3.0)
            // http://wiki.unity3d.com/index.php?title=TextureDrawLine
            private void DrawLine(Texture2D tex, int x0, int y0, int x1, int y1, Color col)
            {
                int dy = y1 - y0;
                int dx = x1 - x0;
                int stepx, stepy;

                if (dy < 0) { dy = -dy; stepy = -1; }
                else { stepy = 1; }
                if (dx < 0) { dx = -dx; stepx = -1; }
                else { stepx = 1; }
                dy <<= 1;
                dx <<= 1;

                float fraction = 0;

                tex.SetPixel(x0, y0, col);
                if (dx > dy)
                {
                    fraction = dy - (dx >> 1);
                    while ((x0 > x1 ? x0 - x1 : x1 - x0) > 1)
                    {
                        if (fraction >= 0)
                        {
                            y0 += stepy;
                            fraction -= dx;
                        }
                        x0 += stepx;
                        fraction += dy;
                        tex.SetPixel(x0, y0, col);
                    }
                }
                else
                {
                    fraction = dx - (dy >> 1);
                    while ((y0 > y1 ? y0 - y1 : y1 - y0) > 1)
                    {
                        if (fraction >= 0)
                        {
                            x0 += stepx;
                            fraction -= dy;
                        }
                        y0 += stepy;
                        fraction += dx;
                        tex.SetPixel(x0, y0, col);
                    }
                }
            }

            public void DestroyTextures()
            {
                Destroy(tex0);
                Destroy(tex1);
            }
        }

        void OnDestroy()
        {
            if (Application.isPlaying)
                Destroy(boxTexture);
        }
    }

}
