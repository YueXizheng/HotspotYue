using System;
using System.Collections.Generic;

namespace HotspotYue
{
    /// <summary>
    /// 内存里的环形日志。默认完全不落盘，最多保留 Capacity 行，
    /// 需要时由用户手动导出成 txt。
    /// </summary>
    public static class Logger
    {
        public const int Capacity = 2000;

        private static readonly object SyncRoot = new object();
        private static readonly LinkedList<string> Lines = new LinkedList<string>();

        public static event Action<string> LineAdded;

        public static void Write(string message)
        {
            string line = DateTime.Now.ToString("HH:mm:ss") + "  " + message;

            lock (SyncRoot)
            {
                Lines.AddLast(line);
                while (Lines.Count > Capacity)
                {
                    Lines.RemoveFirst();
                }
            }

            Action<string> handler = LineAdded;
            if (handler != null)
            {
                handler(line);
            }
        }

        public static string[] Snapshot()
        {
            lock (SyncRoot)
            {
                return new List<string>(Lines).ToArray();
            }
        }

        public static string FullText()
        {
            string[] lines = Snapshot();
            return string.Join(Environment.NewLine, lines);
        }

        public static int Count
        {
            get
            {
                lock (SyncRoot)
                {
                    return Lines.Count;
                }
            }
        }
    }
}