using System;
using System.Threading;
using Seq.Apps;
using Seq.Apps.LogEvents;
using Serilog.Events;

namespace Seq.App.Timeout
{
    [SeqApp("Timeout",
    Description = "Writes a timeout event after a specified time period has elapsed, unless received any event resetting the timer.")]
    public class TimeoutReactor : SeqApp, ISubscribeTo<LogEventData>, IDisposable
    {
        private readonly object _sync = new object();
        private readonly Timer _timer;
        bool _disposed;

        [SeqAppSetting(
            DisplayName = "Timeout (seconda)",
            HelpText = "If the app doesn't receive any events for this many seconds it will output a timeout event. Any value below 1 will disable the app instance.",
            InputType = SettingInputType.Integer,
            IsOptional = false)]
        public int Timeout { get; set; }

        [SeqAppSetting(
            DisplayName = "Repeat",
            HelpText = "Whether or not the timeout should repeat if there are no events. Otherwise, it will only trigger once and wait until next event.")]
        public bool Repeat { get; set; }

        [SeqAppSetting(
            DisplayName = "Event message template",
            HelpText = "Defaults to 'Timeout {Timeout} occurred'",
            InputType = SettingInputType.Text,
            IsOptional = true)]
        public string Template { get; set; } = "Timeout {Timeout} occurred";

        [SeqAppSetting(
            DisplayName = "Event properties",
            HelpText = "Custom properties to add to the timeout events. Format is Key=Value separate by line breaks.",
            InputType = SettingInputType.LongText,
            IsOptional = true)]
        public string Properties { get; set; }

        [SeqAppSetting(
            DisplayName = "Event level",
            HelpText = "Level of event to generate. Defaults to 'Information'. Valid values are 'Debug', 'Error', 'Fatal', 'Information', 'Warning', 'Verbose'",
            InputType = SettingInputType.Unspecified,
            IsOptional = true)]
        public Serilog.Events.LogEventLevel Level { get; set; } = Serilog.Events.LogEventLevel.Information;

        public TimeoutReactor()
        {
            _timer = new Timer(_ => {
                var logger = Log;
                if (!string.IsNullOrWhiteSpace(Properties))
                {
                    foreach (var p in Properties.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var parts = p.Split('=');
                        if (parts.Length != 2) continue;
                        logger = logger.ForContext(parts[0], parts[1]);
                    }
                }
                logger.Write(Level, Template, this.App.Title);
            });
        }

        public void On(Event<LogEventData> evt)
        {
            lock (_sync)
            {
                if (_disposed || Timeout < 1)
                    return;

                _timer.Change(TimeSpan.FromSeconds(Timeout), Repeat ? TimeSpan.FromSeconds(Timeout) : System.Threading.Timeout.InfiniteTimeSpan);
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                    return;

                _disposed = true;

                var wh = new ManualResetEvent(false);
                if (!_timer.Dispose(wh))
                    wh.WaitOne();
            }
        }
    }
}
