using Avalonia;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading;

namespace WeaselSettings.Avalonia;

internal static class Program
{
    private static readonly Mutex Mutex = new(true, "{8F6F0AC4-B9A1-45fd-A8CF-0XF04E6BDE8F}");
    private static bool _firstRun;

    [STAThread]
    public static void Main(string[] args)
    {
        if (!Mutex.WaitOne(TimeSpan.Zero, true))
        {
            ActivateExistingWindow();
            return;
        }

        string installDir = YamlHelper.GetInstallDir();
        string rimeUserDir = YamlHelper.GetUserDataDir();
        string dataDir = Path.Combine(rimeUserDir, "tables", "custom", "wubi.dict.yaml");
        string fontPath = Path.Combine(rimeUserDir, "font", "zigen.ttf");

        if (File.Exists(dataDir))
        {
            // 与原版一致：程序必须部署在 Rime 安装目录下运行
            string currentDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            if (!string.Equals(currentDirectory, installDir.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
#if WINDOWS
                MessageBoxW(IntPtr.Zero,
                    $"非法运行位置！程序必须在指定目录下运行。\n当前目录：{currentDirectory}",
                    "安全检查", 0x10 /* MB_ICONERROR */);
#endif
                Environment.Exit(0);
                return;
            }
        }
        else
        {
            // 首次运行：解压 data.zip 并安装字根字体
            string zipPath = Path.Combine(AppContext.BaseDirectory, "data.zip");
            if (!File.Exists(zipPath))
            {
#if WINDOWS
                MessageBoxW(IntPtr.Zero, $"错误：找不到 data.zip 文件。\n{zipPath}", "错误", 0x10);
#endif
                Environment.Exit(1);
                return;
            }

            ExtractZip(zipPath, rimeUserDir);
#if WINDOWS
            InstallFont(fontPath);
#endif
            _firstRun = true;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        // 与原版一致：首次初始化时，设置窗口关闭后等待 2 秒再执行部署
        if (_firstRun)
        {
            Thread.Sleep(2000);
            try
            {
                YamlHelper.RunDeploy();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("部署失败: " + ex.Message);
            }
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();

    private static void ExtractZip(string zipPath, string rimeUserDir)
    {
        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string fullPath = Path.GetFullPath(Path.Combine(rimeUserDir, entry.FullName));

            // 安全校验：防止 Zip Slip 攻击
            if (!fullPath.StartsWith(Path.GetFullPath(rimeUserDir), StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(fullPath);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                entry.ExtractToFile(fullPath, overwrite: true);
            }
        }
    }

    #region 单实例

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    private static void ActivateExistingWindow()
    {
        var current = Process.GetCurrentProcess();

        foreach (var process in Process.GetProcessesByName(current.ProcessName))
        {
            if (process.Id != current.Id)
            {
                IntPtr handle = process.MainWindowHandle;
                if (handle != IntPtr.Zero)
                {
                    ShowWindow(handle, SW_RESTORE);
                    SetForegroundWindow(handle);
                }
                break;
            }
        }
    }

    #endregion

    #region 字体安装（Windows）

#if WINDOWS
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    [DllImport("gdi32.dll", EntryPoint = "AddFontResourceW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int AddFontResource(string lpszFilename);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam,
                                                    uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    private const uint WM_FONTCHANGE = 0x001D;
    private const uint SMTO_ABORTIFHUNG = 0x0002;
    private static readonly IntPtr HWND_BROADCAST = new(0xffff);

    public static void InstallFont(string sourceFile)
    {
        string fontName = "通用字根";
        string destinationPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
            Path.GetFileName(sourceFile));

        try
        {
            if (File.Exists(destinationPath))
                return;

            File.Copy(sourceFile, destinationPath, true);

            string registryKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts";
            using (Microsoft.Win32.RegistryKey? key =
                   Microsoft.Win32.Registry.LocalMachine.OpenSubKey(registryKey, true))
            {
                key?.SetValue(fontName + " (TrueType)", Path.GetFileName(sourceFile));
            }

            int fontsAdded = AddFontResource(destinationPath);
            if (fontsAdded > 0)
            {
                SendMessageTimeout(HWND_BROADCAST, WM_FONTCHANGE, IntPtr.Zero, IntPtr.Zero,
                                   SMTO_ABORTIFHUNG, 1000, out _);
            }
        }
        catch (UnauthorizedAccessException)
        {
            MessageBoxW(IntPtr.Zero,
                "字体安装失败：权限不足。请以管理员身份运行此配置工具。",
                "权限错误", 0x10);
        }
        catch (Exception ex)
        {
            MessageBoxW(IntPtr.Zero, $"字体安装发生异常:\n{ex.Message}", "错误", 0x10);
        }
    }
#endif

    #endregion
}
