using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Networking.Connectivity;
using Windows.Networking.NetworkOperators;

namespace System
{
    public struct WinRtAwaiter<T> : INotifyCompletion
    {
        private readonly IAsyncOperation<T> _operation;
        public WinRtAwaiter(IAsyncOperation<T> operation) { _operation = operation; }
        public bool IsCompleted { get { return _operation.Status != AsyncStatus.Started; } }
        public T GetResult()
        {
            if (_operation.Status == AsyncStatus.Error) throw _operation.ErrorCode;
            return _operation.GetResults();
        }
        public void OnCompleted(Action continuation)
        {
            _operation.Completed = delegate(IAsyncOperation<T> info, AsyncStatus status) { continuation(); };
        }
    }

    public static class WinRtAwaitExtensions
    {
        public static WinRtAwaiter<T> GetAwaiter<T>(this IAsyncOperation<T> operation)
        {
            return new WinRtAwaiter<T>(operation);
        }
    }
}

internal static class HsCtl
{
    private static NetworkOperatorTetheringManager CreateManager()
    {
        ConnectionProfile profile = NetworkInformation.GetInternetConnectionProfile();
        if (profile == null) return null;
        return NetworkOperatorTetheringManager.CreateFromConnectionProfile(profile);
    }

    private static string ReadState()
    {
        NetworkOperatorTetheringManager manager = CreateManager();
        if (manager == null) return "NoProfile";
        return manager.TetheringOperationalState.ToString();
    }

    private static int Main(string[] args)
    {
        string command = args.Length > 0 ? args[0].ToLowerInvariant() : "state";

        if (command == "state")
        {
            Console.WriteLine(ReadState());
            return 0;
        }

        if (command == "on" || command == "start")
        {
            RunStart().Wait();
            return 0;
        }

        if (command == "off" || command == "stop")
        {
            RunStop().Wait();
            return 0;
        }

        if (command == "watch")
        {
            int seconds = args.Length > 1 ? int.Parse(args[1]) : 20;
            RunWatch(seconds).Wait();
            return 0;
        }

        Console.WriteLine("用法：hsctl state | on | off | watch <秒>");
        return 2;
    }

    private static async Task RunStart()
    {
        NetworkOperatorTetheringManager manager = CreateManager();
        NetworkOperatorTetheringOperationResult result = await manager.StartTetheringAsync();
        Console.WriteLine("start=" + result.Status);
    }

    private static async Task RunStop()
    {
        NetworkOperatorTetheringManager manager = CreateManager();
        NetworkOperatorTetheringOperationResult result = await manager.StopTetheringAsync();
        Console.WriteLine("stop=" + result.Status);
    }

    private static async Task RunWatch(int seconds)
    {
        string previous = "";
        DateTime deadline = DateTime.Now.AddSeconds(seconds);
        while (DateTime.Now < deadline)
        {
            string state = ReadState();
            if (state != previous)
            {
                Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "  " + state);
                previous = state;
            }
            await Task.Delay(300);
        }
    }
}