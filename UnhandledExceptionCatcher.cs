using System;
using System.Collections.Generic;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Windows;
using System.Windows.Threading;
//#define USE_TASK_DIALOG

//using Microsoft.WindowsAPICodePack.Dialogs;
//public MainWindow()
//{
//	new MyLibrary.UnhandledExceptionCatcher(Properties.Resources.ResourceManager, true, true);
//	InitializeComponent();
//}

// リソースに設定するローカライズメッセージ
//	  ApplicationName アプリケーション名
//	  TarminateMessageMain 表示メッセージ
//		  問題が発生したため、このプログラムは停止しました。
//	  TarminateMessageContent 表示メッセージ
//		  プログラムを終了します。
//	  TarminateMessageContentOutput 表示メッセージ
//		  プログラムを終了します。{0}この問題の内部情報を表示しますか？

namespace MabiLauncher
{
	public class UnhandledExceptionCatcher
	{
		const string applicationName = "MabiLauncher";
		string tarminateMessageMain = (string)Application.Current.FindResource("tarminateMessageMain");
		string tarminateMessageContent = (string)Application.Current.FindResource("tarminateMessageContent");
		string tarminateMessageContentOutput = (string)Application.Current.FindResource("tarminateMessageContentOutput");

		const string ApplicationNameString = "ApplicationName";
		const string TarminateMessageMainString = "TarminateMessageMain";
		const string TarminateMessageContentString = "TarminateMessageContent";
		const string TarminateMessageContentOutputString = "TarminateMessageContentOutput";

		object lockobj = new object();
		bool alreadyOccurred = false;
		List<Exception> exceptions = new List<Exception>();
		int maxStockExceptions = 100;

		Assembly Assembly;
		AppDomain AppDomain;
		Application Application;
		ResourceManager ResourceManager;
		bool OutputException = false;
		bool OutputExceptionStackTrace = false;

		public UnhandledExceptionCatcher()
			: this(
				Assembly.GetExecutingAssembly(),
				AppDomain.CurrentDomain,
				Application.Current,
				null,
				false,
				false
			) { }

		public UnhandledExceptionCatcher(ResourceManager resourceManager, bool outputExeption, bool outputStackTrace)
			: this(
				Assembly.GetExecutingAssembly(),
				AppDomain.CurrentDomain,
				Application.Current,
				resourceManager,
				outputExeption,
				outputStackTrace
			) { }

		public UnhandledExceptionCatcher(
			Assembly assembly,
			AppDomain appDomain,
			Application application,
			ResourceManager resourceManager,
			bool outputExeption,
			bool outputStackTrace
			)
		{
			this.Assembly = assembly;
			this.AppDomain = appDomain;
			this.Application = application;
			this.ResourceManager = resourceManager;
			this.OutputException = outputExeption;
			this.OutputExceptionStackTrace = outputStackTrace;

			application.DispatcherUnhandledException += (s, ev) =>
			{
				ev.Handled = true;
				this.AddToExceptionList(ev.Exception); // and sleep if not first

				this.ShowReportAndShutdown();
			};

			appDomain.UnhandledException += (s, ev) =>
			{
				this.HaltUiThread();
				this.AddToExceptionList(ev.ExceptionObject as Exception); // and sleep if not first

				this.ShowReportAndShutdown();
			};
		}

		private void HaltUiThread()
		{
			this.Application.Dispatcher.BeginInvoke(
				new Action(() =>
				{
					while (true) System.Threading.Thread.Sleep(1000);
				}),
				DispatcherPriority.Send
			);
		}

		void AddToExceptionList(Exception exception)
		{
			bool isNotFirst = false;
			lock (this.lockobj)
			{
				isNotFirst = this.alreadyOccurred;
				this.alreadyOccurred = true;
				if (this.maxStockExceptions-- > 0)
				{
					this.exceptions.Add(exception);
				}
			}

			if (isNotFirst)
			{
				while (true) System.Threading.Thread.Sleep(1000);
			}
		}

		static string HeaderLine(string x)
		{
			return x + new string('-', 80 - x.Length);
		}
		static string HeaderNumAndLine(string x, int i)
		{
			string h = string.Format("#{0}:{1}", i, x);
			return h + new string('-', 80 - h.Length);
		}
			  
