
using Quobject.SocketIoClientDotNet.Client;
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace CamFiTransmit
{

    public class Transmitter
    {
        public delegate bool ControlCtrlDelegate(int CtrlType);
        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleCtrlHandler(ControlCtrlDelegate HandlerRoutine, bool Add);
        private static ControlCtrlDelegate cancelHandler = new ControlCtrlDelegate(HandlerRoutine);


        public static string BaseUrl = "http://192.168.9.67";
        public static string Port = "8080";
        public static string AccessPwd = null;
        public static string SavePath = @"D:\Dataset\SfM\SelfMade\New";
        public static bool IsDownloadSuccess = false;
        public static string[] args;
        public static DateTime StartTime;

        public static void Output(string Message, ConsoleColor Color = ConsoleColor.White)
        {
            Console.ForegroundColor = Color;
            Console.WriteLine(Message);
            Console.ForegroundColor = ConsoleColor.White;
        }
        public static void NewImageHandler(string ImgPath)
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\RTPS\\CamFiTransmit", true);
            string InitialString = IsRegeditKeyExist("NewImage") ? key.GetValue("NewImage").ToString() : "";
            key.SetValue("NewImage", InitialString + ImgPath + ";");
        }
        public static void StartWork()
        {
            Output("ip地址:\t\t" + BaseUrl);
            Output("端口号:\t\t" + Port);
            Output("影像保存路径:\t" + SavePath);
            Output("");
            Output("等待设备连接...");
            StartListen();
            InitialSocket();
        }
        public static void EndWork()
        {
            device = new DeviceInfo();
            camera = new CameraInfo();
            downloadQueue = null;
            downloadThread.Abort();
            socket.Close();
            socket.Disconnect();
            socket.Off();
            socket = null;
            IsDownloadSuccess = false;
        }
        public static void Refresh()
        {
            while(true)
            {
                if ((DateTime.Now - StartTime).TotalSeconds >= 30)
                {
                    EndWork();
                    Console.Clear();
                    Output("-------刷新-------", ConsoleColor.Yellow);
                    StartWork();
                    StartTime = DateTime.Now;
                }
                Thread.Sleep(1000);
            }
        }
        [STAThread]
        public static void Main(string[] args)
        {
            SetConsoleCtrlHandler(cancelHandler, true);
            if (args.Length == 3)
            {
                BaseUrl = args[0];
                Port = args[1];
                SavePath = args[2];
            }
            else if (args.Length == 4)
            {
                BaseUrl = args[0];
                Port = args[1];
                AccessPwd = args[2];
                SavePath = args[3];
            }
            else return;
            SavePath = SavePath.Replace("\"", "");
            Transmitter.args = args;
            RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\RTPS\\CamFiTransmit", true);
            key.SetValue("IsRunning", "1");
            StartTime = DateTime.Now;
            Thread RefreshThread = new Thread(Refresh);
            RefreshThread.Start();
            StartWork();
            while (true)
            {
                Thread.Sleep(1000);
            }
        }
        public static bool HandlerRoutine(int CtrlType)
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\RTPS\\CamFiTransmit", true);
            if (IsRegeditKeyExist("NewImage"))
            {
                key.SetValue("NewImage", "");
            }
            key.SetValue("IsRunning", "0");
            return false;
        }


        private static CameraInfo camera = new CameraInfo();
        private static Socket socket;
        private static DeviceInfo device = new DeviceInfo();
        private static Queue<string> downloadQueue;
        private static Thread downloadThread;
        private static string currentDownloadPhoto;
        private static string LastDownloadImgName = null;
        private static bool IsRegeditKeyExist(string name)
        {
            string[] saveSubkeyNames;

            RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\RTPS\\CamFiTransmit", true);
            //获取该子项下的所有键值的名称saveSubkeyNames 
            saveSubkeyNames = key.GetValueNames();
            foreach (string keyName in saveSubkeyNames)
            {
                if (keyName == name)
                {
                    key.Close();
                    return true;
                }
            }
            key.Close();
            return false;
        }
        private static void StartListen()
        {
            device = new DeviceInfo();
            camera = new CameraInfo();
            if (downloadQueue == null)
            {
                downloadQueue = new Queue<string>();
                downloadThread = new Thread(HttpDownloadThread);
                downloadThread.Start();
            }
        }
        private static void InitialSocket()
        {
            IO.Options options = new IO.Options();
            options.ForceNew = true;
            options.Multiplex = true;
            options.Timeout = 3000L;
            options.AutoConnect = true;
            options.Reconnection = true;
            options.ReconnectionDelay = 500L;
            socket = IO.Socket(BaseUrl + ":" + Port, options);
            socket.On(Socket.EVENT_CONNECT, () =>
            {
                InteractionAPIs.ApiResult result = new InteractionAPIs.ApiResult();
                InteractionAPIs.GetInfoSync(BaseUrl, AccessPwd, result);
                if (result.Success && result.Object != null)
                {
                    device.Version = ((result.Object["version"] == null) ? "" : result.Object["version"].ToString());
                    device.Serial = ((result.Object["serial"] == null) ? "" : result.Object["serial"].ToString());
                    device.Used = ((result.Object["used"] == null) ? "" : result.Object["used"].ToString());
                    device.Region = ((result.Object["region"] == null) ? "" : result.Object["region"].ToString());
                    device.Type = ((result.Object["type"] == null) ? "" : result.Object["type"].ToString());
                    device.Subtype = ((result.Object["subtype"] == null) ? "" : result.Object["subtype"].ToString());
                }
                else
                {
                    device.Version = "";
                    device.Serial = "";
                    device.Used = "";
                    device.Region = "";
                    device.Type = "";
                    device.Subtype = "";
                }
                result = new InteractionAPIs.ApiResult();
                InteractionAPIs.GetTetherTimerStatusSync(BaseUrl, AccessPwd, result);
                result = new InteractionAPIs.ApiResult();
                InteractionAPIs.GetCameraSync(BaseUrl, AccessPwd, result);
                if (result.Success && result.Object != null)
                {
                    camera.CameraModel = ((result.Object["model"] == null) ? "" : result.Object["model"].ToString());
                    Console.ForegroundColor = ConsoleColor.Red;
                    Output("已连接【" + camera.CameraModel + "】",ConsoleColor.Green);
                    StartTime = DateTime.Now;
                }
            });
            socket.On(Socket.EVENT_DISCONNECT, () =>
            {
                Output("连接断开!", ConsoleColor.Red);
            });
            socket.On("file_added", delegate (object message)
            {
                try
                {
                    Trace.TraceInformation("{0} socket.io file_added {1}", DateTime.Now, message.ToString());
                    string filename = message.ToString();
                    Output("检测到图像【" + filename + "】");
                    StartTime = DateTime.Now;
                    downloadQueue.Enqueue(filename);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("file_added error", ex.Message);
                }
            });
        }
        private static void HttpDownloadThread()
        {
            while (true)
            {
                if (downloadQueue != null && downloadQueue.Count > 0)
                {
                    currentDownloadPhoto = downloadQueue.Dequeue();
                    bool IsSuccess = HttpDownloadRawFile(currentDownloadPhoto);
                    if (IsSuccess)
                    {
                        IsDownloadSuccess = true;
                        Output("成功下载 " + LastDownloadImgName, ConsoleColor.Green);
                        NewImageHandler(LastDownloadImgName);
                    }
                    else
                    {
                        Output(LastDownloadImgName + "下载失败!", ConsoleColor.Red);
                        LastDownloadImgName = null;
                    }
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }
        private static bool HttpDownloadRawFile(string file, int count = 0)
        {
            string filename = Path.GetFileName(file);
            string localPath = Path.Combine(SavePath, filename);
            LastDownloadImgName = localPath;
            InteractionAPIs.ApiResult result = new InteractionAPIs.ApiResult();
            bool IsSuccess = InteractionAPIs.HttpDownloadRawImage2(BaseUrl, AccessPwd, file, SavePath, localPath, delegate (InteractionAPIs.ApiEventArgs args2) { },
                delegate (int progress) { });
            return IsSuccess;
        }
    }

    internal enum DeviceType
    {
        CamFi,
        CamFiPro,
        CamFiProPlus
    }
    internal enum CameraStatus
    {
        Disconnect,
        Connect
    }
    internal class DeviceInfo
    {
        public string Version;
        public string Serial;
        public string Used;
        public string Region;
        public string Type;
        public string Subtype;
        public string SSID;
        public string Channel;
        public DeviceType DeviceType
        {
            get
            {
                if (Subtype.ToLower() == "plus")
                {
                    return DeviceType.CamFiProPlus;
                }
                if (Type.ToLower() == "pro")
                {
                    return DeviceType.CamFiPro;
                }
                return DeviceType.CamFi;
            }
        }
    }
    internal class CameraInfo
    {
        public string CameraModel;
        public CameraStatus Status
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(CameraModel))
                {
                    return CameraStatus.Connect;
                }
                return CameraStatus.Disconnect;
            }
        }
        public bool IsSony()
        {
            if (CameraModel == null)
            {
                return false;
            }
            if (CameraModel.Contains("Sony") || CameraModel.Contains("SLT-") || CameraModel.Contains("ILCE") || CameraModel == "DSC-RX0")
            {
                return true;
            }
            return false;
        }
    }
}
