using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;

#if USE_TASK_DIALOG
using Microsoft.WindowsAPICodePack.Dialogs;
#endif
namespace MabiLauncher
{
	/// <summary>
	/// Logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private MabiEnvironment env;
		private Patcher p;
		private uint Local;
		private uint Server;
		private string sublocale;
		private string locale;
		private string password = "娄固制作";
		public MainWindow()
		{
			this.InitializeComponent();
			new UnhandledExceptionCatcher(Properties.Resources.ResourceManager, true, true);
			this.comboBox.SelectedIndex = Properties.Settings.Default.LocaleId;
			this.checkBoxAutoboot.IsChecked = Properties.Settings.Default.AutoBoot;
			this.idBox.Text = Properties.Settings.Default.ID;
			if (Properties.Settings.Default.Pass != "" )
			{
				this.passwordBox.Password = DecryptString(Properties.Settings.Default.Pass, this.password);
			}
			this.textBlockVersion.Text = String.Format(" v.{0}", Assembly.GetExecutingAssembly().GetName().Version.ToString());
		}
		private void checkVersionValue() {
			this.Server = UInt32.Parse(this.textBoxServer.Text);
			this.Local = UInt32.Parse(this.textBoxLocal.Text);
			if (this.Local == this.Server)
			{
				this.buttonPatcher.IsEnabled = false;
			}
			else if (this.Server <= this.Local)
			{
				this.Infomation(
					////"Version value is invalued.", 
					////"Server version value does not to be smaller than Local version value.");
					(string)Application.Current.FindResource("luancherInvaluedVersion"),
					(string)Application.Current.FindResource("luancherInvaluedVersionMsg")
				);
			} else {
				this.buttonPatcher.IsEnabled = true;
			}
		}

		/// <summary>
		/// 文字列を暗号化する
		/// </summary>
		/// <param name="sourceString">暗号化する文字列</param>
		/// <param name="password">暗号化に使用するパスワード</param>
		/// <returns>暗号化された文字列</returns>
		public static string EncryptString(string sourceString, string password)
		{
			//RijndaelManagedオブジェクトを作成
			System.Security.Cryptography.RijndaelManaged rijndael =
				new System.Security.Cryptography.RijndaelManaged();

			//パスワードから共有キーと初期化ベクタを作成
			byte[] key, iv;
			GenerateKeyFromPassword(
				password, rijndael.KeySize, out key, rijndael.BlockSize, out iv);
			rijndael.Key = key;
			rijndael.IV = iv;

			//文字列をバイト型配列に変換する
			byte[] strBytes = System.Text.Encoding.UTF8.GetBytes(sourceString);

			//対称暗号化オブジェクトの作成
			System.Security.Cryptography.ICryptoTransform encryptor =
				rijndael.CreateEncryptor();
			//バイト型配列を暗号化する
			//復号化に失敗すると例外CryptographicExceptionが発生
			byte[] encBytes = encryptor.TransformFinalBlock(strBytes, 0, strBytes.Length);
			//閉じる
			encryptor.Dispose();

			//バイト型配列を文字列に変換して返す
			return System.Convert.ToBase64String(encBytes);
		}

		/// <summary>
		/// 暗号化された文字列を復号化する
		/// </summary>
		/// <param name="sourceString">暗号化された文字列</param>
		/// <param name="password">暗号化に使用したパスワード</param>
		/// <returns>復号化された文字列</returns>
		public static string DecryptString(string sourceString, string password)
		{
			//RijndaelManagedオブジェクトを作成
			System.Security.Cryptography.RijndaelManaged rijndael =
				new System.Security.Cryptography.RijndaelManaged();

			//パスワードから共有キーと初期化ベクタを作成
			byte[] key, iv;
			GenerateKeyFromPassword(
				password, rijndael.KeySize, out key, rijndael.BlockSize, out iv);
			rijndael.Key = key;
			rijndael.IV = iv;

			//文字列をバイト型配列に戻す
			byte[] strBytes = System.Convert.FromBase64String(sourceString);

			//対称暗号化オブジェクトの作成
			System.Security.Cryptography.ICryptoTransform decryptor =
				rijndael.CreateDecryptor();
			//バイト型配列を復号化する
			byte[] decBytes = decryptor.TransformFinalBlock(strBytes, 0, strBytes.Length);
			//閉じる
			decryptor.Dispose();

			//バイト型配列を文字列に戻して返す
			return System.Text.Encoding.UTF8.GetString(decBytes);
		}

