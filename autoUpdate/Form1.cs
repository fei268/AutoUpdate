using SharpCompress.Readers;
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

/* 简易自动更新程序设计思路：
 * 主要流程：上传程序更新的压缩包 >> 客户端执行自动更新程序下载 > 解压 > 替换 
 * 1，上传功能集合在一起，通过参数进行功能判断；
 */

namespace autoUpdate
{
    public partial class Form1 : Form
    {
        string exepath = Environment.CurrentDirectory;
        //readonly string connStr = "Data Source=.;Initial Catalog=ExManage;Persist Security Info=True;User ID=sa;Password=123456";
        readonly string connStr = "Data Source=114.168.228.19;Initial Catalog=ExManage;Persist Security Info=True;User ID=sa;Password=ESP_TYO2023!";
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string[] commandLineArgs = Environment.GetCommandLineArgs();
            string arg1 = "";
            string arg2 = "";
            try
            {
                arg1 = commandLineArgs[1];
            }
            catch
            {
            }
            try
            {
                arg2 = commandLineArgs[2];
            }
            catch
            {
            }

            if (arg1 == "admin")
            {
                panel2.Visible = false;
                UpFile();
            }
            else if (arg1.Length > 4 && arg1.Substring(arg1.Length - 4, 4) == ".exe" && arg2.Length > 1)
            {
                panel1.Visible = false;
                DownFile(arg1, arg2);
                this.Close();
            }
            else
            {
                this.Close();
            }

            //DownFile("数据生成导出.exe", "生成数据");
        }

        /// <summary>
        /// 上传更新文件
        /// </summary>
        void UpFile()
        {
        }

