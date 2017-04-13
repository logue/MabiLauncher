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
using System.Diagnostics;
using Microsoft.VisualBasic.Devices;

namespace MabiLauncher
{
    sealed class Patcher
    {
        // MabiLauncherPlusのあるディレクトリ
        private string self_dir = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath) + "\\";
        // サーバー上に存在するパッチファイルの一覧のファイル名
        private string PatchFileListName;
        // ローカルのバージョン
        private UInt32 To;
        // サーバーのバージョン
        private UInt32 From;
        // サーバーのホスト名
        private string Host;
        // ダウンロード元のアドレス
        private string RemoteFilePath;
        // 一時ディレクトリ
        private string TempDir;
        private DirectoryInfo TempDirInfo;
        // 解凍ディレクトリ        
        private string ExtractDir;
        private DirectoryInfo ExtractedDirInfo;
        // プログレスダイアログ（ProgressDialog.cs参照）
        private ProgressDialog pd;
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="s">サーバーのURL</param>
        /// <param name="f">クライアント側ののバージョン</param>
        /// <param name="t">サーバーのバージョン</param>
		public Patcher(Uri s, UInt32 f, UInt32 t)
        {
            From = f;
            To = t;

            // ホスト名
            Host = s.ToString();
            // サーバー側のパッチファイルが存在するパス（[http|ftp]://[パッチサーバー]/[バージョン]/）
            RemoteFilePath = Host + "/" + To.ToString() + "/";

            // 一時ディレクトリ（[Mabinogi.exeのパス]\_mltemp_\）
            TempDir = self_dir + "_mltemp_\\" + To.ToString() + "\\";
            TempDirInfo = new DirectoryInfo(TempDir);
            // 解凍先ディレクトリ（[Mabinogi.exeのパス]\_mltemp_\extracted）
            ExtractDir = self_dir + "_mltemp_\\extracted\\";
            ExtractedDirInfo = new DirectoryInfo(ExtractDir);

            // ウィンドウハンドルを取得
            var handle = Process.GetCurrentProcess().MainWindowHandle;
            // プログレスバーのダイアログを定義。タイトルはMabinogi Patcherに固定
            pd = new ProgressDialog(handle);

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
        /// デストラクタ
        /// </summary>
		~Patcher()
        {
            // プログレスダイアログを閉じる
            pd.CloseDialog();
            // 解凍したファイルをディレクトリごと削除する
            if (ExtractedDirInfo.Exists == true)
                ExtractedDirInfo.Delete(true);
            // 一時ファイルディレクトリを削除する
            if (TempDirInfo.Exists == true)
                TempDirInfo.Delete(true);
        }
        /// <summary>
        /// パッチを取得する
        /// </summary>
        /// <param name="From">ローカルのバージョン</param>
        /// <param name="To">サーバーのバージョン</param>
        /// <returns>成否</returns>
		public void Patch(uint From, uint To)
        {
            this.From = From;
            this.To = To;

            GetLanguagePack(To);
            DownloadPatchFiles(GetPatchList());
            Extract();
        }
        private string GetPatchList()
        {
            // 1, Get main_version download list
            // [from]_to_[to].txt
            //pd.Caption = "1, Initializing...";
            pd.Caption = (string)Application.Current.FindResource("patcherInit");
            //pd.Message = "Get main_version download list...";
            pd.Message = String.Format(
                (string)Application.Current.FindResource("patcherInitCheck"), PatchFileListName);
            //pd.Detail = String.Format("Checking {0}.txt...", this.PatchFileListName);
            pd.Detail = RemoteFilePath + PatchFileListName + ".txt";
            pd.ShowDialog(
                ProgressDialog.PROGDLG.MarqueeProgress, 
                ProgressDialog.PROGDLG.NoCancel
            );

            // サーバー上に存在するパッチ定義ファイルの命名規則
            // 例：ローカルのバージョンが500でサーバーのバージョンが505だった場合、500_to_505.txtを取得する
            PatchFileListName = From.ToString() + "_to_" + To.ToString() + ".txt";
            Console.WriteLine("Checking {0}.txt...", PatchFileListName);
            pd.Detail = RemoteFilePath + PatchFileListName;

            // 差分パッチをダウンロードする
            if (!Download(RemoteFilePath + PatchFileListName, TempDir))
            {
                // できなかった場合はフルパッチを取得する
                // 差分の定義ファイルが見つからない場合、全てのファイルをダウンロードする（500_full.txt）
                PatchFileListName = To.ToString() + "_full.txt";
                Console.WriteLine("Checking {0}.txt...", PatchFileListName);
                pd.Detail = RemoteFilePath + PatchFileListName;
                Download(RemoteFilePath + PatchFileListName, TempDir);
            }

            pd.CloseDialog();
            return TempDir + PatchFileListName;
        }
        /// <summary>
        /// 言語パックのダウンロード
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public bool GetLanguagePack(uint version)
        {
            string languagePackFileName = version.ToString() + "_language.p_";
            string remoteLanguagePackFileName = Host + "/" + version.ToString() + "/" + languagePackFileName;

            if (!Download(remoteLanguagePackFileName, TempDir))
            {
                // Korean server does not exsists language.pack
                return false;
            }
 
            pd.Caption = (string)Application.Current.FindResource("patcherLangPack");
            pd.Message = remoteLanguagePackFileName;
            pd.ShowDialog(
                ProgressDialog.PROGDLG.Modal,
                ProgressDialog.PROGDLG.MarqueeProgress,
                ProgressDialog.PROGDLG.NoMinimize
            );

            File.Move(TempDir + languagePackFileName, TempDir + "language.zip");
            UnzipFile(TempDir + "language.zip", self_dir + "Package\\");

            pd.CloseDialog();
            return true;
        }
        /// <summary>
        /// パッチファイルをダウンロードする
        /// </summary>
        /// <returns></returns>
        private void DownloadPatchFiles(string DownloadFileList)
        {
            Dictionary<string, string> DownloadList = PatchFileList(DownloadFileList);

            //pd.Caption = "2, Downloading partial patch file and check md5sum...";
            pd.Caption = (string)Application.Current.FindResource("patcherDownloader");
            // 3, Download partial patch file and check md5sum
            pd.Maximum = (uint)DownloadList.Count;
            pd.Value = 0;

            if (pd.HasUserCancelled)
            {
                pd.CloseDialog();
                return;
            }
 
            int i = 0;
            pd.ShowDialog(
                ProgressDialog.PROGDLG.Modal,
                ProgressDialog.PROGDLG.AutoTime,
                ProgressDialog.PROGDLG.NoMinimize
            );

            foreach (KeyValuePair<string, string> pair in DownloadList)
            {
                // 分割されたパッチファイル
                string partialfile = TempDir + pair.Key;
                //pd.Message = String.Format("Download Patch file ({0} / {1})", i, DownloadList.Count);
                pd.Message = String.Format(
                    (string)Application.Current.FindResource("patcherDownloaderDownload"), i, DownloadList.Count);
                //pd.Detail = partialfile;

                Console.WriteLine(pair.Key);
                Console.WriteLine(pair.Value);

                if (!File.Exists(partialfile))
                {
                    // ダウンロード
                    do
                    {
                        Download(RemoteFilePath + pair.Key, TempDir);
                    } while (pair.Value == MD5Sum(TempDir + pair.Key));
                    // MD5チェック
                    // TODO: こっちの処理に来ると無限ループの可能性あり）
                    if (pair.Value != MD5Sum(partialfile))
                    {
                        File.Delete(TempDir + pair.Key);
                        do
                        {
                            Download(RemoteFilePath + pair.Key, TempDir);
                        } while (pair.Value == MD5Sum(TempDir + pair.Key));
                    }
                }
                else
                {
                    Console.WriteLine("Skipping...");
                }
                i++;
                pd.Value = (uint)i;
            }
            pd.CloseDialog();

            // マージ処理
            pd.Caption = (string)Application.Current.FindResource("patcherMerge");
            pd.Value = 0;
            pd.ShowDialog(
                ProgressDialog.PROGDLG.Modal,
                ProgressDialog.PROGDLG.AutoTime,
                ProgressDialog.PROGDLG.NoMinimize
            );

            int j = 0;
            //const int buffSize = 16384;
            using (FileStream fs = new FileStream(TempDir + PatchFileListName + ".zip", FileMode.Create))
            {
                foreach (KeyValuePair<string, string> pair in DownloadList)
                {
                    //pd.Message = String.Format("Margeing partial patch file... ({0} / {1})", j, DownloadList.Count);
                    pd.Message = String.Format(
                        (string)Application.Current.FindResource("patcherMargeMargeing"), j, DownloadList.Count);
                    Console.WriteLine("Merge: " + pair.Key);
                    // サーバー上の分割されたファイル名
                    string partialFile = TempDir + pair.Key;
                    // 結合処理（単純に前後のファイルの中身をくっつける）
                    using (FileStream part = new FileStream(partialFile, FileMode.Open))
                    {
                        int buffSize = (int)part.Length;
                        byte[] buffer = new byte[buffSize];

                        long tr;
                        int r;
                        r = part.Read(buffer, 0, buffSize);
                        tr = r;
                        while (r != 0)
                        {
                            //pd.Detail = String.Format("{0} ({1} / {2})", pair.Key, tr, part.Length);
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
        /// <summary>
        /// 解答してVersion.datを更新
        /// </summary>
        private void Extract()
        {
            // 一時フォルダに解凍
            UnzipFile(TempDir + PatchFileListName.Replace(".txt", ".zip"), ExtractDir);
            // 解凍したディレクトリをMabinogi.exeと同じディレクトリに上書きコピー
            CopyDirectory(ExtractDir, self_dir, true);

            // 6, Modify Version.dat
            byte[] b = BitConverter.GetBytes(To);
            File.WriteAllBytes("version.dat", b);
        }
        /// <summary>
        /// パッチ定義書からダウンロードするファイルの一覧を生成する
        /// </summary>
        /// <param name="filename">パッチリストのファイル</param>
        /// <returns></returns>
        private Dictionary<string, string> PatchFileList(string PatchFile)
        {
            Console.WriteLine(PatchFile);
            string line = "";
            ArrayList al = new ArrayList();

            if (!File.Exists(PatchFile))
            {
                Console.WriteLine("Not Found");
                Download(RemoteFilePath + PatchFileListName + ".txt", TempDir);
            }

            using (StreamReader sr = new StreamReader(PatchFile, Encoding.GetEncoding("UTF-8")))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    al.Add(line);
                }
            }
            Dictionary<string, string> ret = new Dictionary<string, string>();
            for (int i = 1; i < al.Count; i++)  // Ignore 1st line
            {
                string[] info = al[i].ToString().Split(',');
                ret.Add(info[0], info[2]);  // filename , hash
            }
            return ret;
        }
        /// <summary>
        /// ファイルのダウンロード
        /// </summary>
        /// <param name="file_url">ファイルURL</param>
        /// <param name="output_path">ローカル側の出力先</param>
        /// <returns></returns>
        private static bool Download(String file_url, String output_path)
        {
            bool verbose = false;
#if(DEBUG)
            verbose = true;
#endif
            Uri uri = new Uri(file_url);
            string request = (((uri.Port == 80) ? "http://" : "ftp://") + uri.Host + uri.AbsolutePath).ToString();
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
                catch (System.Net.WebException)
                {
                    Console.WriteLine("Not Found: " + filename);
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
                catch (System.Net.WebException)
                {
                    Console.WriteLine("Not Found: " + filename);
                    return false;
                }
            }

            if (File.Exists(filename))
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// ファイルのMD5ハッシュを取得
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        private static string MD5Sum(string filename)
        {
            if (!File.Exists(filename))
            {
                throw new FileNotFoundException();
            }
            FileInfo file = new FileInfo(filename);
            FileStream fs = new FileStream(file.FullName, FileMode.Open);
            string md5sum = BitConverter.ToString(
                System.Security.Cryptography.MD5.Create().ComputeHash(fs)
                ).
                ToLower().
                Replace("-", "");
            fs.Close();
            return md5sum;
        }
        /// <summary>
        /// 圧縮ファイルの解凍
        /// </summary>
        /// <param name="inputFileName">入力ファイル名</param>
        /// <param name="destinationPath">出力先のパス</param>
        /// <returns></returns>
        private bool UnzipFile(string inputFileName, string destinationPath)
        {
            Console.WriteLine("Extract : " + inputFileName);

            // 解凍先のディレクトリが存在しない場合作成する
            if (!Directory.Exists(destinationPath))
                Directory.CreateDirectory(destinationPath);

            // ZipStorerを用いて圧縮ファイルを開く
            ZipStorer zip = ZipStorer.Open(inputFileName, FileAccess.Read);
            // Read all directory contents
            List<ZipStorer.ZipFileEntry> dir = zip.ReadCentralDir();

            //pd.Caption = "3, Combine";
            pd.Caption = (string)Application.Current.FindResource("patcherExtract");
            pd.Value = 0;
            pd.Maximum = (uint)dir.Count;
            pd.ShowDialog();

            // Extract all files in target directory
            string DestinationPath;
            string fileName;
            bool result = false;
            uint i = 0;

            // ipファイルに含まれているファイルを一つ一つ取得する
            foreach (ZipStorer.ZipFileEntry entry in dir)
            {
                // Zipファイルに含まれているファイル名
                fileName = entry.FilenameInZip;
                // 解凍先
                DestinationPath = Path.Combine(destinationPath, fileName);

                pd.Message = String.Format(
                    (string)Application.Current.FindResource("patcherExtractExtracting"), i, dir.Count);
                pd.Detail = fileName;

                if (fileName.EndsWith("/"))
                {
                    // ディレクトリだった（末尾が/）場合、ディレクトリを作成
                    if (!Directory.Exists(DestinationPath.ToString()))
                        Directory.CreateDirectory(DestinationPath.ToString());
                }
                else
                {
                    // ファイルを解凍
                    result = zip.ExtractFile(entry, DestinationPath);
                }
                Console.WriteLine(String.Format("{0} / {1} {2}", pd.Value, dir.Count, entry.FilenameInZip));
                i++;
            }
            zip.Close();
            zip.Dispose();

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
        private void RefleshProgressDialog()
        {
            pd.Title = "Mabinogi Patcher";
            pd.Caption = "";
            pd.Maximum = 100;
            pd.Value = 0;
            pd.Message = "";
            pd.Detail = "";
        }
    }
}