		void ShowReportAndShutdown()
		{
			try
			{
				string appName = this.GetResourceString(ApplicationNameString);
				if (string.IsNullOrWhiteSpace(appName))
				{
					var productAttribute = this.GetAssemblyAttribute<AssemblyProductAttribute>();
					appName = (productAttribute != null) ? productAttribute.Product : string.Empty;
				}

				string messageMain = this.GetResourceString(TarminateMessageMainString) ?? tarminateMessageMain;
				messageMain = string.Format(messageMain, Environment.NewLine);
				string messageContent = OutputException ?
					this.GetResourceString(TarminateMessageContentOutputString) ?? tarminateMessageContentOutput :
					this.GetResourceString(TarminateMessageContentString) ?? tarminateMessageContent;
				messageContent = string.Format(messageContent, Environment.NewLine);

				bool result = this.ShowDialog(appName, messageMain, messageContent);

				if (result)
				{
					StringBuilder builder = new StringBuilder();
					try
					{
						builder.AppendLine(HeaderLine("Date"));
						builder.AppendLine(DateTime.UtcNow.ToString("u"));

						builder.AppendLine(HeaderLine("CommandLine"));
						builder.AppendLine(Environment.CommandLine);
						builder.AppendLine(HeaderLine("Version"));
						builder.AppendLine(System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());
						builder.AppendLine(HeaderLine("CurrentUICulture"));
						builder.AppendLine(System.Threading.Thread.CurrentThread.CurrentUICulture.ToString());
						builder.AppendLine(HeaderLine("OS"));
						builder.AppendLine(
							string.Format("{0} / Is64BitOperatingSystem:{1} / Is64BitProcess:{2}",
							Environment.OSVersion.VersionString,
							Environment.Is64BitOperatingSystem,
							Environment.Is64BitProcess));

						lock (this.lockobj)
						{
							int count = 0;
							foreach (var ex in this.exceptions)
							{
								count++;
								WriteExceptionDetail(builder, count, 0, ex);
							}
						}
						builder.AppendLine();
						builder.AppendLine();
						builder.AppendLine();
					}
					catch { }
					finally
					{
						System.Diagnostics.Debug.WriteLine(builder.ToString());
#if !DEBUG
						try
						{
							string path = System.IO.Path.Combine(
								System.IO.Path.GetTempPath(),
								System.IO.Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0])
								+ ".errorlog.txt");
							System.IO.File.AppendAllText(path, builder.ToString());
						}
						catch { }
#endif
					}
				}
			}
			catch { }
			finally
			{
#if USE_TASK_DIALOG
			Environment.Exit(1);
#else

#endif
			}
		}

		void WriteExceptionDetail(StringBuilder builder, int num, int innerCount, Exception exception)
		{
			string inner = (innerCount > 0) ? string.Format(":Inner{0}", innerCount) : string.Empty;
#if USE_TASK_DIALOG
			TaskDialog td = new TaskDialog();
			td.Icon = TaskDialogStandardIcon.Error;
			td.Caption = exception.Source ;
			td.InstructionText =exception.GetType().ToString();
			td.Text = exception.Message;
			td.StandardButtons = TaskDialogStandardButtons.Close;
#endif
			builder.AppendLine(HeaderNumAndLine("Type of exception" + inner, num));
			builder.AppendLine(exception.GetType().ToString());
			builder.AppendLine(HeaderNumAndLine("Exception.Message" + inner, num));
			builder.AppendLine(exception.Message);
			builder.AppendLine(HeaderNumAndLine("Exception.Source" + inner, num));
			builder.AppendLine(exception.Source);
			builder.AppendLine(HeaderNumAndLine("Exception.TargetSite" + inner, num));
			builder.AppendLine((exception.TargetSite != null) ? exception.TargetSite.ToString() : string.Empty);
			if (OutputExceptionStackTrace)
			{
				builder.AppendLine(HeaderNumAndLine("Exception.StackTrace" + inner, num));
				builder.AppendLine(exception.StackTrace);
			}
			builder.AppendLine(HeaderNumAndLine("Exception.Data" + inner, num));
			foreach (var key in exception.Data.Keys)
			{
				if (exception.Data[key] != null) builder.AppendLine(string.Format("{0} = {1}", key.ToString(), exception.Data[key].ToString()));
			}
			if (exception.InnerException != null) WriteExceptionDetail(builder, num, innerCount + 1, exception.InnerException);
#if USE_TASK_DIALOG
			td.DetailsExpandedText = exception.StackTrace;
			td.Show();
#else
			ErrorReporter ew = new ErrorReporter(exception.Message, exception.StackTrace);
			ew.Show();
#endif
		}

		T GetAssemblyAttribute<T>()
		{
			var attrs = this.Assembly.GetCustomAttributes(false);
			foreach (var attr in attrs)
			{
				if (attr.GetType().Equals(typeof(T))) return (T)attr;
			}
			return default(T);
		}

		string GetResourceString(string name)
		{
			try
			{
				if (this.ResourceManager != null) return this.ResourceManager.GetString(name);
			}
			catch { }
			return null;
		}

		bool ShowDialog(string title, string main, string content)
		{
			try
			{
#if USE_TASK_DIALOG
				TaskDialog td = new TaskDialog();
				td.Icon = TaskDialogStandardIcon.Error;
				td.Caption = title;
				td.InstructionText = main;
				td.Text = content;
				td.StandardButtons =
					TaskDialogStandardButtons.Yes |
					TaskDialogStandardButtons.Close;

				TaskDialogResult result = td.Show();

				return (result.ToString() == "Yes") ? true : false;
#else
				var result = MessageBox.Show(string.Format("{1}{0}{0}{0}{2}{0}", Environment.NewLine, main, content), title,
						OutputException ?
							MessageBoxButton.YesNo :
							MessageBoxButton.OK,
						MessageBoxImage.Error);
				return (result == MessageBoxResult.Yes);
	  
#endif
			}
			catch(Exception e)
			{
				Console.WriteLine(e);
			}
			return false;
		}
	}
}
