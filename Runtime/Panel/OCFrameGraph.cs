#nullable enable

using UnityEngine;
using UnityEngine.UIElements;

namespace OperatorConsole
{
    /// <summary>Bar graph of the kept frame times, newest on the right: main-thread time, rest of the frame, frames over budget, and a dashed budget line.</summary>
    internal sealed class OCFrameGraph : VisualElement
    {
        private const float MinTopMs = 33.3f;
        private const float TopPadding = 8f;
        private const float DashLength = 6f;
        private const float DashGap = 5f;
        private const float LineWidth = 1f;

        private static readonly Color CpuColor = new(63f / 255f, 123f / 255f, 224f / 255f);
        private static readonly Color RestColor = new(159f / 255f, 194f / 255f, 1f, 0.45f);
        private static readonly Color OverColor = new(224f / 255f, 179f / 255f, 64f / 255f);
        private static readonly Color GridColor = new(1f, 1f, 1f, 0.06f);
        private static readonly Color BudgetColor = new(1f, 1f, 1f, 0.75f);

        private readonly OCFrameStats _stats;
        private readonly Label _topLabel = new();
        private readonly Label _budgetLabel = new();
        private float _budgetMs = 16.67f;
        private float _shownTop = -1f;
        private float _shownBudget = -1f;

        public OCFrameGraph(OCFrameStats stats)
        {
            _stats = stats;
            AddToClassList("ocp-graph");
            _topLabel.AddToClassList("ocp-graph__label");
            _budgetLabel.AddToClassList("ocp-graph__label");
            Add(_topLabel);
            Add(_budgetLabel);
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => PlaceLabels());
        }

        public void SetBudget(float budgetMs, float labelFontSize)
        {
            _budgetMs = budgetMs;
            _topLabel.style.fontSize = labelFontSize;
            _budgetLabel.style.fontSize = labelFontSize;
            PlaceLabels();
            MarkDirtyRepaint();
        }

        private float TopMs => Mathf.Max(_budgetMs * 2f, MinTopMs);

        private float Y(float ms, float height)
        {
            var top = TopMs;
            return height - Mathf.Min(ms, top) / top * (height - TopPadding);
        }

        private void PlaceLabels()
        {
            var height = contentRect.height;
            if (float.IsNaN(height) || height <= 0f) return;
            var top = TopMs;
            if (!Mathf.Approximately(top, _shownTop))
            {
                _shownTop = top;
                _topLabel.text = top.ToString("0") + " ms";
            }
            if (!Mathf.Approximately(_budgetMs, _shownBudget))
            {
                _shownBudget = _budgetMs;
                _budgetLabel.text = _budgetMs.ToString("0.0") + " ms";
            }
            _topLabel.style.top = 2f;
            _budgetLabel.style.top = Y(_budgetMs, height) - 2f;
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            var width = rect.width;
            var height = rect.height;
            if (width <= 0f || height <= 0f) return;

            var count = _stats.Count;
            var dashes = Mathf.CeilToInt(width / (DashLength + DashGap));
            var quads = count * 2 + dashes + 2;
            var mesh = context.Allocate(quads * 4, quads * 6);
            var vertex = 0;

            var barWidth = width / OCFrameStats.Capacity;
            for (var i = 0; i < count; i++)
            {
                var frame = _stats.FrameAt(i);
                var cpu = _stats.CpuAt(i);
                var x = width - (count - i) * barWidth;
                var right = x + Mathf.Max(1f, barWidth - 0.5f);
                var frameY = Y(frame, height);

                if (frame > _budgetMs || float.IsNaN(cpu))
                {
                    Quad(mesh, ref vertex, x, frameY, right, height, frame > _budgetMs ? OverColor : CpuColor);
                    Quad(mesh, ref vertex, x, height, right, height, CpuColor);
                    continue;
                }
                var cpuY = Y(Mathf.Min(cpu, frame), height);
                Quad(mesh, ref vertex, x, frameY, right, cpuY, RestColor);
                Quad(mesh, ref vertex, x, cpuY, right, height, CpuColor);
            }

            Quad(mesh, ref vertex, 0f, Y(_budgetMs * 0.5f, height), width, Y(_budgetMs * 0.5f, height) + LineWidth, GridColor);
            Quad(mesh, ref vertex, 0f, Y(_budgetMs * 1.5f, height), width, Y(_budgetMs * 1.5f, height) + LineWidth, GridColor);

            var budgetY = Y(_budgetMs, height);
            for (var d = 0; d < dashes; d++)
            {
                var x = d * (DashLength + DashGap);
                Quad(mesh, ref vertex, x, budgetY, Mathf.Min(x + DashLength, width), budgetY + LineWidth, BudgetColor);
            }
        }

        private static void Quad(MeshWriteData mesh, ref int vertex, float left, float top, float right, float bottom, Color color)
        {
            mesh.SetNextVertex(new Vertex { position = new Vector3(left, top, Vertex.nearZ), tint = color });
            mesh.SetNextVertex(new Vertex { position = new Vector3(right, top, Vertex.nearZ), tint = color });
            mesh.SetNextVertex(new Vertex { position = new Vector3(right, bottom, Vertex.nearZ), tint = color });
            mesh.SetNextVertex(new Vertex { position = new Vector3(left, bottom, Vertex.nearZ), tint = color });
            var b = (ushort)vertex;
            mesh.SetNextIndex(b);
            mesh.SetNextIndex((ushort)(b + 1));
            mesh.SetNextIndex((ushort)(b + 2));
            mesh.SetNextIndex(b);
            mesh.SetNextIndex((ushort)(b + 2));
            mesh.SetNextIndex((ushort)(b + 3));
            vertex += 4;
        }
    }
}
