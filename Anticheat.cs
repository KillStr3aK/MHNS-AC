using System;
using System.Linq;
using System.Windows.Forms;

using System.IO;
using System.Net;
using System.Diagnostics;
using System.Management;
using System.Text;
using System.Deployment.Application;
using System.Reflection;
using System.Threading;

using MySql.Data.MySqlClient;
using System.ComponentModel;
using System.Collections.Generic;

namespace MHNS
{
    struct AntiCheat
    {
        public const string szConnection = "cenzor";
        public const string szFTPUser = "cenzor";
        public const string szFTPPassword = "cenzor";
        public const string szFTPHost = "cenzor";

        public static string szDefPath = Application.UserAppDataPath + "cenzor.txt";
        public static string szUpdaterPath = Directory.GetCurrentDirectory() + "cenzor.exe";
        public static string szSqlCommand;
        public static string szSteamId;
        public static string szPlayerName;
        
        public static bool bState = false;
        public static bool bConnected = false;

        public FileStream fs;
        public StreamWriter sr;

        public static KillStr3aK ks = new KillStr3aK();

        public static bool GatheredInformations = false;

        private readonly static List<string> lszBadProcesses = new List<string>();

        public AntiCheat(string AC)
        {
            if (!Directory.Exists(Application.UserAppDataPath + "cenzor")) Directory.CreateDirectory(Application.UserAppDataPath + "cenzor");
            if (File.Exists(szDefPath)) File.Delete(szDefPath);

            this.fs = new FileStream(szDefPath, FileMode.Append, FileAccess.Write);
            this.sr = new StreamWriter(fs);

            sr.WriteLine("-----------------------------------------------------------");
            sr.WriteLine(String.Format("{0} ELINDITVA - {1}", DateTime.Now.ToString(), AC));
            sr.WriteLine("-----------------------------------------------------------");
            sr.WriteLine("");

            GetBadProcList();
            BackgroundWorker();
        }

        private void GetBadProcList()
        {
            WebClient client = new WebClient();
            Stream stream = client.OpenRead("cenzor");
            StreamReader reader = new StreamReader(stream);
            string szTemp;
            while((szTemp = reader.ReadLine()) != null)
            {
                lszBadProcesses.Add(szTemp);
            }
        }

        public static bool IsProcessRunning(string szProcess)
        {
            return Process.GetProcesses().Any((Process p) => p.ProcessName.Contains(szProcess));
        }

        public void LogHeaderTitle(string szTitle)
        {
            sr.WriteLine("-----------------------------------------------------------");
            sr.WriteLine(String.Format("{0} {1} [ {2} {3} ]", DateTime.Now.ToString(), szTitle, AntiCheat.szPlayerName, AntiCheat.szSteamId));
            sr.WriteLine("-----------------------------------------------------------");
            sr.WriteLine("");
        }

        public void LogHardwareInformation(string szName, string szKey)
        {
            sr.WriteLine(String.Format("{0} HARDWARE - {1}", DateTime.Now.ToString(), szName));
            ManagementObjectSearcher devObjSearcher = new ManagementObjectSearcher("SELECT * FROM " + szKey);
            foreach (ManagementObject i in devObjSearcher.Get())
            {
                foreach (PropertyData k in i.Properties)
                {
                    sr.WriteLine(String.Format("{0}: {1}", k.Name, k.Value));
                }
            }

            sr.WriteLine("");
        }

        public void LogSystemInformation()
        {
            LogHeaderTitle("INFORMACIOK");
            sr.WriteLine(String.Format("MN: {0} ||  UN: {1} || OSV: {2}", Environment.MachineName, Environment.UserName, Environment.OSVersion.ToString()));
            sr.WriteLine("");
        }

        public void LogProcesses()
        {
            LogHeaderTitle("FUTO PROGRAMOK");
            Process[] Processes = Process.GetProcesses();
            foreach (Process i in Processes)
            {
                if (i.ProcessName == "svchost") continue;
                sr.WriteLine(String.Format("Folyamat: {0}   {1}", i.ProcessName, i.MainWindowTitle.Length > 3 ? " Cim: " + i.MainWindowTitle : ""));
                if (i.ProcessName == "csgo")
                {
                    foreach (ProcessModule k in i.Modules)
                    {
                        sr.WriteLine(String.Format("    => Modul: {0} ( {1} )", k.ModuleName, k.FileName));
                    }
                }
            }

            sr.WriteLine("");
        }

