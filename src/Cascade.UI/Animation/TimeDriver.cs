namespace Cascade.UI;

/// <summary>
/// Factory methods for time-based animation drivers.
/// </summary>
public static class TimeDriver
{
    /// <summary>
    /// Creates a driver that progresses from 0 to 1 over the given duration.
    /// </summary>
    public static AnimationDriver Playing(Duration duration)
    {
        var driver = new PlayingDriver(duration);
        driver.Start();
        return driver;
    }

    /// <summary>
    /// Creates a driver that loops continuously over the given duration.
    /// </summary>
    public static AnimationDriver Looping(Duration duration)
    {
        var driver = new LoopingDriver(duration);
        driver.Start();
        return driver;
    }

    /// <summary>
    /// Creates a driver that plays forward then backward continuously.
    /// </summary>
    public static AnimationDriver PingPong(Duration duration)
    {
        var driver = new PingPongDriver(duration);
        driver.Start();
        return driver;
    }

    internal sealed class PlayingDriver : AnimationDriver
    {
        private readonly float durationSeconds;
        private float elapsed;
        private float currentProgress;

        internal PlayingDriver(Duration duration)
        {
            durationSeconds = (float)(duration.TotalMilliseconds / 1000.0);
        }

        public override float Progress => currentProgress;

        internal void Start()
        {
            if (durationSeconds <= 0f)
            {
                currentProgress = 1f;
                NotifyProgressChanged(currentProgress);
                return;
            }

            SharedScheduler.Instance.Register(
                dt =>
                {
                    elapsed += dt;
                    float p = Math.Clamp(elapsed / durationSeconds, 0f, 1f);
                    currentProgress = p;
                    NotifyProgressChanged(currentProgress);
                },
                () => elapsed >= durationSeconds);
        }
    }

    internal sealed class LoopingDriver : AnimationDriver
    {
        private readonly float durationSeconds;
        private float elapsed;
        private float currentProgress;

        internal LoopingDriver(Duration duration)
        {
            durationSeconds = (float)(duration.TotalMilliseconds / 1000.0);
        }

        public override float Progress => currentProgress;

        internal void Start()
        {
            if (durationSeconds <= 0f)
            {
                currentProgress = 1f;
                NotifyProgressChanged(currentProgress);
                return;
            }

            SharedScheduler.Instance.Register(
                dt =>
                {
                    elapsed += dt;
                    currentProgress = (elapsed / durationSeconds) % 1f;
                    NotifyProgressChanged(currentProgress);
                },
                () => false);
        }
    }

    internal sealed class PingPongDriver : AnimationDriver
    {
        private readonly float durationSeconds;
        private float elapsed;
        private float currentProgress;

        internal PingPongDriver(Duration duration)
        {
            durationSeconds = (float)(duration.TotalMilliseconds / 1000.0);
        }

        public override float Progress => currentProgress;

        internal void Start()
        {
            if (durationSeconds <= 0f)
            {
                currentProgress = 1f;
                NotifyProgressChanged(currentProgress);
                return;
            }

            SharedScheduler.Instance.Register(
                dt =>
                {
                    elapsed += dt;
                    float cycle = (elapsed / durationSeconds) % 2f;
                    currentProgress = cycle <= 1f ? cycle : 2f - cycle;
                    NotifyProgressChanged(currentProgress);
                },
                () => false);
        }
    }
}
