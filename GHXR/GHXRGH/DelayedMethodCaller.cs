using System;
using System.Timers;

namespace GHXR
{
    class DelayedMethodCaller
    {
        int delay;
        Timer timer = new Timer();

        public DelayedMethodCaller(int delay)
        {
            this.delay = delay;
        }

        public void CallMethod(Action action)
        {
            if (!timer.Enabled)
            {
                timer = new Timer(delay)
                {
                    AutoReset = false
                };
                timer.Elapsed += (object sender, ElapsedEventArgs e) =>
                {
                    action();
                };
                timer.Start();
            }
            else
            {
                timer.Stop();
                timer.Start();
            }
        }
    }
}