        void DownFile(string arg1, string arg2)
        {
            //获取本地版本号
            string nowVer = "";
            string newVer = "";
            try
            {
                using (StreamReader sr = new StreamReader(exepath + "/version.txt"))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        nowVer = line;
                    }
                }
            }
            catch { }

            //获取服务器版本号
            DataTable dt = new DataTable();
            using (SqlConnection connection = new SqlConnection(connStr))
            {
                SqlCommand command = new SqlCommand("SELECT fileversion FROM autoupdata where ename=@ename and cname=@cname", connection);
                connection.Open();
                command.Parameters.AddWithValue("@ename", arg1.Substring(0, arg1.Length - 4));
                command.Parameters.AddWithValue("@cname", arg2);
                SqlDataReader dr = command.ExecuteReader();
                dt.Load(dr);
                dr.Close();
            }
            if (dt.Rows.Count == 1)
            {
                newVer = dt.Rows[0][0].ToString();
            }
            else if (dt.Rows.Count == 0)
            {
                newVer = "99";//初次数据库中没有数据
            }
           
            long newv = 0;
            long nowv = 0;
            Int64.TryParse(newVer.Trim(), out newv);
            Int64.TryParse(nowVer.Trim(), out nowv);

            string folderPath = exepath + "\\updatefile";
            string fileName = "";

            if (newv > nowv || newVer=="99")
            {
                //避免文件夹不存在
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }
                //先删除
                string[] filePaths = Directory.GetFiles(folderPath);
                foreach (string filePath in filePaths)
                {
                    File.Delete(filePath);
                }

                using (SqlConnection connection = new SqlConnection(connStr))
                {
                    SqlCommand command = new SqlCommand("SELECT filedata, filename FROM autoupdata where ename=@ename and cname=@cname", connection);
                    connection.Open();
                    command.Parameters.AddWithValue("@ename", arg1.Substring(0, arg1.Length - 4));
                    command.Parameters.AddWithValue("@cname", arg2);

                    SqlDataReader dr = command.ExecuteReader();

                    while (dr.Read())
                    {
                        // 读取二进制数据和文件名
                        byte[] binaryData = (byte[])dr["filedata"];
                        fileName = dr["filename"].ToString();

                        // 将二进制数据写入本地文件
                        string filePath = Path.Combine(folderPath, fileName);
                        File.WriteAllBytes(filePath, binaryData);
                    }
                    dr.Close();
                }

                Decomp(arg1,fileName, folderPath, newv);
            }

        }

        void Decomp(string exename,string filename, string folderpath,long newver)
        {
            using (var stream = File.OpenRead(folderpath + "\\"+ filename))
            {
                using (var reader = ReaderFactory.Open(stream))
                {
                    while (reader.MoveToNextEntry())
                    {
                        if (!reader.Entry.IsDirectory)
                        {
                            string filePath = Path.Combine(folderpath, reader.Entry.Key);
                            string directoryPath = Path.GetDirectoryName(filePath);

                            if (!Directory.Exists(directoryPath))
                            {
                                Directory.CreateDirectory(directoryPath);
                            }
                            if (File.Exists(filePath))
                            {
                                File.Delete(filePath);
                            }

                            reader.WriteEntryToFile(filePath);
                        }
                    }
                }
            }

            Upprogram(exename, folderpath, newver);
        }

        void Upprogram(string exename, string folderpath,long newver)
        {
            foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exename)))
            {
                process.Kill();
                process.WaitForExit();
            }

            MoveDirectory(folderpath, exepath);

            using (StreamWriter writer = new StreamWriter(exepath + "\\version.txt"))
            {
                // 更新版本号
                writer.WriteLine(newver);
            }
            // 自动启动程序
            //Process.Start(exename);
        }

        /// <summary>
        /// 替换更新文件
        /// </summary>
        /// <param name="sourceDirectory"></param>
        /// <param name="targetDirectory"></param>
        static void MoveDirectory(string sourceDirectory, string targetDirectory)
        {
            //Directory.CreateDirectory(targetDirectory);
            foreach (string dirPath in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourceDirectory, targetDirectory));
            }

            foreach (string newPath in Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(newPath);
                if (extension != ".rar" && extension != ".zip")
                {
                    File.Copy(newPath, newPath.Replace(sourceDirectory, targetDirectory), true);
                }
            }
            //删除下载目录保持程序干净
            Directory.Delete(sourceDirectory, true);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFile = new OpenFileDialog();
            openFile.Filter = "rar Files (*.rar)|*.rar|zip Files (*.zip)|*.zip";
            string ename = "";//执行程序的名称，不需要带.exe
            string cname = "";//执行程序窗体的名称
            if (rb1.Checked)
            {
                ename = "数据生成导出";
                cname = "生成数据";
            }

            if (ename == "")
            {
                MessageBox.Show("请先选择程序名称");
                return;
            }

            if (openFile.ShowDialog() == DialogResult.OK)
            {
                int cnt = 0;
                string dtVer = DateTime.Now.ToString("yyMMddHHmmss");

                using (SqlConnection connection = new SqlConnection(connStr))
                {
                    connection.Open();
                    //先删除相同文件，避免数据库变大
                    using (SqlCommand command = new SqlCommand("delete from autoupdata where ename=@ename and cname=@cname", connection))
                    {
                        command.Parameters.AddWithValue("@ename", ename);
                        command.Parameters.AddWithValue("@cname", cname);
                        command.ExecuteNonQuery();
                    }

                    foreach (string filePath in openFile.FileNames)
                    {
                        byte[] binaryData = File.ReadAllBytes(filePath);
                        string fileName = Path.GetFileName(filePath);

                        string insertQuery = "INSERT INTO autoupdata (ename,cname,fileversion,filedata,filename,uptime) VALUES (@ename,@cname,@fileVer,@BinaryData,@fileName,@uptime)";
                        using (SqlCommand command = new SqlCommand(insertQuery, connection))
                        {
                            command.Parameters.AddWithValue("@BinaryData", binaryData);
                            command.Parameters.AddWithValue("@fileName", fileName);
                            command.Parameters.AddWithValue("@fileVer", dtVer);
                            command.Parameters.AddWithValue("@ename", ename);
                            command.Parameters.AddWithValue("@cname", cname);
                            command.Parameters.AddWithValue("@uptime", DateTime.Now);
                            cnt = command.ExecuteNonQuery();
                        }
                    }

                }
                if (cnt == 1)
                {
                    MessageBox.Show("上传完成");
                }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
