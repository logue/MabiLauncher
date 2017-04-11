/*!
 * Mabinogi Patcher Class
 * Copyright (C) 2012,2017 by Logue <http://logue.be/>
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License version 3
 * as published by the Free Software Foundation.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 * 
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;
using Microsoft.VisualBasic.Devices;

namespace MabiLauncher
{
	sealed class Patcher
	{
		private string self_dir = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath) + "\\";
		private string PatchFileListName;

		private UInt32 To;
		private UInt32 From;
		private string Host;
		private string TempDir;
		private DirectoryInfo TempDirInfo;
		private string Remote;
		private string ExtractDir;
		private DirectoryInfo ExtractedDirInfo;

		private Dictionary<string, string> DownloadList;
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="s">サーバーのURL</param>
        /// <param name="f">クライアント側ののバージョン</param>
        /// <param name="t">サーバーのバージョン</param>
		public Patcher(Uri s, UInt32 f, UInt32 t){
			this.From = f;
			this.To = t;
			this.Host = s.ToString();
			this.TempDir = self_dir + "_mltemp_\\" + this.To.ToString() + "\\";
			this.Remote = Host + "/" + this.To.ToString() + "/";

			this.TempDirInfo = new DirectoryInfo(this.TempDir);
			this.ExtractDir = self_dir + "_mltemp_\\extracted\\";
			this.ExtractedDirInfo = new DirectoryInfo(this.ExtractDir);
		}
        /// <summary>
        /// デストラクタ
        /// </summary>
		~Patcher(){
            // 解凍したファイルをディレクトリごと削除する
			if (ExtractedDirInfo.Exists == true)
				this.ExtractedDirInfo.Delete(true);
            // 一時ファイルディレクトリを削除するb
			if (TempDirInfo.Exists == true)
				this.TempDirInfo.Delete(true);
			DirectoryInfo di = new DirectoryInfo(self_dir + "_mltemp_");
			if (di.Exists == true)
				di.Delete(true);
		}
        /// <summary>
        /// パッチを取得する
        /// </summary>
        /// <returns></returns>
		public bool Patch()
		{
            // 初期化
            Initialize();
            // 言語パックをダウンロード
			GetLanguagePack(this.To);
            // ダウンロードが完了したときの処理
            if (Downloader() == true)
			{
                // 結合
				Merge();
                // 回答
				Extract();
				return true;
			}
			return false;
		}
        /// <summary>
        /// パッチを当てる
        /// </summary>
        /// <param name="From">ローカルのバージョン</param>
        /// <param name="To">サーバーのバージョン</param>
        /// <returns></returns>
		public bool Patch(uint From, uint To){
			this.From = From;
			this.To = To;
			Initialize();
			GetLanguagePack(this.To);
			if (Downloader() == true){
				Merge();
				Extract();
				return true;
			}
			return false;
		}
        /// <summary>
        /// 一時ディレクトリの作成
        /// </summary>
		private void DirectorySetup(){
			// 一時ディレクトリ
			if (TempDirInfo.Exists == false)
			{
				TempDirInfo.Create();
			}
			else
			{
				TempDirInfo.Delete(true);
				TempDirInfo.Create();
			}
            // 解凍ディレクトリ
			if (ExtractedDirInfo.Exists == false)
			{
				ExtractedDirInfo.Create();
			}
			else
			{
				ExtractedDirInfo.Delete(true);
				ExtractedDirInfo.Create();
			}
		}
		/// <summary>
        /// 初期化
        /// </summary>
        /// <returns>成功したか？</returns>
		private bool Initialize() {
			DirectorySetup();
            // サーバー上に存在するパッチ定義ファイルの命名規則
            // ローカルのバージョンが500でサーバーのバージョンが505だった場合、500_to_505.txtを取得する
 
			PatchFileListName = From.ToString() + "_to_" + To.ToString();
			// 1, Get main_version download list
			// [from]_to_[to].txt
 
			ProgressDialog pd = new ProgressDialog();
			pd.Title = "Mabinogi Patcher";
			//pd.Caption = "1, Initializing...";
			//pd.Message = "Get main_version download list...";
			pd.Caption = (string)Application.Current.FindResource("patcherInit");
			//pd.Detail = String.Format("Checking {0}.txt...", this.PatchFileListName);
			pd.Message = String.Format((string)Application.Current.FindResource("patcherInitCheck"), this.PatchFileListName + ".txt");
			pd.Detail = Remote + this.PatchFileListName + ".txt";
			pd.ShowDialog(ProgressDialog.PROGDLG.MarqueeProgress);
            // キャンセルボタンが押されたときのバンドら
			if (pd.HasUserCancelled)
			{
				pd.CloseDialog();
				return false;
			}

            // サーバー上に存在するパッチファイルの定義を取得する
            Console.WriteLine("Checking {0}.txt...", this.PatchFileListName);
			if (!Download(Remote + this.PatchFileListName + ".txt", TempDir))
			{
				pd.Value = 2;
                // 存在しない場合は、５
				PatchFileListName = this.To.ToString() + "_full";
				pd.Detail = Remote + this.PatchFileListName + ".txt";
				pd.Message = String.Format((string)Application.Current.FindResource("patcherInitCheck"), this.PatchFileListName + ".txt");
				Console.WriteLine("Checking {0}.txt...", this.PatchFileListName);
				if (!Download(Remote + this.PatchFileListName + ".txt", TempDir))
				{
					pd.Value = 3;
					pd.CloseDialog();
					return false;
				}
			}
			pd.CloseDialog();
			return true;
		}

		public bool GetLanguagePack(uint version){
			DirectorySetup();
			ProgressDialog pd = new ProgressDialog();
			string language_pack = version.ToString() + "_language.p_";
			string language_pack_req = this.Host + "/" + version.ToString() + "/" + language_pack;
			pd.Title = "Mabinogi Patcher";
			pd.Caption = (string)Application.Current.FindResource("patcherLangPack");
			pd.Message = language_pack_req;
			pd.ShowDialog();

			try{	// Korean server does not exsists language.pack
				File.Delete(TempDir + "language.zip");
				Download(language_pack_req, TempDir);
				File.Move(TempDir + language_pack, TempDir + "language.zip");

				if (File.Exists(TempDir + "language.zip"))
				{
					UnzipFile(TempDir + "language.zip", self_dir + "Package\\");
				}
				return true;
			}
			catch(Exception e)
			{
				Console.WriteLine(e);
				return false;
			}
			finally
			{
				pd.CloseDialog();
			}
		}

		private bool Downloader(){
			ProgressDialog pd = new ProgressDialog();
			pd.Title = "Mabinogi Patcher";
			//pd.Caption = "2, Downloading partial patch file and check md5sum...";
			pd.Caption = (string)Application.Current.FindResource("patcherDownloader");
			// 3, Download partial patch file and check md5sum
			this.DownloadList = PatchFileList(TempDir + PatchFileListName + ".txt");
			pd.Maximum = (uint)DownloadList.Count;
			pd.Value = 0;
			int i = 0;
			pd.ShowDialog();
			try{
				foreach (KeyValuePair<string, string> pair in DownloadList)
				{
					if (pd.HasUserCancelled){
						pd.CloseDialog();
						return false;
					}
					else
					{
						//pd.Message = String.Format("Download Patch file ({0} / {1})", i, DownloadList.Count);
						pd.Message = String.Format((string)Application.Current.FindResource("patcherDownloaderDownload"), i, DownloadList.Count);
						pd.Detail = TempDir + pair.Key;
						Console.WriteLine(pair.Key);
						Console.WriteLine(pair.Value);
						String partialfile = TempDir + pair.Key;
						if (!File.Exists(partialfile)){
							do 
							{
								Download(Remote + pair.Key, TempDir);
							} while (pair.Value == MD5Sum(TempDir + pair.Key));
						}
						else if (pair.Value != MD5Sum(TempDir + pair.Key))
						{
							File.Delete(TempDir + pair.Key);
							do 
							{
								Download(Remote + pair.Key, TempDir);
							} while (pair.Value == MD5Sum(TempDir + pair.Key));
						}
						else
						{
							Console.WriteLine("Skipping...");
						}
						i++;
						pd.Value = (uint)i;
					}
					
				}
				pd.CloseDialog();
				return true;
			}
			catch(Exception ex)
			{
				pd.Message = ex.ToString();
				pd.CloseDialog();
				return false;
			}
		}

		private void Merge(){
			// 4, Combine partial patch file.
			ProgressDialog pd = new ProgressDialog();
			pd.Title = "Mabinogi Patcher";
			//pd.Caption = "3, Combine";
			pd.Caption = (string)Application.Current.FindResource("patcherMerge");
			pd.Value = 0;
			pd.Maximum = (uint)DownloadList.Count;
			pd.ShowDialog();
			
			int j = 0;

			//const int buffSize = 16384;
			using (FileStream fs = new FileStream(TempDir + this.PatchFileListName + ".zip", FileMode.Create)){
				foreach (KeyValuePair<string, string> pair in DownloadList)
				{
					//pd.Message = String.Format("Margeing partial patch file... ({0} / {1})", j, DownloadList.Count);
					pd.Message = String.Format((string)Application.Current.FindResource("patcherMargeMargeing"), j, DownloadList.Count);
					Console.WriteLine("Marge: " + pair.Key);
					string partialFile = TempDir + pair.Key;
					using (FileStream part = new FileStream(partialFile, FileMode.Open)){
						int buffSize = (int)part.Length;
						byte[] buffer = new byte[buffSize];
				
						long tr;
						int r;
						r = part.Read(buffer, 0, buffSize);
						tr = r;
						while (r != 0)
						{
							pd.Detail =  String.Format("{0} ({1} / {2})", pair.Key ,tr , part.Length);
							fs.Write(buffer, 0, r);
							r = part.Read(buffer, 0, buffSize);
							tr += r;
						}
					}
					j++;
					pd.Value = (uint)j;
				}
			}
			pd.CloseDialog();
		}

		private void Extract(){
			UnzipFile(TempDir + this.PatchFileListName + ".zip", ExtractDir);

			CopyDirectory(ExtractDir, self_dir, true);
			
			// 6, Modify Version.dat
			byte[] b = BitConverter.GetBytes(this.To);
			File.WriteAllBytes("version.dat", b);
		}

		private Dictionary<string, string> PatchFileList(string filename)
		{
			Console.WriteLine(filename);
			string line = "";
			ArrayList al = new ArrayList();
			using (StreamReader sr = new StreamReader(filename, Encoding.GetEncoding("UTF-8")))
			{
				while ((line = sr.ReadLine()) != null)
				{
					al.Add(line);
				}
			}
			Dictionary<string, string> ret = new Dictionary<string, string>();
			for (int i = 1; i < al.Count; i++)	// Ignore 1st line
			{
				string[] info = al[i].ToString().Split(',');
				ret.Add(info[0], info[2]);	// filename , hash
			}
			return ret;
		}
		private static bool Download(String file_url, String output_path, bool verbose = true)
		{
			Uri uri = new Uri(file_url);

			string request = ( ((uri.Port == 80) ? "http://" : "ftp://") + uri.Host + uri.AbsolutePath).ToString();
			string filename = output_path + Path.GetFileName(request);
			Console.WriteLine(request + " -> " + filename);

			Network network = new Network();
			if (uri.UserInfo != "")
			{
				// FTP Download
				string[] userinfos = uri.UserInfo.Split(':');

				try
				{
					network.DownloadFile(
						request, 
						filename,
						userinfos[0], 
						userinfos[1],
						verbose, 
						6000, 
						true,
						Microsoft.VisualBasic.FileIO.UICancelOption.DoNothing
					);
				}
				catch(Exception e)
				{
					Console.WriteLine(e);
					return false;
				}
			
			}
			else
			{
				// HTTP Download
				try 
				{
					network.DownloadFile(
						request,
						filename,
						"", 
						"",
						verbose, 
						6000, 
						true,
						Microsoft.VisualBasic.FileIO.UICancelOption.DoNothing
					);
				}
				catch (Exception e)
				{
					Console.WriteLine(e);
					return false;
				}
			}
 
			if (File.Exists(filename))
			{
				return true;
			}
			else
			{
				return false;
			}
		}
		private static string MD5Sum(string filename)
		{
			if (File.Exists(filename)){
				FileInfo file = new FileInfo(filename);
				FileStream fs = new FileStream(file.FullName, FileMode.Open);
				string md5sum = BitConverter.ToString(System.Security.Cryptography.MD5.Create().ComputeHash(fs)).ToLower().Replace("-", "");
				fs.Close();
				return md5sum;
			}else{
				return null;
			}
		}
		private static bool UnzipFile(string inputFileName, string destinationPath)
		{
			Console.WriteLine("Extract : "+inputFileName);
			
			if ( !Directory.Exists(destinationPath) )
				Directory.CreateDirectory(destinationPath);


			ProgressDialog pd = new ProgressDialog();
			pd.Title = "Mabinogi Patcher";
			//pd.Caption = "3, Combine";
			pd.Caption = (string)Application.Current.FindResource("patcherExtract");
			pd.Value = 0;

			ZipStorer zip = ZipStorer.Open(inputFileName, FileAccess.Read);

			// Read all directory contents
			List<ZipStorer.ZipFileEntry> dir = zip.ReadCentralDir();
			pd.Maximum = (uint)dir.Count;
			pd.ShowDialog();

			// Extract all files in target directory
			string path;
			bool result = false;
			foreach (ZipStorer.ZipFileEntry entry in dir)
			{
                String fileName = entry.FilenameInZip;
                path = Path.Combine(destinationPath, fileName);
				pd.Message = String.Format((string)Application.Current.FindResource("patcherExtractExtracting"), pd.Value, dir.Count);
				pd.Detail = fileName;
                

                if (fileName.EndsWith("/"))
                {
					if ( !Directory.Exists(path.ToString()) )
						Directory.CreateDirectory(path.ToString());
				}
				else
				{
					result = zip.ExtractFile(entry, path);
				}
				Console.WriteLine(String.Format("{0} / {1} {2}",pd.Value, dir.Count, entry.FilenameInZip));
				pd.Value++;
			}
			zip.Close();
			pd.CloseDialog();
			return true;
		}
		// http://jeanne.wankuma.com/tips/csharp/directory/copy.html
		/// <summary>
		///     ファイルまたはディレクトリ、およびその内容を新しい場所にコピーします。<summary>
		/// <param name="stSourcePath">
		///     コピー元のディレクトリのパス。</param>
		/// <param name="stDestPath">
		///     コピー先のディレクトリのパス。</param>
		/// <param name="bOverwrite">
		///     コピー先が上書きできる場合は true。それ以外の場合は false。</param>
		/// ------------------------------------------------------------------------------------
		private static void CopyDirectory(string stSourcePath, string stDestPath, bool bOverwrite)
		{
			// コピー先のディレクトリがなければ作成する
			if (!Directory.Exists(stDestPath))
			{
				Directory.CreateDirectory(stDestPath);
				File.SetAttributes(stDestPath, File.GetAttributes(stSourcePath));
				bOverwrite = true;
			}

			// コピー元のディレクトリにあるすべてのファイルをコピーする
			if (bOverwrite)
			{
				foreach (string stCopyFrom in Directory.GetFiles(stSourcePath))
				{
					string stCopyTo = Path.Combine(stDestPath, Path.GetFileName(stCopyFrom));
					File.Copy(stCopyFrom, stCopyTo, true);
				}

				// 上書き不可能な場合は存在しない時のみコピーする
			}
			else
			{
				foreach (string stCopyFrom in System.IO.Directory.GetFiles(stSourcePath))
				{
					string stCopyTo = Path.Combine(stDestPath, Path.GetFileName(stCopyFrom));

					if (!File.Exists(stCopyTo))
					{
						File.Copy(stCopyFrom, stCopyTo, false);
					}
				}
			}

			// コピー元のディレクトリをすべてコピーする (再帰)
			foreach (string stCopyFrom in Directory.GetDirectories(stSourcePath))
			{
				string stCopyTo = Path.Combine(stDestPath, Path.GetFileName(stCopyFrom));
				CopyDirectory(stCopyFrom, stCopyTo, bOverwrite);
			}
		}
	}
}