		/// <summary>
		/// パスワードから共有キーと初期化ベクタを生成する
		/// </summary>
		/// <param name="password">基になるパスワード</param>
		/// <param name="keySize">共有キーのサイズ（ビット）</param>
		/// <param name="key">作成された共有キー</param>
		/// <param name="blockSize">初期化ベクタのサイズ（ビット）</param>
		/// <param name="iv">作成された初期化ベクタ</param>
		private static void GenerateKeyFromPassword(string password,
			int keySize, out byte[] key, int blockSize, out byte[] iv)
		{
			//パスワードから共有キーと初期化ベクタを作成する
			//saltを決める
			byte[] salt = System.Text.Encoding.UTF8.GetBytes("$1$RJW24BzZ$ko6Mfw/SL2NW3RH846xfN1");
			//Rfc2898DeriveBytesオブジェクトを作成する
			System.Security.Cryptography.Rfc2898DeriveBytes deriveBytes =
				new System.Security.Cryptography.Rfc2898DeriveBytes(password, salt);
			//.NET Framework 1.1以下の時は、PasswordDeriveBytesを使用する
			//System.Security.Cryptography.PasswordDeriveBytes deriveBytes =
			//    new System.Security.Cryptography.PasswordDeriveBytes(password, salt);
			//反復処理回数を指定する デフォルトで1000回
			deriveBytes.IterationCount = 1000;

			//共有キーと初期化ベクタを生成する
			key = deriveBytes.GetBytes(keySize / 8);
			iv = deriveBytes.GetBytes(blockSize / 8);
		}

		private void Window_Initialized(object sender, EventArgs e)
		{
			// Parse query strings
			foreach (string cmd in System.Environment.GetCommandLineArgs())
			{
				string[] arg = cmd.Split(':');
				switch (arg[0])
				{
					case "/N":
						// USER ID
						idBox.Text = arg[1];
						break;
					case "/V":
						// PASSWORD
						passwordBox.Password = arg[1];
						break;
					case "/T":
						// sublocale
						this.sublocale = arg[1];
						break;
				}
			}

			if (this.sublocale == "")
			{
				passwordBox.IsEnabled = false;
				idBox.IsEnabled = false;
				checkBoxSavePassword.IsEnabled = false;
			}
			else
			{
				passwordBox.IsEnabled = true;
				idBox.IsEnabled = true;
				checkBoxSavePassword.IsEnabled = true;
			}
			/*
			if (Properties.Settings.Default.AutoBoot)
			{
				this.p = new Patcher(env.PatchServer, env.LocalVersion, env.Version);
				if (!(env.LocalVersion == env.Version))
				{
					p.Patch(env.LocalVersion, env.Version);
				}
				Launch();
			}
			 */

		}

