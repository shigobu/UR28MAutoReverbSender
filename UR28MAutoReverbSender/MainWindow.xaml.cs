using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
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

		const int MIDIChannel = 0;
		CancellationTokenSource tokenSource = null;
		CancellationToken token;

		Task MIDIMessageLoop = null;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
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

            //MIDIデバイスの名前取得
            midiInCom.ItemsSource = midiInDeviceEnum();
            midiInCom.SelectedIndex = 0;
		}

        /// <summary>
        /// MIDIメッセージを取得するスレッド
        /// </summary>
        private void MIDILoadThread()
        {
			MIDIIN midiIn = null;
			try
			{
				midiIn = new MIDIIN(GetSelectedDeviceName());
				while (!token.IsCancellationRequested)
				{
					byte[] message = midiIn.GetMIDIMessage();
					if (message.Length == 0)
					{
						Thread.Sleep(1);
						continue;
					}
					switch (message[0])
					{
						//ノートオン
						case 0x90 + MIDIChannel:
							//指定の音階の場合
							if (message[1] == GetNoteNumber())
							{
								ReverbOn();
							}
							break;
						//ノートオフ
						case 0x80 + MIDIChannel:
							//指定の音階の場合
							if (message[1] == GetNoteNumber())
							{
								ReverbOff();
							}
							break;
						default:
							break;
					}
				}
			}
			catch (Exception ex)
			{
				stopButton.PerformClick();
				throw;
			}
			finally
			{
				if (midiIn != null)
				{
					midiIn.Dispose();
				}
			}
        }

		/// <summary>
		/// 選択されているデバイス名を取得します。
		/// </summary>
		/// <returns></returns>
		private string GetSelectedDeviceName()
		{
			if (midiInCom.Dispatcher.CheckAccess())
			{
				return midiInCom.Text;
			}
			else
			{
				return midiInCom.Dispatcher.Invoke<string>(new Func<string>(GetSelectedDeviceName));
			}
		}

		/// <summary>
		/// 入力されているノート番号を取得します。
		/// </summary>
		/// <returns></returns>
		private int GetNoteNumber()
		{
			if (noteNum.Dispatcher.CheckAccess())
			{
				int num = 0;
				int.TryParse(noteNum.Text, out num);
				return num;
			}
			else
			{
				return noteNum.Dispatcher.Invoke<int>(new Func<int>(GetNoteNumber));
			}
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
			Thread.Sleep(100);

            SetCursorPos(rect.Left + (int)OnPoint.X, rect.Top + (int)OnPoint.Y);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
			Thread.Sleep(50);
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
			Thread.Sleep(100);

            SetCursorPos(rect.Left + (int)OffPoint.X, rect.Top + (int)OffPoint.Y);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
			Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
        }

		private void DoButton_Click(object sender, RoutedEventArgs e)
		{
			//別スレッドを起動し、MIDIメッセージの読み込みを開始します。
			//有効無効切り替え
			doButton.IsEnabled = false;
			midiInCom.IsEnabled = false;
			midiInButton.IsEnabled = false;
			noteNum.IsEnabled = false;

			//スレッド開始
			tokenSource = new CancellationTokenSource();
			token = tokenSource.Token;
			MIDIMessageLoop = Task.Run(new Action(MIDILoadThread), token);
			while (!(MIDIMessageLoop.Status == TaskStatus.Running))
			{
				Thread.Sleep(1);
			}
			stopButton.IsEnabled = true;
		}

		private void StopButton_Click(object sender, RoutedEventArgs e)
		{
			//有効無効切り替え
			stopButton.IsEnabled = false;
			tokenSource.Cancel();
			while (MIDIMessageLoop.Status == TaskStatus.Running)
			{
				Thread.Sleep(1);
			}

			MIDIMessageLoop.Dispose();
			MIDIMessageLoop = null;

			doButton.IsEnabled = true;
			midiInCom.IsEnabled = true;
			midiInButton.IsEnabled = true;
			noteNum.IsEnabled =	true;
		}

		private void MidiInButton_Click(object sender, RoutedEventArgs e)
		{
			//MIDIデバイスの名前取得
			midiInCom.ItemsSource = midiInDeviceEnum();
			midiInCom.SelectedIndex = 0;
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

	/// <summary>
	/// ボタンの拡張
	/// </summary>
	public static class ButtonExtensions
	{
		/// <summary>
		/// ボタンのクリックイベントを発生させます。
		/// </summary>
		/// <param name="button"></param>
		public static void PerformClick(this Button button)
		{
			if (button == null)
				throw new ArgumentNullException("button");

			var provider = new ButtonAutomationPeer(button) as IInvokeProvider;
			provider.Invoke();
		}
	}
}
