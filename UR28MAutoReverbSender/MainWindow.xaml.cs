using MIDIIOCSWrapper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.IO;

namespace UR28MAutoReverbSender
{
	/// <summary>
	/// MainWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class MainWindow : Window
	{
		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

		[DllImport("USER32.dll", CallingConvention = CallingConvention.StdCall)]
		private static extern void SetCursorPos(int X, int Y);

		[DllImport("USER32.dll", CallingConvention = CallingConvention.StdCall)]
		private static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

		private const uint MK_LBUTTON = 0x0001;
		private const uint MK_CONTROL = 0x0008;

		private const int MOUSEEVENTF_LEFTDOWN = 0x2;
		private const int MOUSEEVENTF_LEFTUP = 0x4;

		private IntPtr handle = IntPtr.Zero;
		private Process pro = null;
		private Point OnPoint = new Point(40, 200);
		private Point OffPoint = new Point(24, 210);

		private const int MIDIChannel = 0;
		private CancellationTokenSource tokenSource = null;
		private CancellationToken token;

		private Task MIDIMessageLoop = null;

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
				MessageBox.Show("dspMixFx_UR28Mのウィンドウを取得できませんでした。\n終了します。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
				this.Close();
				return;
			}
			handle = pro.MainWindowHandle;

			//MIDIデバイスの名前取得
			midiInCom.ItemsSource = midiInDeviceEnum();
			midiInCom.SelectedIndex = 0;

			//設定ファイル読み込み
			//exeパス取得
			string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;
			//ディレクトリ取得
			string thisAssemblyDirectory = Path.GetDirectoryName(thisAssemblyPath);
			//設定ファイルパス作成
			string settingFilePath = Path.Combine(thisAssemblyDirectory, "SettingData.txt");
			if (File.Exists(settingFilePath))
			{
				try
				{
					//設定ファイル読み込み
					string[] settingData = File.ReadAllLines(settingFilePath);
					//設定
					midiInCom.SelectedIndex = int.Parse(settingData[0]);
					noteNum.Text = settingData[1];
				}
				catch (Exception)
				{
					//何もしない
				}
			}
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
				//有効無効切り替え
				SetEnableEnd(false);
				SetEnableStart(true);
				MessageBox.Show(ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
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
				throw new Exception("dspMixFx_UR28Mの場所を取得できませんでした。\nアプリケーションの再起動をしてください。");
			}

			Microsoft.VisualBasic.Interaction.AppActivate(pro.Id);
			Thread.Sleep(30);

			SetCursorPos(rect.Left + (int)OnPoint.X, rect.Top + (int)OnPoint.Y);
			mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
			Thread.Sleep(30);
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
				throw new Exception("dspMixFx_UR28Mの場所を取得できませんでした。\nアプリケーションの再起動をしてください。");
			}

			Microsoft.VisualBasic.Interaction.AppActivate(pro.Id);
			Thread.Sleep(30);

			SetCursorPos(rect.Left + (int)OffPoint.X, rect.Top + (int)OffPoint.Y);
			mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
			Thread.Sleep(30);
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
			SetEnableEnd(false);

			//スレッド終了
			tokenSource.Cancel();
			while (!MIDIMessageLoop.IsCompleted)
			{
				Thread.Sleep(1);
			}

			MIDIMessageLoop.Dispose();
			MIDIMessageLoop = null;

			SetEnableStart(true);
		}

		/// <summary>
		/// 開始ボタンの有効無効を変更します。
		/// </summary>
		/// <param name="enable"></param>
		private void SetEnableStart(bool enable)
		{
			if (doButton.Dispatcher.CheckAccess())
			{
				doButton.IsEnabled = enable;
				midiInCom.IsEnabled = enable;
				midiInButton.IsEnabled = enable;
				noteNum.IsEnabled = enable;
			}
			else
			{
				doButton.Dispatcher.Invoke(new Action<bool>(SetEnableStart), enable);
			}
		}

		/// <summary>
		/// 終了ボタンの有効無効を変更します。
		/// </summary>
		/// <param name="enable"></param>
		private void SetEnableEnd(bool enable)
		{
			if (doButton.Dispatcher.CheckAccess())
			{
				stopButton.IsEnabled = false;
			}
			else
			{
				stopButton.Dispatcher.Invoke(new Action<bool>(SetEnableEnd), enable);
			}
		}

		private void MidiInButton_Click(object sender, RoutedEventArgs e)
		{
			//MIDIデバイスの名前取得
			midiInCom.ItemsSource = midiInDeviceEnum();
			midiInCom.SelectedIndex = 0;
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			//スレッド終了
			if (MIDIMessageLoop != null)
			{
				tokenSource.Cancel();
				while (MIDIMessageLoop.Status == TaskStatus.Running)
				{
					Thread.Sleep(1);
				}

				MIDIMessageLoop.Dispose();
				MIDIMessageLoop = null;
			}

			//設定ファイル書き込み
			//exeパス取得
			string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;
			//ディレクトリ取得
			string thisAssemblyDirectory = Path.GetDirectoryName(thisAssemblyPath);
			//設定ファイルパス作成
			string settingFilePath = Path.Combine(thisAssemblyDirectory, "SettingData.txt");

			StreamWriter sw = null;
			try
			{
				//設定書き込み
				sw = new StreamWriter(settingFilePath, false);
				sw.WriteLine(midiInCom.SelectedIndex.ToString());
				sw.WriteLine(noteNum.Text);
			}
			catch (Exception)
			{
				//何もしない
			}
			finally
			{
				if (sw != null)
				{
					sw.Close();
				}
			}
		}

		/// <summary>
		/// ノート・CCラジオボタンチェック変更のイベントハンドラ
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void radio_Checked(object sender, RoutedEventArgs e)
		{
			RadioButton rb = sender as RadioButton;
			if (rb.Equals(noteRadio))
			{
				noteNum.IsEnabled = rb.IsChecked ?? noteNum.IsEnabled;
			}
			else if (rb.Equals(ccRadio))
			{
				ccNum.IsEnabled = rb.IsChecked ?? ccNum.IsEnabled;
			}
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