		private void buttonPatcher_Click(object sender, RoutedEventArgs e)
		{
			this.Server = UInt32.Parse(this.textBoxServer.Text);
			this.Local = UInt32.Parse(this.textBoxLocal.Text);
			if (!this.env.isDownloadable) 
			{
				this.Infomation(
					////"Patch Server is offline.", 
					////"Patch Server is currently down. Please try again later.");
					(string)Application.Current.FindResource("luancherServerOffline"),
					(string)Application.Current.FindResource("luancherServerOfflineMsg")
				);
			}
			else if (this.Server <= this.Local)
			{
				this.Infomation(
					////"Version value is invalued.", 
					////"Server version value does not to be smaller than Local version value.");
					(string)Application.Current.FindResource("luancherInvaluedVersion"),
					(string)Application.Current.FindResource("luancherInvaluedVersionMsg")
				);
			}
			else
			{
				if (p.Patch(this.Local, this.Server))
				{
					this.textBoxLocal.Text = this.Server.ToString();
					this.Local = this.Server;
					this.buttonPatcher.IsEnabled = false;
				}
				else
				{
					this.Infomation("Error" ,"Aborted.");
				}
			}
		}

		private void textBoxServer_TextChanged(object sender, TextChangedEventArgs e)
		{
			this.Server = UInt32.Parse(textBoxServer.Text);
		}
		private void textBoxLocal_TextChanged(object sender, TextChangedEventArgs e)
		{
			this.Local = UInt32.Parse(textBoxLocal.Text);
		}

		private void comboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			this.webBrowser.Source = new Uri("about:blank");
			this.gridLauncher.IsEnabled = false;
			bool initial_expander = false;
			if (this.expander.IsExpanded)
			{
				this.expander.IsExpanded = false;
				initial_expander = true;
			}


			XmlElement line = (XmlElement)comboBox.SelectedItem;
			this.env = new MabiEnvironment(line.GetAttribute("patch"));
			this.p = new Patcher(this.env.PatchServer, this.env.LocalVersion, this.env.Version);
			this.textBoxLocal.Text = this.env.LocalVersion.ToString();
			this.textBoxServer.Text = this.env.Version.ToString();
			this.Server = UInt32.Parse(this.textBoxServer.Text);
			this.Local = UInt32.Parse(this.textBoxLocal.Text);
			this.sublocale = line.GetAttribute("sublocale");
			this.locale = line.GetAttribute("locale");

			//Properties.Resources.Culture = CultureInfo.GetCultureInfo(this.locale);

			if (this.sublocale == "")
			{
				this.passwordBox.IsEnabled = false;
				this.idBox.IsEnabled = false;
				this.checkBoxSavePassword.IsEnabled = false;
			} else {
				this.passwordBox.IsEnabled = true;
				this.idBox.IsEnabled = true;
				this.checkBoxSavePassword.IsEnabled = true;
			}

			this.buttonLangPack.IsEnabled = (line.GetAttribute("locale") == "ko") ? false : true;

			Properties.Settings.Default["LocaleId"] = this.comboBox.SelectedIndex;
			Properties.Settings.Default.Save();

