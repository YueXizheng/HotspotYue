using System;
using System.Runtime.CompilerServices;
using Windows.Foundation;

namespace System
{
    /// <summary>
    /// IAsyncOperation&lt;T&gt; 的 await 支持。
    /// 说明：本机 .NET Framework 的 System.Runtime.WindowsRuntime 里没有可用的
    /// GetAwaiter 扩展（实测 CS4028），因此这里自己实现一个最小 awaiter，
    /// 只依赖 WinRT 元数据本身，不依赖 WindowsRuntimeSystemExtensions。
    /// </summary>
    public struct WinRtAwaiter<T> : INotifyCompletion
    {
        private readonly IAsyncOperation<T> _operation;

        public WinRtAwaiter(IAsyncOperation<T> operation)
        {
            _operation = operation;
        }

        public bool IsCompleted
        {
            get { return _operation.Status != AsyncStatus.Started; }
        }

        public T GetResult()
        {
            if (_operation.Status == AsyncStatus.Error)
            {
                throw _operation.ErrorCode;
            }
            return _operation.GetResults();
        }

        public void OnCompleted(Action continuation)
        {
            _operation.Completed = delegate(IAsyncOperation<T> info, AsyncStatus status)
            {
                continuation();
            };
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