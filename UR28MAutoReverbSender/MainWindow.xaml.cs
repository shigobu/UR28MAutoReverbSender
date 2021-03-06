﻿using MIDIIOCSWrapper;
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

		private int MIDIChannel = 0;
		private CancellationTokenSource tokenSource = null;
		private CancellationToken token;

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

			//読み込み
			LoadData();
		}

        /// <summary>
        /// MIDIメッセージを取得するスレッド
        /// </summary>
		private void MIDILoadThread()
        {
            SetEnableEnd(true);

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

                    //MIDIメッセージ表示
                    PrintMIDIMessage(message);

                    //midiメッセージ毎に分岐
                    //ノートオン
                    if (message[0] == 0x90 + MIDIChannel)
                    {
                        //ノートが選択されていた場合
                        if (GetRadioIsCheckde(noteRadio))
                        {
                            //指定の音階の場合
                            if (message[1] == GetNoteNumber())
                            {
                                //ベロシティーでOnかOffか判定
                                if (message[2] != 0)
                                {
                                    ReverbOn();
                                }
                                else
                                {
                                    ReverbOff();
                                }
                            }
                        }
                    }
                    //ノートオフ
                    else if (message[0] == 0x80 + MIDIChannel)
                    {
                        //ノートが選択されていた場合
                        if (GetRadioIsCheckde(noteRadio))
                        {
                            //指定の音階の場合
                            if (message[1] == GetNoteNumber())
                            {
                                ReverbOff();
                            }
                        }
                    }
                    //コントロールチェンジ
                    else if (message[0] == 0xB0 + MIDIChannel)
                    {
                        //コントロールチェンジ選択時
                        if (GetRadioIsCheckde(ccRadio))
                        {
                            //指定のCC番号の場合
                            if (message[1] == GetCCNumber())
                            {
                                //64以上の(63より多い)場合にOn
                                if (message[2] > 63)
                                {
                                    ReverbOn();
                                }
                                else
                                {
                                    ReverbOff();
                                }
                            }
                        }
                    }
                    else {/*何もしない*/}
                }
            }
            catch
            {
                //例外処理はメインスレッドで行う。そのため、呼び出し元に例外を再スロー。
                throw;
            }
            finally
            {
                if (midiIn != null)
                {
                    midiIn.Dispose();
                    midiIn = null;
                }
                //有効無効切り替え
                SetEnableEnd(false);
                SetEnableStart(true);
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
		/// 入力されているコントロールチェンジ番号を取得します。
		/// </summary>
		/// <returns></returns>
		private int GetCCNumber()
		{
			if (ccNum.Dispatcher.CheckAccess())
			{
				int num = 0;
				int.TryParse(ccNum.Text, out num);
				return num;
			}
			else
			{
				return ccNum.Dispatcher.Invoke<int>(new Func<int>(GetCCNumber));
			}
		}

		/// <summary>
		/// ノートラジオボタンのチェック状態を取得します。
		/// </summary>
		/// <returns></returns>
		private bool GetNoteRadioIsChecked()
		{
			if (noteRadio.Dispatcher.CheckAccess())
			{
				return noteRadio.IsChecked ?? false;
			}
			else
			{
				return noteRadio.Dispatcher.Invoke<bool>(new Func<bool>(GetNoteRadioIsChecked));
			}
		}

		/// <summary>
		/// CCラジオボタンのチェック状態を取得します。
		/// </summary>
		/// <returns></returns>
		private bool GetCCRadioIsChecked()
		{
			if (ccRadio.Dispatcher.CheckAccess())
			{
				return ccRadio.IsChecked ?? false;
			}
			else
			{
				return ccRadio.Dispatcher.Invoke<bool>(new Func<bool>(GetCCRadioIsChecked));
			}
		}

		/// <summary>
		/// GetRadioIsCheckdeで使うデリゲート
		/// </summary>
		/// <param name="radioButton">ラジオボタン</param>
		/// <returns></returns>
		delegate bool GetRadioIsCheckedDelegate(RadioButton radioButton);
		/// <summary>
		/// ラジオボタンのチェック状態を取得します。
		/// </summary>
		/// <param name="checkox">取得するラジオボタン</param>
		/// <returns></returns>
		private bool GetRadioIsCheckde(RadioButton radioButton)
		{
			if (radioButton.Dispatcher.CheckAccess())
			{
				return radioButton.IsChecked ?? false;
			}
			else
			{
				GetRadioIsCheckedDelegate del = GetRadioIsCheckde;
				return (bool)radioButton.Dispatcher.Invoke(del, radioButton);
			}
		}

        /// <summary>
        /// MIDIメッセージを表示します。
        /// </summary>
        /// <param name="MIDIMessage">MIDIメッセージ</param>
        private void PrintMIDIMessage(byte[] MIDIMessage)
        {
            if (MIDIMessageText.Dispatcher.CheckAccess())
            {
                //テキストボックスをクリア
                MIDIMessageText.Text = "";
                //メッセージ出力
                foreach (byte item in MIDIMessage)
                {
                    MIDIMessageText.Text = string.Format("{0}0x{1:x2} ", MIDIMessageText.Text, item);
                }
            }
            else
            {
                MIDIMessageText.Dispatcher.Invoke(new Action<byte[]>(PrintMIDIMessage), MIDIMessage);
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

        /// <summary>
        /// 開始ボタンクリックイベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		private async void DoButton_Click(object sender, RoutedEventArgs e)
		{
			//別スレッドを起動し、MIDIメッセージの読み込みを開始します。
			//有効無効切り替え
			SetEnableStart(false);

            //MIDIメッセージ出力テキスト変更
            MIDIMessageText.Text = "開始";

            //スレッド開始
            tokenSource = null;
            try
            {
                tokenSource = new CancellationTokenSource();
				token = tokenSource.Token;
                await Task.Run(new Action(MIDILoadThread), token);
            }
            //別スレッドで発生した例外の処理
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (tokenSource != null)
                {
                    tokenSource.Dispose();
                    //別メソッドでtokenSourceがnullかどうか参照しているので、nullの代入をする。
                    tokenSource = null;
                }
            }

            //MIDIメッセージ出力テキスト変更
            MIDIMessageText.Text = "終了";
        }
        
        /// <summary>
        /// 終了ボタンクリックイベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StopButton_Click(object sender, RoutedEventArgs e)
		{
            //MIDIタスク終了
            CancelMIDITask();
		}

        /// <summary>
        /// MIDI読み込みタスクにキャンセル要求をします。
        /// </summary>
        private void CancelMIDITask()
        {
            //スレッド終了
            if (tokenSource != null)
            {
                tokenSource.Cancel();
            }
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
				noteRadio.IsEnabled = enable;
				ccNum.IsEnabled = enable;
				ccRadio.IsEnabled = enable;
                midiChCom.IsEnabled = enable;
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
				stopButton.IsEnabled = enable;
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
            //MIDIタスクをキャンセルします
            CancelMIDITask();
            //保存
            SaveData();
		}

		/// <summary>
		/// 保存します。
		/// </summary>
		private void SaveData()
		{
			//設定ファイル書き込み
			//exeパス取得
			string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;
			//ディレクトリ取得
			string thisAssemblyDirectory = Path.GetDirectoryName(thisAssemblyPath);
			//設定ファイルパス作成
			string settingFilePath = Path.Combine(thisAssemblyDirectory, "SettingData.txt");

			using(StreamWriter sw = new StreamWriter(settingFilePath, false))
			{
                //設定書き込み
				sw.WriteLine(midiInCom.SelectedIndex.ToString());
				sw.WriteLine(noteNum.Text);
				sw.WriteLine(ccRadio.IsChecked.ToString());
				sw.WriteLine(ccNum.Text);
                sw.WriteLine(midiChCom.SelectedIndex);
			}
		}

		/// <summary>
		/// 読み込みます。
		/// </summary>
		private void LoadData()
		{
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
					ccRadio.IsChecked = bool.Parse(settingData[2]);
					ccNum.Text = settingData[3];
                    midiChCom.SelectedIndex = int.Parse(settingData[4]);
				}
				catch (Exception)
				{
					//何もしない
                    //設定が適応されないだけ
				}
			}
		}

        /// <summary>
        /// チャンネル選択コンボボックスの選択状態が変わったときに発動する。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MIDIChannel = ((ComboBox)sender).SelectedIndex;
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