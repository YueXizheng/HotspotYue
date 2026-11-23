using System;
using System.Threading.Tasks;
using Windows.Networking.Connectivity;
using Windows.Networking.NetworkOperators;

namespace HotspotYue
{
    /// <summary>某一时刻热点的快照。</summary>
    public sealed class HotspotSnapshot
    {
        public bool Ok;          // 是否成功读到状态
        public string State;     // On / Off / InTransition / NoProfile / Unknown
        public string Ssid;
        public string Band;
        public string Error;
    }

    /// <summary>开启/关闭操作的结果。</summary>
    public sealed class TetheringResult
    {
        public bool Success;
        public string Message;
    }

    /// <summary>
    /// 直接调用 Windows Runtime 的移动热点 API，全程不经过 PowerShell。
    /// </summary>
    public static class TetheringEngine
    {
        private static NetworkOperatorTetheringManager CreateManager()
        {
            ConnectionProfile profile = NetworkInformation.GetInternetConnectionProfile();
            if (profile == null)
            {
                return null;
            }
            return NetworkOperatorTetheringManager.CreateFromConnectionProfile(profile);
        }

        public static HotspotSnapshot GetSnapshot()
        {
            HotspotSnapshot snapshot = new HotspotSnapshot();
            try
            {
                NetworkOperatorTetheringManager manager = CreateManager();
                if (manager == null)
                {
                    snapshot.Ok = false;
                    snapshot.State = "NoProfile";
                    snapshot.Error = "当前没有可用的网络连接";
                    return snapshot;
                }

                snapshot.State = manager.TetheringOperationalState.ToString();
                snapshot.Ok = true;

                try
                {
                    NetworkOperatorTetheringAccessPointConfiguration config =
                        manager.GetCurrentAccessPointConfiguration();
                    snapshot.Ssid = config.Ssid;
                    snapshot.Band = BandText(config.Band);
                }
                catch (Exception)
                {
                    // 部分状态下读不到配置，不影响保活主流程。
                }
            }
            catch (Exception ex)
            {
                snapshot.Ok = false;
                snapshot.State = "Unknown";
                snapshot.Error = ex.Message;
            }
            return snapshot;
        }

        public static async Task<TetheringResult> StartAsync()
        {
            TetheringResult result = new TetheringResult();
            try
            {
                NetworkOperatorTetheringManager manager = CreateManager();
                if (manager == null)
                {
                    result.Message = "当前没有可用的网络连接，无法开启热点";
                    return result;
                }

                NetworkOperatorTetheringOperationResult operation = await manager.StartTetheringAsync();
                result.Success = operation.Status == TetheringOperationStatus.Success;
                result.Message = StatusText(operation);
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = ex.GetType().Name + "：" + ex.Message;
                return result;
            }
        }

        public static async Task<TetheringResult> StopAsync()
        {
            TetheringResult result = new TetheringResult();
            try
            {
                NetworkOperatorTetheringManager manager = CreateManager();
                if (manager == null)
                {
                    result.Message = "当前没有可用的网络连接";
                    return result;
                }

                NetworkOperatorTetheringOperationResult operation = await manager.StopTetheringAsync();
                result.Success = operation.Status == TetheringOperationStatus.Success;
                result.Message = StatusText(operation);
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = ex.GetType().Name + "：" + ex.Message;
                return result;
            }
        }

        private static string StatusText(NetworkOperatorTetheringOperationResult operation)
        {
            string text = operation.Status.ToString();
            string extra = operation.AdditionalErrorMessage;
            if (extra != null && extra.Length > 0)
            {
                text = text + "（" + extra + "）";
            }
            return text;
        }

        public static string BandText(TetheringWiFiBand band)
        {
            // 不同系统版本里枚举成员名不完全一致，这里按名称判断，避免编译期绑定。
            string name = band.ToString();
            if (name.IndexOf("5", StringComparison.Ordinal) >= 0
                || name.IndexOf("Five", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "5GHz";
            }
            if (name.IndexOf("2", StringComparison.Ordinal) >= 0
                || name.IndexOf("Two", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "2.4GHz";
            }
            return "自动";
        }

        public static string StateText(string state)
        {
            if (state == "On") return "已开启";
            if (state == "Off") return "已关闭";
            if (state == "InTransition") return "切换中";
            if (state == "NoProfile") return "无网络连接";
            return "未知";
        }
    }
}