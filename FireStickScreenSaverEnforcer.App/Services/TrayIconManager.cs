using System.Runtime.InteropServices;

namespace FireStickScreenSaverEnforcer.App.Services;

/// <summary>
/// Manages a system tray (notification area) icon using native Win32 APIs.
/// No third-party dependencies required.
/// </summary>
internal sealed class TrayIconManager : IDisposable
{
    private const int WM_USER = 0x0400;
    private const int WM_TRAYICON = WM_USER + 1;
    private const int WM_COMMAND = 0x0111;
    private const int WM_DESTROY = 0x0002;

    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;

    private const int NIM_ADD = 0x00;
    private const int NIM_MODIFY = 0x01;
    private const int NIM_DELETE = 0x02;

    private const int NIF_MESSAGE = 0x01;
    private const int NIF_ICON = 0x02;
    private const int NIF_TIP = 0x04;

    private const int MF_STRING = 0x0000;
    private const int MF_SEPARATOR = 0x0800;

    private const int TPM_RIGHTBUTTON = 0x0002;
    private const int TPM_NONOTIFY = 0x0080;
    private const int TPM_RETURNCMD = 0x0100;

    private const int IDM_RESTORE = 1000;
    private const int IDM_EXIT = 1001;

    private const int IMAGE_ICON = 1;
    private const int LR_LOADFROMFILE = 0x0010;
    private const int LR_SHARED = 0x8000;

    private const string TrayWindowClassName = "FireStickEnforcerTrayClass";

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WNDCLASSEX
    {
        public int cbSize;
        public int style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public IntPtr lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA pnid);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle, string lpClassName, string lpWindowName,
        int dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, int uFlags, int uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenu(IntPtr hMenu, int uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr hInst, string name, int type, int cx, int cy, int fuLoad);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private IntPtr _hiddenWindowHandle;
    private IntPtr _iconHandle;
    private NOTIFYICONDATA _notifyIconData;
    private WndProcDelegate? _wndProc;
    private Thread? _messageLoopThread;
    private bool _isCreated;
    private bool _disposed;
    private readonly ManualResetEventSlim _readyEvent = new(false);

    public event Action? RestoreRequested;
    public event Action? ExitRequested;

    public void Create(string tooltip)
    {
        if (_isCreated) return;

        _messageLoopThread = new Thread(() => MessageLoopThreadProc(tooltip))
        {
            IsBackground = true,
            Name = "TrayIconMessageLoop"
        };
        _messageLoopThread.SetApartmentState(ApartmentState.STA);
        _messageLoopThread.Start();

        _readyEvent.Wait(TimeSpan.FromSeconds(5));
    }

    private void MessageLoopThreadProc(string tooltip)
    {
        var hInstance = GetModuleHandle(null);

        // Keep delegate alive for the lifetime of the message loop
        _wndProc = WndProc;

        var wndClass = new WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = hInstance,
            lpszClassName = TrayWindowClassName
        };

        RegisterClassEx(ref wndClass);

        _hiddenWindowHandle = CreateWindowEx(
            0, TrayWindowClassName, "TrayIconWindow", 0,
            0, 0, 0, 0,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

        // Try to load a custom .ico from the app directory, fall back to default app icon
        _iconHandle = TryLoadIcon();

        _notifyIconData = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hiddenWindowHandle,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _iconHandle,
            szTip = tooltip.Length > 127 ? tooltip[..127] : tooltip
        };

        Shell_NotifyIcon(NIM_ADD, ref _notifyIconData);
        _isCreated = true;
        _readyEvent.Set();

        // Win32 message loop
        while (GetMessage(out var msg, IntPtr.Zero, 0, 0))
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_TRAYICON)
        {
            var mouseMsg = (int)lParam;
            if (mouseMsg == WM_LBUTTONDBLCLK)
            {
                RestoreRequested?.Invoke();
            }
            else if (mouseMsg == WM_RBUTTONUP)
            {
                ShowContextMenu(hWnd);
            }
            return IntPtr.Zero;
        }

        if (msg == WM_COMMAND)
        {
            var menuId = (int)(wParam & 0xFFFF);
            switch (menuId)
            {
                case IDM_RESTORE:
                    RestoreRequested?.Invoke();
                    break;
                case IDM_EXIT:
                    ExitRequested?.Invoke();
                    break;
            }
            return IntPtr.Zero;
        }

        if (msg == WM_DESTROY)
        {
            RemoveIcon();
            return IntPtr.Zero;
        }

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void ShowContextMenu(IntPtr hWnd)
    {
        var hMenu = CreatePopupMenu();
        AppendMenu(hMenu, MF_STRING, IDM_RESTORE, "Restore");
        AppendMenu(hMenu, MF_SEPARATOR, 0, string.Empty);
        AppendMenu(hMenu, MF_STRING, IDM_EXIT, "Exit");

        GetCursorPos(out var pt);

        // Required to make the menu disappear when clicking outside
        SetForegroundWindow(hWnd);
        TrackPopupMenu(hMenu, TPM_RIGHTBUTTON, pt.X, pt.Y, 0, hWnd, IntPtr.Zero);
        DestroyMenu(hMenu);
    }

    public void UpdateTooltip(string tooltip)
    {
        if (!_isCreated) return;

        _notifyIconData.szTip = tooltip.Length > 127 ? tooltip[..127] : tooltip;
        _notifyIconData.uFlags = NIF_TIP;
        Shell_NotifyIcon(NIM_MODIFY, ref _notifyIconData);
    }

    private void RemoveIcon()
    {
        if (!_isCreated) return;
        Shell_NotifyIcon(NIM_DELETE, ref _notifyIconData);
        _isCreated = false;
    }

    private static IntPtr TryLoadIcon()
    {
        // Try to load app.ico from the application directory
        var exeDir = AppContext.BaseDirectory;
        var icoPath = Path.Combine(exeDir, "app.ico");
        if (File.Exists(icoPath))
        {
            var hIcon = LoadImage(IntPtr.Zero, icoPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
            if (hIcon != IntPtr.Zero)
                return hIcon;
        }

        // Fall back to extracting the icon from the current executable
        var exePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exePath))
        {
            var hIcon = ExtractIconFromExe(exePath);
            if (hIcon != IntPtr.Zero)
                return hIcon;
        }

        return IntPtr.Zero;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    private static IntPtr ExtractIconFromExe(string exePath)
    {
        return ExtractIcon(IntPtr.Zero, exePath, 0);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_hiddenWindowHandle != IntPtr.Zero)
        {
            RemoveIcon();
            PostMessage(_hiddenWindowHandle, WM_DESTROY, IntPtr.Zero, IntPtr.Zero);
            // Post WM_QUIT to break the message loop
            PostMessage(_hiddenWindowHandle, 0x0012 /* WM_QUIT */, IntPtr.Zero, IntPtr.Zero);
        }

        if (_iconHandle != IntPtr.Zero)
        {
            DestroyIcon(_iconHandle);
            _iconHandle = IntPtr.Zero;
        }

        _readyEvent.Dispose();
    }
}
