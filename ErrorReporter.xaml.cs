using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml;

namespace MabiLauncher
{
	/// <summary>
	/// ErrorReporter.xaml の相互作用ロジック
	/// </summary>
	public partial class ErrorReporter : Window
	{
		public ErrorReporter(string msg, string detail)
		{
			InitializeComponent();
			this.textBoxDetail.Text = detail;
			this.textBoxMessage.Text = msg;
		}

		private void Exit_Click(object sender, RoutedEventArgs e)
		{
			Environment.Exit(-1);

		}

		private void Continue_Click(object sender, RoutedEventArgs e)
		{
			this.Close();
		}

	}
}