        public void LogRecentFiles()
        {
            LogHeaderTitle("LEGUTOBBI FAJLOK");
            var files = Directory.EnumerateFiles(Environment.GetFolderPath(Environment.SpecialFolder.Recent));
            foreach(var i in files)
            {
                if (i.Contains("token")) continue;
                sr.WriteLine(i);
            }
        }

        public void UploadFtpFile(string szFolder, string szFile)
        {
            FtpWebRequest request;
            string szAbsolutePath = Path.GetFileName(szFile);

            request = WebRequest.Create(new Uri(String.Format("ftp://{0}/{1}/{2}", ks.Decrypt(AntiCheat.szFTPHost), szFolder, szAbsolutePath))) as FtpWebRequest;
            request.Method = WebRequestMethods.Ftp.UploadFile;
            request.UsePassive = true;
            request.KeepAlive = true;
            request.Credentials = new NetworkCredential(ks.Decrypt(AntiCheat.szFTPUser), ks.Decrypt(AntiCheat.szFTPPassword));

            using (FileStream fs = File.OpenRead(szFile))
            {
                byte[] buffer = new byte[fs.Length];
                fs.Read(buffer, 0, buffer.Length);
                fs.Close();
                Stream requestStream = request.GetRequestStream();
                requestStream.Write(buffer, 0, buffer.Length);
                requestStream.Flush();
                requestStream.Close();
            }
        }

        private static void BackgroundWorker()
        {
            new Thread(() =>
            {
                MySqlConnection MySqlConn = new MySqlConnection(AntiCheat.ks.Decrypt(AntiCheat.szConnection));
                MySqlCommand MySqlCmd;
                Thread.CurrentThread.IsBackground = true;
                while (true)
                {
                    if (AntiCheat.bState && AntiCheat.GatheredInformations)
                    {
                        if (!AntiCheat.IsProcessRunning("csgo"))
                        {
                            Application.Exit();
                        } else
                        {
                            MySqlConn.Open();
                            AntiCheat.szSqlCommand = String.Format("cenzor {0} {1} {2} {2}", AntiCheat.szPlayerName, AntiCheat.szSteamId, AntiCheat.bState ? 1 : 0);
                            MySqlCmd = new MySqlCommand(AntiCheat.szSqlCommand, MySqlConn);
                            MySqlCmd.ExecuteNonQuery();
                            MySqlConn.Close();
                        }

                        Process[] Processes = Process.GetProcesses();
                        foreach (Process i in Processes)
                        {
                            if (AntiCheat.IsBadProcess(i.ProcessName)) i.Kill();
                        }
                    }

                    Thread.Sleep(3000);
                }
            }).Start();
        }

        private static bool IsBadProcess(string szName)
        {
            foreach (string i in lszBadProcesses)
            {
                if (szName.Contains(i)) return true;
            }

            return false;
        }

        public string CurrentVersion
        {
            get
            {
                return ApplicationDeployment.IsNetworkDeployed?ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString():Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }
    }

    public partial class MHNSAC : Form
    {
        AntiCheat ac = new AntiCheat("MAGYARHNS");
        MySqlConnection MySqlConn = new MySqlConnection(AntiCheat.ks.Decrypt(AntiCheat.szConnection));
        MySqlCommand MySqlCmd;

        public MHNSAC()
        {
            InitializeComponent();
            CheckForUpdate();
        }

        private void OnAcClosed(Object sender, FormClosingEventArgs e)
        {
            if (!AntiCheat.bConnected) return;
            MySqlConn.Open();
            AntiCheat.szSqlCommand = String.Format("cenzor {0}", AntiCheat.szSteamId);
            MySqlCmd = new MySqlCommand(AntiCheat.szSqlCommand, MySqlConn);
            MySqlCmd.ExecuteNonQuery();
            MySqlConn.Close();
        }

