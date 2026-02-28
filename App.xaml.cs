using System.Windows;
using System;
using System.Runtime.InteropServices;
using Application = System.Windows.Application;

namespace OpenAnt
{
    public partial class App : Application
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        static App()
        {
            try
            {
                SetProcessDpiAwarenessContext(new IntPtr(-5));
            }
            catch
            {
            }
        }
    }
}
