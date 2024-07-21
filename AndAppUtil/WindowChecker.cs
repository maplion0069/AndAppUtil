using System.Runtime.InteropServices;

namespace AndAppUtil {
    public partial class WindowChecker {
        public delegate bool EnumWindowsDelegate(IntPtr hWnd, IntPtr lParam);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool EnumWindows(EnumWindowsDelegate lpEnumFunc, IntPtr lParam);

        [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true, EntryPoint = "GetWindowTextW")]
        private static partial int GetWindowText(IntPtr hWnd, Span<char> lpString, int nMaxCount);

        [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true, EntryPoint = "GetWindowTextLengthW")]
        private static partial int GetWindowTextLength(IntPtr hWnd);

        [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true, EntryPoint = "GetClassNameW")]
        private static partial int GetClassName(IntPtr hWnd, Span<char> lpClassName, int nMaxCount);

        [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        private static partial int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        private static partial IntPtr OpenProcess(uint processAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int processId);

        [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true, EntryPoint = "QueryFullProcessImageNameW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, Span<char> lpExeName, ref int lpdwSize);

        [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int y, int cx, int cy, int uFlags);

        [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool DestroyWindow(IntPtr hWnd);

        [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true, EntryPoint = "SendMessageW")]
        private static partial IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        //        [LibraryImport("dwmapi.dll")]
        //        private static partial long DwmGetWindowAttribute(IntPtr hWnd, uint dwAttribute, out RECT rect, int cbAttribute);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        private static readonly object lockObj = new();

        private static bool isAndAppUpdated;
        private static int andAppProcessId = -1;

        private static bool isV11Updated;
        private static int? v11LastLeft = null;
        private static int? v11LastTop = null;

        public void Check() {
            lock (lockObj) {
                isAndAppUpdated = false;
                isV11Updated = false;

                EnumWindows(new EnumWindowsDelegate(EnumWindowCallBack), IntPtr.Zero);

                if (!isAndAppUpdated) {
                    andAppProcessId = -1;
                }
                if (!isV11Updated) {
                    v11LastLeft = null;
                    v11LastTop = null;
                }
            }
        }

        private static bool EnumWindowCallBack(IntPtr hWnd, IntPtr lparam) {
            // ウィンドウのタイトルの長さを取得
            int windowTitleLength = GetWindowTextLength(hWnd);
            if (windowTitleLength > 0) {
                // プロセス名を取得
                int windowProcessId = 0;
                GetWindowThreadProcessId(hWnd, out windowProcessId);
                IntPtr processHandle = OpenProcess(0x00001000 /* PROCESS_QUERY_LIMITED_INFORMATION  */, false, windowProcessId);
                int processPathCapacity = 32_767; // UNICODE_STRING_MAX_CHARS
                Span<char> processPathBuffer = stackalloc char[processPathCapacity + 1];
                QueryFullProcessImageName(processHandle, 0, processPathBuffer, ref processPathCapacity);
                var processFilePath = processPathBuffer.Slice(0, processPathBuffer.IndexOf('\0')).ToString();
                var processFileName = processFilePath.Split("\\").Last();

                // ウインドウのクラス名を取得
                Span<char> windowClassBuffer = stackalloc char[256];
                GetClassName(hWnd, windowClassBuffer, windowClassBuffer.Length);
                var windowClass = windowClassBuffer.Slice(0, windowClassBuffer.IndexOf('\0')).ToString();

                // ウインドウのタイトルを取得
                Span<char> windowTitleBuffer = stackalloc char[windowTitleLength + 1];
                GetWindowText(hWnd, windowTitleBuffer, windowTitleLength + 1);
                var windowTitle = windowTitleBuffer.Slice(0, windowTitleBuffer.IndexOf('\0')).ToString();

                // アプリ固有の処理
                switch (processFileName, windowClass, windowTitle) {
                    // AndApp本体は最小化
                    case var (fileName, className, titleName) when fileName.Equals("andApp.exe", StringComparison.OrdinalIgnoreCase) && className.StartsWith("Chrome_WidgetWin_") && titleName == "AndApp":
                        if (andAppProcessId != windowProcessId) {
                            andAppProcessId = windowProcessId;
                            Task.Run(() => {
                                Thread.Sleep(500);
                                ShowWindow(hWnd, 0x00000006 /* SW_MINIMIZE */);
                            });
                        }
                        isAndAppUpdated = true;
                        break;

                    // AndAppの広告は閉じる
                    case var (fileName, className, titleName) when fileName.Equals("andApp.exe", StringComparison.OrdinalIgnoreCase) && className.StartsWith("Chrome_WidgetWin_"): // && titleName.Contains("キャンペーン")
                        System.Diagnostics.Debug.WriteLine($"className: {className}");
                        System.Diagnostics.Debug.WriteLine($"titleName: {titleName}");
                        Task.Run(() => {
                            Thread.Sleep(500);
                            SendMessage(hWnd, 0x00000010 /* WM_CLOSE */, 0, 0);
                        });
                        break;

                    // びびびはウインドウを移動
                    case var (fileName, className, titleName) when fileName.Equals("venus11_andapp.exe", StringComparison.OrdinalIgnoreCase) && className == "UnityWndClass" && titleName == "Vイレブン":
                        RECT windowBounds = new();
                        GetWindowRect(hWnd, out windowBounds);
                        // DwmGetWindowAttribute(hWnd, 9 /* DWMWA_EXTENDED_FRAME_BOUNDS */, out windowBounds, Marshal.SizeOf(typeof(RECT)));
                        if (v11LastLeft == null || v11LastTop == null) {
                            // TODO: 設定ファイルから直近の座標を取得して、SetWindowPos
                            var screenBounds = System.Windows.Forms.Screen.PrimaryScreen?.Bounds;
                            if (screenBounds != null) {
                                Task.Run(() => {
                                    Thread.Sleep(500);
                                    SetWindowPos(
                                        hWnd,
                                        0,
                                        screenBounds.Value.Width - (windowBounds.right - windowBounds.left) - 48, // TODO: 設定ファイルから前回の座標を取得。今は右下。マルチディスプレイもタスクバーも考慮なし。
                                        screenBounds.Value.Height - (windowBounds.bottom - windowBounds.top) - 48, // TODO: 設定ファイルから前回の座標を取得。今は右下。マルチディスプレイもタスクバーも考慮なし。
                                        0,
                                        0,
                                        0x0001 /* SWP_NOSIZE */ | 0x0004 /* SWP_NOZORDER */ | 0x0200 /* SWP_NOOWNERZORDER */
                                    );

                                    // TODO: 画面外だったら再調整
                                    RECT adjustBounds = new();
                                    GetWindowRect(hWnd, out adjustBounds);
                                    if (adjustBounds.right >= screenBounds.Value.Width || adjustBounds.bottom >= screenBounds.Value.Height) {
                                        Thread.Sleep(250);
                                        SetWindowPos(
                                            hWnd,
                                            0,
                                            screenBounds.Value.Width - (adjustBounds.right - adjustBounds.left) - 48, // TODO: 設定ファイルから前回の座標を取得。今は右下。マルチディスプレイもタスクバーも考慮なし。
                                            screenBounds.Value.Height - (adjustBounds.bottom - adjustBounds.top) - 48, // TODO: 設定ファイルから前回の座標を取得。今は右下。マルチディスプレイもタスクバーも考慮なし。
                                            0,
                                            0,
                                            0x0001 /* SWP_NOSIZE */ | 0x0004 /* SWP_NOZORDER */ | 0x0200 /* SWP_NOOWNERZORDER */
                                        );
                                    }
                                });
                            }
                            GetWindowRect(hWnd, out windowBounds);
                        }
                        else {
                            // TODO: ウインドウ座標を取得して、設定ファイルに保存
                        }

                        // TODO: v11LastLeftとv11LastTopに直近の座標をセット
                        isV11Updated = true;
                        v11LastLeft = windowBounds.left;
                        v11LastTop = windowBounds.top;
                        break;

                    // Fences3のアップデート通知は閉じる
                    case var (fileName, className, titleName) when fileName.Equals("SdDisplay.exe", StringComparison.OrdinalIgnoreCase) && titleName.Contains("Fences"):
                        System.Diagnostics.Debug.WriteLine($"className: {className}");
                        System.Diagnostics.Debug.WriteLine($"titleName: {titleName}");
                        Task.Run(() => {
                            Thread.Sleep(500);
                            SendMessage(hWnd, 0x00000010 /* WM_CLOSE */, 0, 0);
                        });
                        break;
                }
            }

            return true;
        }
    }
}
