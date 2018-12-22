using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MIDIIOCSWrapper;

namespace UR28MAutoReverbSender
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("USER32.dll", CallingConvention = CallingConvention.StdCall)]
        static extern void SetCursorPos(int X, int Y);

        [DllImport("USER32.dll", CallingConvention = CallingConvention.StdCall)]
        static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

        private const uint MK_LBUTTON = 0x0001;
        private const uint MK_CONTROL = 0x0008;

        private const int MOUSEEVENTF_LEFTDOWN = 0x2;
        private const int MOUSEEVENTF_LEFTUP = 0x4;

        IntPtr handle = IntPtr.Zero;
        Process pro = null;
        Point OnPoint = new Point(40, 200);
        Point OffPoint = new Point(24, 210);

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            midiInCom.ItemsSource = midiInDeviceEnum();
            midiInCom.SelectedIndex = 0;

            string processName = "dspMixFx_UR28M";
            Process[] pros = Process.GetProcessesByName(processName);
            foreach (var item in pros)
            {
                pro = item;
                break;
            }
            if (pro == null)
            {
                MessageBox.Show("dspMixFx_UR28Mのウィンドウを取得できませんでした。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
                return;
            }
            handle = pro.MainWindowHandle;
        }

        /// <summary>
        /// すべてのMIDIInデバイスの名前を取得します。
        /// </summary>
        /// <returns></returns>
        private string[] midiInDeviceEnum()
        {
            List<string> names = new List<string>();
            int inNum = MIDIIN.GetDeviceNum();
            for (int i = 0; i < inNum; i++)
            {
                names.Add(MIDIIN.GetDeviceName(i));
            }
            return names.ToArray();
        }

        /// <summary>
        /// リバーブをONにします
        /// </summary>
        private void ReverbOn()
        {
            RECT rect;
            bool err = GetWindowRect(handle, out rect);
            if (!err)
            {
                MessageBox.Show("dspMixFx_UR28Mの場所を取得できませんでした。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Microsoft.VisualBasic.Interaction.AppActivate(pro.Id);
            System.Threading.Thread.Sleep(100);

            SetCursorPos(rect.Left + (int)OnPoint.X, rect.Top + (int)OnPoint.Y);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
        }

        /// <summary>
        /// リバーブをOFFにします
        /// </summary>
        private void ReverbOff()
        {
            RECT rect;
            bool err = GetWindowRect(handle, out rect);
            if (!err)
            {
                MessageBox.Show("dspMixFx_UR28Mの場所を取得できませんでした。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Microsoft.VisualBasic.Interaction.AppActivate(pro.Id);
            System.Threading.Thread.Sleep(100);

            SetCursorPos(rect.Left + (int)OffPoint.X, rect.Top + (int)OffPoint.Y);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
        }

    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;        // x position of upper-left corner
        public int Top;         // y position of upper-left corner
        public int Right;       // x position of lower-right corner
        public int Bottom;      // y position of lower-right corner
    }
}
