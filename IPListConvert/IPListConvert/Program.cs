using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Configuration;
using System.Reflection;

namespace IPListConvert
{
    class Program
    {
        static void Main(string[] args)
        {
            string str = @"
[info] 実行すると自動的にアドレス割り当てリストをRIRよりダウンロードし
[info] 分類しやすいように変換をかけます。
[info] 特に引数は使用しません。
[info] ダウンロード先のURLはappSettingsに書いてあります。
[info] 実行例: IPListConvert.exe

";
            IPListConverter con = new IPListConverter();
            con.Convert();
        }
    }

    /// <summary>
    /// IPリストを変換
    /// </summary>
    public class IPListConverter
    {
        private const string DOWNLOAD_FOLDER = "UrlList";
        private class IPData
        {
            public string country { get; set; }
            public string startIP { get; set; }
            public string endIP { get; set; }
            public uint startIpNum { get; set; }
            public uint endIpNum { get; set; }
        }
        private List<IPData> dataList = new List<IPData>();


        public void Convert()
        {
            Console.WriteLine("[DownLoad] Start");
            Download();
            Console.WriteLine("[DownLoad] End");

            Console.WriteLine("[DataRead] Start");
            DataRead();
            Console.WriteLine("[DataRead] End");

            Console.WriteLine("[FileOutput] Start");
            FileOutput();
            Console.WriteLine("[FileOutput] End");
        }

        /// <summary>
        /// ファイルからデータを整形する
        /// </summary>
        public void DataRead()
        {
            string[] files = Directory.GetFiles(DOWNLOAD_FOLDER, "*.txt", SearchOption.AllDirectories);

            string line;
            foreach (string path in files)
            {
                using (StreamReader sr = new StreamReader(path))
                {
                    while ((line = sr.ReadLine()) != null)
                    {
                        string[] data = line.Split('|');
                        if (data == null) continue;
                        if (data.Length != 8) continue;
                        if (data[0].IndexOf("#") == 1) continue; //コメント行
                        if (string.IsNullOrEmpty(data[1])) continue; //国別コードが空文字
                        if (data[1].Equals("*")) continue; //国別コードが*
                        if (data[2].Equals("ipv4") == false) continue; //ipv4じゃない

                        IPData tmp = new IPData();
                        tmp.country = data[1];
                        tmp.startIP = data[3];

                        //IPAddressを数値に変換
                        IPAddress startAdd = IPAddress.Parse(tmp.startIP);
                        Byte[] byteStartIP = startAdd.GetAddressBytes();
                        Array.Reverse(byteStartIP);
                        tmp.startIpNum = BitConverter.ToUInt32(byteStartIP, 0);

                        //末尾の数値を個数から計算
                        tmp.endIpNum = tmp.startIpNum + uint.Parse(data[4]) - 1;//X.X.X.Xから何個という表記のため自分の分を抜く

                        //アドレス化
                        Byte[] byteEndIP = BitConverter.GetBytes(tmp.endIpNum);
                        Array.Reverse(byteEndIP);
                        IPAddress endAdd = new IPAddress(byteEndIP);
                        tmp.endIP = endAdd.ToString();

                        dataList.Add(tmp);
                    }
                }
            }

            dataList.Sort((a, b) =>
            {
                return a.startIpNum.CompareTo(b.startIpNum);
            });
        }

        /// <summary>
        /// コンバートしたリストを出力する
        /// </summary>
        /// <param name="filePath"></param>
        public void FileOutput(string filePath = "")
        {
            if (string.IsNullOrEmpty(filePath)) filePath = "convert.txt";

            string output = "";

            IPData temp = new IPData();
            PropertyInfo[] proInfo = temp.GetType().GetProperties();

            foreach (PropertyInfo info in proInfo)
            {
                output += info.Name + ",";
            }
            output = output.Remove(output.Length-1, 1);
            output += "\n";

            foreach (IPData tmp in dataList)
            {
                output += string.Format("{0},{1},{2},{3},{4}\n", tmp.country, tmp.startIP, tmp.endIP, tmp.startIpNum, tmp.endIpNum);
            }

            using (StreamWriter sw = new StreamWriter("convert.txt", false, System.Text.Encoding.GetEncoding("shift_jis")))
            {
                sw.Write(output);
            }
        }

        /// <summary>
        /// 国別コードが書かれたＩＰリストを取得する
        /// </summary>
        public void Download()
        {
            //保存先のディレクトリを作成する
            if (!Directory.Exists(DOWNLOAD_FOLDER))
            {
                Directory.CreateDirectory(DOWNLOAD_FOLDER);
            }

            string urlData = ConfigurationManager.AppSettings["UrlList"];
            string[] urlList = urlData.Split(',');

            //各URLからすべて持ってくる
            foreach( string url in urlList)
            {
                Console.WriteLine("[DownLoad] Target:" + url);
                Uri u = new Uri(url);

                //ダウンロードするファイル名
                string downloadFileName = DOWNLOAD_FOLDER + "\\" + Path.GetFileName(url) + ".txt";

                //ファイルが存在するならスキップ
                if(File.Exists(downloadFileName)) continue;

                //リクエストを作成
                FtpWebRequest ftpWebReq = (FtpWebRequest)WebRequest.Create(u);
                ftpWebReq.Method = WebRequestMethods.Ftp.DownloadFile;

                ftpWebReq.KeepAlive = false;   //完了後接続閉じる
                ftpWebReq.UseBinary = true;    //バイナリモードで
                ftpWebReq.UsePassive = false;　//アクティブ利用

                //ファイルをダウンロードするためのStreamを取得
                using (FtpWebResponse ftpWebRes = (FtpWebResponse)ftpWebReq.GetResponse())
                {
                    using (Stream resStream = ftpWebRes.GetResponseStream())
                    {
                        //ファイル書き込み
                        using (FileStream fs = new FileStream(downloadFileName, FileMode.Create, FileAccess.Write))
                        {
                            byte[] buffer = new byte[4096];
                            while (true)
                            {
                                int readSize = resStream.Read(buffer, 0, buffer.Length);
                                if (readSize == 0)
                                    break;
                                fs.Write(buffer, 0, readSize);
                            }
                        }
                    }
                }
            }
        }
    }
}