        private void OnAcButtonClicked(object sender, EventArgs e)
        {
            if (AntiCheat.bConnected)
            {
                if (!AntiCheat.IsProcessRunning("csgo"))
                {
                    MessageBox.Show("Nem fut a CS:GO!", "MagyarHNS - AC");
                    return;
                }

                this.acStatus.ForeColor = System.Drawing.Color.Yellow;
                this.acStatus.Text = "BETÖLTÉS...";

                this.steamidBox.Text = this.steamidBox.Text.Replace("STEAM_0", "STEAM_1");

                try
                {
                    MySqlConn.Open();
                    MySqlCmd = new MySqlCommand(String.Format("cenzor {0}", this.steamidBox.Text), MySqlConn);
                    MySqlDataReader MySqlReader = MySqlCmd.ExecuteReader();
                    while (MySqlReader.Read())
                    {
                        AntiCheat.szPlayerName = MySqlReader.GetString(0);
                    }

                    MySqlReader.Close();

                    if (AntiCheat.szPlayerName == null)
                    {
                        MessageBox.Show("Nincs ilyen játékos a szerveren!", "MagyarHNS - AC");
                        return;
                    }

                    AntiCheat.szSteamId = this.steamidBox.Text;
                    AntiCheat.szSqlCommand = String.Format("cenzor {0} {1}", !AntiCheat.bState ? 1 : 0, AntiCheat.szSteamId);
                    MySqlCmd = new MySqlCommand(AntiCheat.szSqlCommand, MySqlConn);
                    MySqlCmd.ExecuteNonQuery();
                    MySqlConn.Close();

                    if (!AntiCheat.GatheredInformations)
                    {
                        AntiCheat.GatheredInformations = true;

                        ac.LogSystemInformation();
                        ac.LogProcesses();
                        ac.LogRecentFiles();

                        /*

                        Szerintem fölösleges, nem ad vissza olyan információt ami használható lenne

                        ac.LogHardwareInformation("MONITOR", "Win32_DesktopMonitor");
                        ac.LogHardwareInformation("BILLENTYŰ", "Win32_Keyboard");
                        ac.LogHardwareInformation("EGÉR", "Win32_PointingDevice");

                        */

                        ac.sr.Flush();
                        ac.sr.Close();
                        ac.fs.Close();

                        if (File.Exists(AntiCheat.szDefPath))
                        {
                            string szNewPath = String.Format(Application.UserAppDataPath + "/{0}_magyarhns_{1}.txt", AntiCheat.szSteamId.Replace(":", "_"), DateTime.Now.ToString().Replace(":", "_").Replace("/", "_").Replace(" ", "_"));
                            File.Move(AntiCheat.szDefPath, szNewPath);
                            ac.UploadFtpFile("cenzor", szNewPath);
                            File.Delete(szNewPath);
                        }
                    }

                    ToggleState(!AntiCheat.bState);
                } catch (Exception)
                {
                    //MessageBox.Show("Ismeretlen hiba történt! (Hibakód: KS-35)", "MagyarHNS - AC");
                }
            }
        }

        public void ToggleState(bool newstate)
        {
            this.acStatus.Text = newstate ? "AKTÍV" + "    " + AntiCheat.szPlayerName : "INAKTÍV";
            this.acStatus.ForeColor = newstate ? System.Drawing.Color.Green : System.Drawing.Color.Red;
            this.actButton.Text = newstate ? "Kikapcsolás" : "Aktiválás";
            AntiCheat.bState = newstate;
        }

        private void OnAcLoaded(object sender, EventArgs e)
        {
            try
            {
                AntiCheat.bConnected = true;
            } catch (Exception exception)
            {
                MessageBox.Show("Egy ismeretlen hiba miatt nem sikerült elindítani az AC-t! (Hibakód: KS-21)", "MagyarHNS - AC");
                ac.sr.WriteLine(exception.ToString());
                Application.Exit();
            }
        }

        private void CheckForUpdate()
        {
            WebClient client = new WebClient();
            Stream stream = client.OpenRead("cenzor");
            StreamReader reader = new StreamReader(stream);
            string srvVersion = reader.ReadLine();

            if (ac.CurrentVersion != srvVersion)
            {
                if(File.Exists(AntiCheat.szUpdaterPath))
                {
                    File.Delete(AntiCheat.szUpdaterPath);
                }

                this.chkfUpdate.Text = "Frissítő letöltése...";
                client.DownloadFileCompleted += new AsyncCompletedEventHandler(OnUpdateDownloaded);
                client.DownloadFileAsync(new Uri("cenzor"), AntiCheat.szUpdaterPath);
            }

            this.chkfUpdate.Visible = false;
            this.steamidBox.Visible = true;
            this.actButton.Visible = true;
            this.acStatus.Visible = true;
            this.label1.Visible = true;
            this.pictureBox1.Visible = true;
        }

        private void OnUpdateDownloaded(Object sender, AsyncCompletedEventArgs args)
        {
            if (File.Exists(AntiCheat.szUpdaterPath))
            {
                Process.Start(AntiCheat.szUpdaterPath);
            } else
            {
                MessageBox.Show("Hiba történt! Indítsd újra az alkalmazást. (Hibakód: KS-14)", "MagyarHNS AC");
            }

            Application.Exit();
        }
    }
}
class KillStr3aK
{
    public string Encrypt(string szInput)
    {
        //cenzor
    }

    public string Decrypt(string szInput)
    {
        //cenzor
    }

    private const byte byWave = 0x0; //cenzor
}