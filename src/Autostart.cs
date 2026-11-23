using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace HotspotYue
{
    /// <summary>
    /// 开机自启。写当前用户的 Run 键即可，不需要管理员权限，也不会弹 UAC。
    /// </summary>
    public static class Autostart
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "HotspotYue";
        private const string TrayArgument = " --tray";

        private static string ExePath
        {
            get { return Application.ExecutablePath; }
        }

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null)
                    {
                        return false;
                    }
                    string value = key.GetValue(ValueName) as string;
                    return !string.IsNullOrEmpty(value);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void Enable()
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("无法打开注册表自启项");
                }
                key.SetValue(ValueName, "\"" + ExePath + "\"" + TrayArgument, RegistryValueKind.String);
            }
        }

        public static void Disable()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
            {
                if (key != null)
                {
                    key.DeleteValue(ValueName, false);
                }
            }
        }
    }
}