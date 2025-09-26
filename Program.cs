using System;
using NetworkWatcher.Services;
using NetworkWatcher.Utils;

namespace NetworkWatcher
{
    class Program
    {
        static void Main(string[] args)
        {
            var watcher = new NetworkWatcherService();

            watcher.NetworkChanged += (sender, evt) =>
            {
                Console.WriteLine(JsonHelper.ToJson(evt));
            };

            watcher.ServiceStatusChanged += (sender, running) =>
            {
                Console.WriteLine("[Network Watcher] " + (running ? "Started" : "Stopped"));
            };

            watcher.Start();
            Console.ReadLine();
            watcher.Stop();
        }
    }
}
