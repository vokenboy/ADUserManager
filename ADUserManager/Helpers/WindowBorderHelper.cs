using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ActiveManager.Helpers;

internal static class WindowBorderHelper
{
    private const int DwmWindowAttributeBorderColor = 34;
    private const int DialogBorderColor = 0x00A8862B;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);

    public static void ApplyDialogBorder(Window window)
    {
        window.SourceInitialized += OnSourceInitialized;
        return;

        void OnSourceInitialized(object? sender, EventArgs e)
        {
            window.SourceInitialized -= OnSourceInitialized;

            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
                return;

            var color = DialogBorderColor;
            _ = DwmSetWindowAttribute(handle, DwmWindowAttributeBorderColor, ref color, sizeof(int));
        }
    }
}
