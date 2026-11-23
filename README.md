# HotspotYue · 热点保活器

Win11 的移动热点被荣耀工作台之类的程序周期性掐断，这个工具负责盯着它、发现断了就立刻拉回来。

单文件 exe，**34 KB，零依赖**（.NET Framework 4.8 是 Win11 自带的），双击即用，不需要装 .NET SDK、不需要 Visual Studio。

## 下载

[![下载 HotspotYue.exe](https://img.shields.io/badge/下载-HotspotYue.exe-2ea44f?style=flat-square&logo=windows11&logoColor=white)](https://github.com/YueXizheng/HotspotYue/releases/latest/download/HotspotYue.exe)

- 直链：<https://github.com/YueXizheng/HotspotYue/releases/latest/download/HotspotYue.exe>
- 版本页：[Releases](https://github.com/YueXizheng/HotspotYue/releases)
- 当前版本：`v1.1.0`

下载后双击即可运行，无需安装。文件校验：

```
SHA256  39106CFA9D68FB369E412369F660F78BF61C9386DBA2611F44E3977BAD8A7FAD
```

![界面](screenshot.png)


## 目录结构

```
HotspotYue/
├── src/
│   ├── Program.cs          单实例、命令行参数
│   ├── MainForm.cs         界面、托盘、保活循环
│   ├── TetheringEngine.cs  WinRT 热点读写
│   ├── Logger.cs           内存环形日志
│   ├── Autostart.cs        HKCU Run 自启
│   ├── WinRtAwait.cs       IAsyncOperation 的 await 支持
│   └── app.manifest        DPI、Win10/11 兼容声明
├── tools/
│   ├── hsctl.cs            外部检查/启停热点的小工具（调试用，非必需品）
│   └── hsctl.exe
├── bin/
│   ├── HotspotYue.exe      最终产物
│   └── HotspotYue.ico
└── build.ps1               一键编译
```

## 使用

1. 双击 `bin\HotspotYue.exe`。
2. 点「开启保活」：会立刻打开热点，之后每 5 秒检查一次。
3. 关掉窗口只是收进托盘，保活继续；要真正退出，右键托盘图标 →「退出」。
4. 勾选「开机自动启动」：写当前用户的 Run 项，下次登录自动进托盘并开始保活。
5. 「清空日志」一键清空内存日志和界面显示（托盘右键菜单里也有同名项）。

托盘图标颜色：绿色 = 热点正常，红色 = 热点断开正在恢复，灰色 = 保活已停止。

## 行为细节

- **检测周期**：默认 5 秒，可调 2–60 秒。运行中改周期立即生效。
- **退避**：连续恢复失败 3 次后，间隔自动拉长到 30 秒，避免和抢热点的程序死循环对打。
- **恢复**：`StartTetheringAsync`，成功后托盘气泡提示一次；失败时只记日志，不弹窗骚扰。
- **日志**：只在状态变化时写，**内核缓冲和界面显示都按 2000 行封顶**，超出后从最旧的行开始丢；默认完全不落盘。需要时点「导出日志」，想立刻归零点「清空日志」。
- **单实例**：重复双击不会开出第二个进程，而是把已有窗口叫出来。

## 重新编译

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

脚本直接调用 Windows 自带的 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`，引用 `System32\WinMetadata` 下的 WinRT 元数据编译。没有 NuGet、没有 SDK、没有联网。

## 实测记录

在Windows11上：

- 标准用户调用 `StartTetheringAsync` / `StopTetheringAsync` 均返回 `Success`，无需管理员权限；
- 外部停止热点模拟被掐断，应用在 6.7 秒内完成 `Off → InTransition → On`；
- 应用日志对应记录：`状态变化：已开启 → 已关闭` → `[警告] 检测到热点未开启，正在自动恢复…` → `[正常] 热点已重新开启。`
- 日志裁剪：写入 2102 行后，界面与内核都稳定在 2000 行，首行变为第 102 行（最旧的 102 行被丢弃）；
- 清空日志：点击后界面只剩一行 `[提示] 日志已清空。`

### 开发笔记：ReadOnly 的坑

`RichTextBox.ReadOnly = true` 时，给 `SelectedText` 赋值会被**静默忽略**——不报错，也不改内容。所以删旧行时必须临时把 `ReadOnly` 置 false，删完再恢复，否则计数器说"已裁剪 2000 行"、界面却还在无限增长。

另外，`GetFirstCharIndexFromLine` 按**显示行**计数，开了 `WordWrap` 后和逻辑行对不上，裁剪位置要用 `\n` 自己数。

## 调试用编译开关

需要压力测试日志裁剪时，可以用 `LOGTEST` 宏编一个会自灌 2100 行日志的版本（不会进入正式产物）：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Output .\bin\HotspotYue.logtest.exe -Define LOGTEST
```

## 已知限制

- 依赖 Win11 的「移动热点」功能（`Windows.Networking.NetworkOperators`）。旧系统的「承载网络」`netsh wlan set hostednetwork` 不适用。
- 没有网络连接时热点无法开启，界面会显示「读取失败」并记录原因。
- 断线恢复靠轮询，所以最短恢复延迟等于检测周期。
- 想彻底清除：取消勾选自启 + 删除 exe 本体即可，程序不写任何注册表以外的位置。

## 许可证

[MIT](LICENSE)

[![](https://img.shields.io/badge/powered_by-dsh-4D6BFE?style=flat-square&logo=deepseek&logoColor=white)](https://github.com/deepseek-ai/deepseek-harness)
