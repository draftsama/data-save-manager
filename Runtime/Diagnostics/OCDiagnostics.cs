#nullable enable

using Cysharp.Threading.Tasks;

namespace OperatorConsole
{
    /// <summary>Starts and stops keeping the Log and measuring Performance, depending on which Panel Definitions show them.</summary>
    internal static class OCDiagnostics
    {
        private static Ticker? s_ticker;

        public static bool AnyPanelShowsLog(OCConfig config)
        {
            foreach (var panel in config.Panels)
                if (panel.Enabled && panel.ShowLog) return true;
            return false;
        }

        public static bool AnyPanelShowsPerformance(OCConfig config)
        {
            foreach (var panel in config.Panels)
                if (panel.Enabled && panel.ShowPerformance) return true;
            return false;
        }

        /// <summary>Starts keeping the Log as early as possible; Performance waits for <see cref="StartTicking"/>.</summary>
        public static void ApplyLogCapture(OCConfig config, OCLog log)
        {
            log.SetCapacity(config.LogCapacity);
            if (AnyPanelShowsLog(config)) log.StartCapture();
            else log.StopCapture();
        }

        /// <summary>Hooks the per-frame update. UniTask's player loop must be ready, so call it after the first scene loads.</summary>
        public static void StartTicking(OCConfig config, OCLog log, OCPerformance performance)
        {
            if (AnyPanelShowsPerformance(config)) performance.StartMeasuring();
            else performance.StopMeasuring();

            if (s_ticker != null) return;
            s_ticker = new Ticker(log, performance);
            PlayerLoopHelper.AddAction(PlayerLoopTiming.PostLateUpdate, s_ticker);
        }

        public static void Shutdown(OCLog? log, OCPerformance? performance)
        {
            log?.StopCapture();
            performance?.StopMeasuring();
            if (s_ticker != null) s_ticker.Stopped = true;
            s_ticker = null;
        }

        private sealed class Ticker : IPlayerLoopItem
        {
            private readonly OCLog _log;
            private readonly OCPerformance _performance;

            public Ticker(OCLog log, OCPerformance performance)
            {
                _log = log;
                _performance = performance;
            }

            public bool Stopped { get; set; }

            public bool MoveNext()
            {
                if (Stopped) return false;
                _performance.Sample();
                _log.RaiseChangedIfNeeded();
                return true;
            }
        }
    }
}
