#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace OperatorConsole
{
    /// <summary>The Runtime Panel's Performance tab: frame rate summary, frame time graph, and Frame / Memory / Rendering Metrics.</summary>
    internal sealed class OCPerformanceView : VisualElement, IOCLayoutAware
    {
        private const float TextInterval = 0.25f;
        private const float GraphInterval = 0.1f;
        private const string Unavailable = "n/a";
        private const float FpsScale = 3.2f;

        private readonly OCPerformance _performance;
        private readonly ScrollView _scroll = new(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
        private readonly Label _fps = new();
        private readonly Label _fpsUnit = new("FPS");
        private readonly Label _average = new();
        private readonly Label _low = new();
        private readonly Label _worst = new();
        private readonly Button _pause;
        private readonly Button _resetWorst;
        private readonly OCFrameGraph _graph;
        private readonly OCMetricCell _frameTime = new("Frame time");
        private readonly OCMetricCell _cpu = new("CPU main thread");
        private readonly OCMetricCell _gpu = new("GPU");
        private readonly OCMetricCell _gcAlloc = new("GC alloc / frame");
        private readonly OCMetricCell _allocated = new("Total allocated", true);
        private readonly OCMetricCell _mono = new("Mono heap used", true);
        private readonly OCMetricCell _batches = new("Batches");
        private readonly OCMetricCell _setPass = new("SetPass calls");
        private readonly OCMetricCell _triangles = new("Triangles");
        private readonly Button _forceGc;
        private readonly List<Label> _smallLabels = new();
        private readonly List<OCMetricCell> _cells = new();

        private OCPanelLayout? _layout;
        private bool _paused;
        private float _nextText;
        private float _nextGraph;

        public OCPerformanceView(OCPerformance performance)
        {
            _performance = performance;
            AddToClassList("ocp-perf");
            _scroll.AddToClassList("ocp-scroll");
            Add(_scroll);
            var content = _scroll.contentContainer;

            var hero = new VisualElement();
            hero.AddToClassList("ocp-perf__hero");
            var fps = new VisualElement();
            fps.AddToClassList("ocp-perf__fps");
            _fps.AddToClassList("ocp-perf__fps-value");
            _fpsUnit.AddToClassList("ocp-perf__fps-unit");
            fps.Add(_fps);
            fps.Add(_fpsUnit);
            hero.Add(fps);
            hero.Add(Stat("Average", _average));
            hero.Add(Stat("1% low", _low));
            hero.Add(Stat("Worst frame", _worst));
            var spacer = new VisualElement();
            spacer.AddToClassList("ocp-grow");
            hero.Add(spacer);
            _pause = SmallButton("Pause", TogglePause);
            _resetWorst = SmallButton("Reset min/max", () =>
            {
                _performance.ResetWorstFrame();
                UpdateText();
            });
            hero.Add(_pause);
            hero.Add(_resetWorst);
            content.Add(hero);

            _graph = new OCFrameGraph(performance.Frames);
            content.Add(_graph);
            content.Add(Legend());

            content.Add(Section("Frame", _frameTime, _cpu, _gpu, _gcAlloc));
            _forceGc = SmallButton("Force GC", () =>
            {
                GC.Collect();
                UpdateText();
            });
            var gcCell = new VisualElement();
            gcCell.AddToClassList("ocp-metric");
            gcCell.AddToClassList("ocp-metric--action");
            gcCell.Add(_forceGc);
            content.Add(Section("Memory", _allocated, _mono, gcCell));
            content.Add(Section("Rendering", _batches, _setPass, _triangles));
        }

        public void ApplyLayout(OCPanelLayout layout)
        {
            _layout = layout;
            _scroll.verticalScroller.style.width = layout.ScrollbarWidth;
            _scroll.mouseWheelScrollSize = layout.ScrollSensitivity;
            _fps.style.fontSize = layout.FontSize * FpsScale;
            _fpsUnit.style.fontSize = layout.FontSize;
            foreach (var label in new[] { _average, _low, _worst }) label.style.fontSize = layout.FontSize;
            foreach (var label in _smallLabels) label.style.fontSize = layout.FontSize * 0.7f;
            foreach (var cell in _cells) cell.ApplyLayout(layout);
            foreach (var button in new[] { _pause, _resetWorst, _forceGc })
            {
                button.style.fontSize = layout.FontSize * 0.85f;
                button.style.height = layout.RowHeight;
            }
            _graph.style.height = layout.PerformanceGraphHeight;
            _graph.SetBudget(layout.FrameBudgetMs, layout.FontSize * 0.65f);
            UpdateText();
        }

        /// <summary>Updates the numbers a few times a second and the graph ten times a second, unless paused.</summary>
        public void Refresh()
        {
            if (_paused) return;
            var now = Time.unscaledTime;
            if (now >= _nextGraph)
            {
                _nextGraph = now + GraphInterval;
                _graph.MarkDirtyRepaint();
            }
            if (now >= _nextText)
            {
                _nextText = now + TextInterval;
                UpdateText();
            }
        }

        private void TogglePause()
        {
            _paused = !_paused;
            _pause.text = _paused ? "Resume" : "Pause";
        }

        private void UpdateText()
        {
            if (_layout == null) return;
            var p = _performance;
            var budget = _layout.FrameBudgetMs;
            _fps.text = Mathf.RoundToInt(p.CurrentFps).ToString();
            _average.text = p.AverageFps.ToString("0.0") + " fps";
            _low.text = p.OnePercentLowFps.ToString("0.0") + " fps";
            _worst.text = p.WorstFrameMs.ToString("0.0") + " ms";

            _frameTime.SetValue(Ms(p.LatestFrameMs), p.LatestFrameMs > budget);
            _cpu.SetValue(Ms(p.CpuMainThreadMs));
            _gpu.SetValue(Ms(p.GpuMs));
            _gpu.SetNote(float.IsNaN(p.GpuMs) && !p.FrameTimingAvailable ? "Turn on Frame Timing Stats in Player Settings" : null);
            _gcAlloc.SetValue(Bytes(p.GcAllocatedInFrame));

            var allocated = p.TotalAllocatedMemory;
            var reserved = p.TotalReservedMemory;
            _allocated.SetValue(Bytes(allocated));
            _allocated.SetNote("of " + Bytes(reserved) + " reserved");
            _allocated.SetFill(reserved > 0 ? (float)allocated / reserved : 0f);

            var monoUsed = p.MonoUsedMemory;
            var monoHeap = p.MonoHeapMemory;
            _mono.SetValue(Bytes(monoUsed));
            _mono.SetNote("of " + Bytes(monoHeap));
            _mono.SetFill(monoHeap > 0 ? (float)monoUsed / monoHeap : 0f);

            _batches.SetValue(Count(p.Batches));
            _setPass.SetValue(Count(p.SetPassCalls));
            _triangles.SetValue(Count(p.Triangles));
        }

        private static string Ms(float ms) => float.IsNaN(ms) ? Unavailable : ms.ToString("0.00") + " ms";

        private static string Bytes(long bytes)
        {
            if (bytes < 0) return Unavailable;
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024f).ToString("0.0") + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / (1024f * 1024f)).ToString("0.0") + " MB";
            return (bytes / (1024f * 1024f * 1024f)).ToString("0.00") + " GB";
        }

        private static string Count(long value)
        {
            if (value < 0) return Unavailable;
            if (value < 10_000) return value.ToString();
            if (value < 1_000_000) return (value / 1000f).ToString("0.0") + " K";
            return (value / 1_000_000f).ToString("0.00") + " M";
        }

        private VisualElement Stat(string name, Label value)
        {
            var stat = new VisualElement();
            stat.AddToClassList("ocp-perf__stat");
            var label = new Label(name.ToUpperInvariant());
            label.AddToClassList("ocp-perf__stat-name");
            _smallLabels.Add(label);
            value.AddToClassList("ocp-perf__stat-value");
            stat.Add(label);
            stat.Add(value);
            return stat;
        }

        private VisualElement Legend()
        {
            var legend = new VisualElement();
            legend.AddToClassList("ocp-perf__legend");
            legend.Add(Key("CPU main thread", "ocp-perf__key--cpu"));
            legend.Add(Key("Rest of frame", "ocp-perf__key--rest"));
            legend.Add(Key("Over budget", "ocp-perf__key--over"));
            legend.Add(Key("Budget", "ocp-perf__key--budget"));
            return legend;
        }

        private VisualElement Key(string text, string swatchClass)
        {
            var key = new VisualElement();
            key.AddToClassList("ocp-perf__key");
            var swatch = new VisualElement();
            swatch.AddToClassList("ocp-perf__swatch");
            swatch.AddToClassList(swatchClass);
            var label = new Label(text);
            _smallLabels.Add(label);
            key.Add(swatch);
            key.Add(label);
            return key;
        }

        private VisualElement Section(string title, params VisualElement[] cells)
        {
            var section = new VisualElement();
            section.AddToClassList("ocp-perf__section");
            var header = new Label(title.ToUpperInvariant());
            header.AddToClassList("ocp-header");
            header.AddToClassList("ocp-perf__section-title");
            _smallLabels.Add(header);
            section.Add(header);
            var grid = new VisualElement();
            grid.AddToClassList("ocp-perf__grid");
            foreach (var cell in cells)
            {
                grid.Add(cell);
                if (cell is OCMetricCell metric) _cells.Add(metric);
            }
            section.Add(grid);
            return section;
        }

        private static Button SmallButton(string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("ocp-button");
            return button;
        }
    }
}
