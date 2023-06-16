简易自动更新程序

1，拷贝autoUpdate.exe，SharpCompress.dll到需要更新的程序中；
2，将需要更新的最新文件压缩为zip或rar；
3，创建autoUpdate.exe快捷方式，设置参数 admin，运行成功后上传文件到数据库；（默认同一个程序只会有一条数据）
4，（需要自动更新的程序Load函数中加入自动更新代码）；
5，运行需要更新的程序即可（不是autoUpdate.exe）；

为省略文件及配置，数据库连接等写死在程序中，可根据实际情况调整。

表字段：
ename(nvarchar(30))，执行程序名称，如autoUpdate.exe不要.exe
cname(nvarchar(30))，运行程序名称，即默认登陆后运行的窗体名称
程序使用这两个字段判断需要更新的程序是哪个，避免出现exe重名的程序导致更新错误；
fileversion(nvarchar(30))，版本号使用上传时间年月日时判断，方便对比；
filedata(varbinary)
filename(nvarchar(30))，上传的压缩包名字，没用处，暂时留着方便识别；
uptime(datetime)，上传时间

客户端代码：
#region 自动更新
            string nowVer = "0";
            string newVer = "0";
            try
            {
                using (StreamReader sr = new StreamReader(Environment.CurrentDirectory + "/version.txt"))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        nowVer = line;
                    }
                }
            }
            catch { }

            string ename = Assembly.GetEntryAssembly().GetName().Name;//执行程序名称，即XXX.exe（不需要.exe）
            string cname = "生成数据";//程序名称
            try
            {
                string[] autoupIp=ConfigurationManager.AppSettings["autoIp"].Replace(" ", "").Split(',');
                string connStr = $"Data Source={autoupIp[0]};Initial Catalog={autoupIp[1]};Persist Security Info=True;User ID={autoupIp[2]};Password={autoupIp[3]}";
                //获取服务器版本号
                using (SqlConnection connection = new SqlConnection(connStr))
                {
                    SqlCommand command = new SqlCommand("SELECT fileversion FROM autoupdata where ename=@ename and cname=@cname", connection);
                    connection.Open();
                    command.Parameters.AddWithValue("@ename", ename);
                    command.Parameters.AddWithValue("@cname", cname);
                    SqlDataReader dr = command.ExecuteReader();
                    while (dr.Read())
                    {
                        newVer = dr[0].ToString();
                    }
                    dr.Close();
                }
            }
            catch
            {

            }
            
            long newv = 0;
            long nowv = 0;
            Int64.TryParse(newVer.Trim(), out newv);
            Int64.TryParse(nowVer.Trim(), out nowv);

            //执行自动更新程序
            int exitCode = 1;
            if (newv > nowv)
            {
                Process process = new Process();
                process.StartInfo.FileName = Environment.CurrentDirectory+ "/autoUpdate.exe";
                process.StartInfo.Arguments = ename+".exe "+cname;//通过传参数给更新程序取数据库中相应数据
                process.Start();
                process.WaitForExit();
                exitCode = process.ExitCode;
            }
            //0成功1失败，不管成功失败都继续

            this.Text += " 当前版本："+nowVer+" 最新版本："+newVer;

            #endregion