			this.webBrowser.Source = new Uri(line.GetAttribute("info"));
			if (this.env.isDownloadable)
			{
				this.buttonLangPack.IsEnabled = true;
				this.buttonLaunch.IsEnabled = true;
				this.buttonPatcher.IsEnabled = true;
				this.textBoxServer.IsEnabled = true;
				this.textBoxLocal.IsEnabled = true;
			}else{
				this.buttonLangPack.IsEnabled = false;
				this.buttonLaunch.IsEnabled = false;
				this.buttonPatcher.IsEnabled = false;
				this.textBoxServer.IsEnabled = false;
				this.textBoxLocal.IsEnabled = false;

			}
			if (initial_expander == true)
			{
				this.expander.IsExpanded = true;
			}
			this.gridLauncher.IsEnabled = true;
		}

		private void buttonLaunch_Click(object sender, RoutedEventArgs e)
		{			
			if (this.Server != this.Local &&
				!this.Confirm(
					"Mabinogi Launcher",
					//// "Local Version and Server Version does not same. Are you sure you want to continue?")
					(string)Application.Current.FindResource("luancherNotSameVersionMsg")
				)
			)
			{
				return;
			}
			else
			{
				this.Launch();
			}
		}

		private void Launch()
		{
			string[] oArgs = new string[3];
			
			Properties.Settings.Default["AutoBoot"] = checkBoxAutoboot.IsChecked;
			if (this.sublocale != "")
			{
				oArgs[0] = "/N:" + this.idBox.Text;
				oArgs[1] = "/V:" + this.passwordBox.Password;
				oArgs[2] = "/T:" + this.sublocale;
				Properties.Settings.Default["ID"] = this.idBox.Text;
				if (this.checkBoxSavePassword.IsChecked == true)
				{
					Properties.Settings.Default["Pass"] = EncryptString(this.passwordBox.Password, this.password);
				}
				else
				{
					Properties.Settings.Default["Pass"] = "";
				}
			}
			Properties.Settings.Default.Save();
			bool isLocal = (this.checkBoxConnectLocal.IsChecked == true) ? true : false;
			if (this.env.Launch(oArgs, null, isLocal))
			{
				Environment.Exit(1);
			}

		}
		private bool Confirm(string caption, string message, string instruction = "")
		{
#if USE_TASK_DIALOG
			TaskDialog td = new TaskDialog();
			TaskDialogStandardButtons button = TaskDialogStandardButtons.Yes;
			button |= TaskDialogStandardButtons.No;
			td.Icon = TaskDialogStandardIcon.Information;
			td.StandardButtons = button;
			td.InstructionText = instruction;
			td.Caption = caption;
			td.Text = messafe;
			TaskDialogResult res = td.Show();

			if (res.ToString() != "Yes")
			{
				return false;
			}
			return true;
#else
			var result = MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Information);
			return (result == MessageBoxResult.Yes);
#endif
		}
		private void Infomation(string caption, string message, string instruction = "")
		{
#if USE_TASK_DIALOG
			TaskDialog td = new TaskDialog();
			TaskDialogStandardButtons button = TaskDialogStandardButtons.Ok;
			td.Icon = TaskDialogStandardIcon.Information;
			td.StandardButtons = button;
			td.InstructionText = instruction;
			td.Caption = caption;
			td.Text = messafe;
			TaskDialogResult res = td.Show();

#else
			var result = MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Information);
#endif
		}

		private void buttonLangPack_Click(object sender, RoutedEventArgs e)
		{
			if (this.p.GetLanguagePack(this.env.Version))
			{
				this.Infomation(
					"language.pack",
					//// "Download successfully."
					(string)Application.Current.FindResource("luancherDownloadSuccess")
				);
			}
			else
			{
				this.Infomation(
					"language.pack",
					//// "Download failure."
					(string)Application.Current.FindResource("luancherDownloadFailure")
				);
			}
		}

		private void checkBoxAutoboot_Click(object sender, RoutedEventArgs e)
		{
			Properties.Settings.Default["AutoBoot"] = checkBoxAutoboot.IsChecked;
			Console.WriteLine(checkBoxAutoboot.IsChecked);
			Properties.Settings.Default.Save();
		}

		private void Close_Click(object sender, RoutedEventArgs e)
		{
			//this.Close();
			Environment.Exit(1);
		}

		private void checkBoxSavePassword_Checked(object sender, RoutedEventArgs e)
		{
			if (checkBoxSavePassword.IsChecked == true)
			{
				this.Infomation(
					"Save Password",
					// "Saving password in local makes security risk, such as account hacking."
					(string)Application.Current.FindResource("luancherSavePassword")
				);
			}
		}

		private void imageLogo_MouseUp(object sender, MouseButtonEventArgs e)
		{
			System.Diagnostics.Process.Start("http://mabiassist.logue.be/MabiLauncher%20Plus");
		}

		private void textBlockCopyrights_MouseUp(object sender, MouseButtonEventArgs e)
		{
			System.Diagnostics.Process.Start("http://logue.be/");
		}

		private void textBlockCopyrights_MouseDown(object sender, MouseButtonEventArgs e)
		{
			System.Diagnostics.Process.Start("http://logue.be/");
		}
	}
}
