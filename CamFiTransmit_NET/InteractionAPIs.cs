using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace CamFiTransmit
{
    internal class InteractionAPIs
    {
        public interface IApiResult
        {
            bool Success { get; set; }

            string Content { get; set; }

            string Msg { get; set; }

            JObject Object { get; }

            JArray Array { get; }
        }

        public class ApiResult : IApiResult
        {
            public bool Success { get; set; }

            public string Content { get; set; }

            public string Msg { get; set; }

            public JObject Object
            {
                get
                {
                    try
                    {
                        return JObject.Parse(Content);
                    }
                    catch
                    {
                    }
                    return null;
                }
            }

            public JArray Array
            {
                get
                {
                    try
                    {
                        return JArray.Parse(Content);
                    }
                    catch
                    {
                    }
                    return null;
                }
            }

            public ApiResult()
            {
                Success = false;
                Content = "";
                Msg = "";
            }
        }

        public class ApiEventArgs : ApiResult
        {
            public string Command { get; private set; }

            public string Parameter { get; private set; }

            public string Value { get; private set; }

            public ApiEventArgs(string command)
            {
                Command = command;
                Parameter = null;
                Value = null;
            }

            public ApiEventArgs(string command, string parameter)
            {
                Command = command;
                Parameter = parameter;
                Value = null;
            }

            public ApiEventArgs(string command, string parameter, string value)
            {
                Command = command;
                Parameter = parameter;
                Value = value;
            }
        }

        public delegate void ApiDelegate(ApiEventArgs args);

        public delegate void HttpDownloadProgressDelegate(int progress);

        private static object lockObj = new object();

        public static long DOWNLOAD_PART_SIZE = 4096L;

        private static RestClient client = new RestClient("http://192.168.9.67/");

        public static void GetInfoSync(string url, string password, ApiResult result)
        {
            GetSync(url, password, "info", result, 5000);
        }

        public static void GetCameraSync(string url, string password, ApiResult result)
        {
            GetSync(url, password, "camera", result, 5000);
        }

        public static void GetNetworkSync(string url, string password, ApiResult result)
        {
            GetSync(url, password, "network", result, 5000);
        }

        public static void GetTetherTimerStatusSync(string url, string password, ApiResult result)
        {
            GetSync(url, password, "tethertimer", result, 5000);
        }

        public static void GetCameraConfigSync(string url, string password, ApiResult result, CancellationToken token = default(CancellationToken))
        {
            GetSync(url, password, "config", result, 10000, token);
        }

        public static void GetFilesListSync(string url, string password, int count, ApiResult result, CancellationToken token = default(CancellationToken))
        {
            GetSync(url, password, "files/0/" + count, result, 10000, token);
        }

        public static void SetWifiInfoSync(string url, string password, string ssid, string pswd, string channel, ApiResult result = null)
        {
            string body;
            if (ssid != null && ssid != "" && pswd != null && pswd != "")
            {
                body = "{\"ssid\":\"" + ssid + "\",\"channel\":\"" + channel + "\",\"password\":\"" + pswd + "\"}";
            }
            else if (ssid != null && ssid != "")
            {
                body = "{\"ssid\":\"" + ssid + "\",\"channel\":\"" + channel + "\"}";
            }
            else
            {
                if (pswd == null || !(pswd != ""))
                {
                    if (result != null)
                    {
                        result.Success = false;
                        result.Content = "";
                        result.Msg = "";
                    }
                    return;
                }
                body = "{\"password\":\"" + pswd + "\"}";
            }
            PostSync(url, password, "network", body, result);
        }

        public static void PutSetConfigValueSync(string url, string password, string name, string value, ApiResult result)
        {
            string body = JsonConvert.SerializeObject(new { name, value });
            PutSync(url, password, "setconfigvalue", body, result);
        }

        public static void DeleteFileSync(string url, string password, string filename, ApiResult result)
        {
            DeleteSync(url, password, "/image/" + filename, result);
        }

        private static void GetSync(string url, string password, string api, ApiResult result, int timeout = 30000, CancellationToken token = default(CancellationToken))
        {
            RestClient client = new RestClient(url);
            RestRequest request = new RestRequest(api);
            request.Credentials = new NetworkCredential("CamFi", password);
            request.Timeout = timeout;
            Task<IRestResponse> task = client.ExecuteTaskAsync(request, token);
            try
            {
                task.Wait();
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Content = "";
                result.Msg = ex.Message;
                Trace.TraceInformation("{0} {1} {2}", DateTime.Now, client.BaseUrl?.ToString() + api, ex.Message);
                return;
            }
            IRestResponse response = task.Result;
            if (result != null)
            {
                result.Success = false;
                result.Content = "";
                result.Msg = "";
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    result.Content = response.Content;
                    result.Success = true;
                }
                else
                {
                    GetResultMsg(response, result);
                }
                if (api == "camera" && result.Object != null)
                {
                    string cameraModel = ((result.Object["model"] == null) ? "" : result.Object["model"].ToString());
                    Trace.TraceInformation("{0} {1} {2} {3}", DateTime.Now, client.BaseUrl?.ToString() + api, result.Success, cameraModel);
                }
                else
                {
                    Trace.TraceInformation("{0} {1} {2} {3}", DateTime.Now, client.BaseUrl?.ToString() + api, result.Success, result.Content);
                }
            }
        }

        private static void PostSync(string url, string password, string api, string body, ApiResult result)
        {
            RestClient client = new RestClient(url);
            RestRequest request = new RestRequest(api);
            request.Credentials = new NetworkCredential("CamFi", password);
            request.Timeout = 30000;
            request.AddParameter("application/json", body, ParameterType.RequestBody);
            IRestResponse response = client.Post(request);
            if (result != null)
            {
                result.Success = false;
                result.Content = "";
                result.Msg = "";
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    result.Success = true;
                }
                else
                {
                    GetResultMsg(response, result);
                }
                Trace.TraceInformation("{0} {1} {2} {3}", DateTime.Now, client.BaseUrl?.ToString() + api, body, result.Success);
            }
        }

        private static void PutSync(string url, string password, string api, string body, ApiResult result)
        {
            RestClient client = new RestClient(url);
            RestRequest request = new RestRequest(api);
            request.Credentials = new NetworkCredential("CamFi", password);
            request.Timeout = 30000;
            request.AddParameter("application/json", body, ParameterType.RequestBody);
            IRestResponse response = client.Put(request);
            if (result != null)
            {
                result.Success = false;
                result.Content = "";
                result.Msg = "";
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    result.Success = true;
                }
                else
                {
                    GetResultMsg(response, result);
                }
                Trace.TraceInformation("{0} {1} {2} {3}", DateTime.Now, client.BaseUrl?.ToString() + api, body, result.Success);
            }
        }

        private static void DeleteSync(string url, string password, string api, ApiResult result, int timeout = 30000)
        {
            IRestResponse response = new RestClient(url).Delete(new RestRequest(api)
            {
                Credentials = new NetworkCredential("CamFi", password),
                Timeout = timeout
            });
            if (result != null)
            {
                result.Success = false;
                result.Content = "";
                result.Msg = "";
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    result.Content = response.Content;
                    result.Success = true;
                }
                else
                {
                    GetResultMsg(response, result);
                }
            }
        }

        public static void GetNewImagesListAsync(string url, string password, ApiDelegate handler)
        {
            GetAsync(url, password, "newimages", handler);
        }

        private static void GetAsync(string url, string password, string api, ApiDelegate handler)
        {
            RestClient client = new RestClient(url);
            RestRequest request = new RestRequest(api, Method.GET);
            request.Credentials = new NetworkCredential("CamFi", password);
            client.ExecuteAsync(request, delegate (IRestResponse response)
            {
                if (handler != null)
                {
                    ApiEventArgs apiEventArgs = new ApiEventArgs(api);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        apiEventArgs.Content = response.Content;
                        apiEventArgs.Success = true;
                    }
                    else
                    {
                        GetResultMsg(response, apiEventArgs);
                    }
                    Trace.TraceInformation("{0} {1} {2} {3}", DateTime.Now, client.BaseUrl?.ToString() + api, apiEventArgs.Success, apiEventArgs.Content);
                    handler(apiEventArgs);
                }
            });
        }

        private static void PostAsync(string url, string password, string api, string body, ApiDelegate handler)
        {
            RestClient client = new RestClient(url);
            RestRequest request = new RestRequest(api, Method.POST);
            if (body != null)
            {
                request.AddParameter("application/json", body, ParameterType.RequestBody);
            }
            request.Credentials = new NetworkCredential("CamFi", password);
            client.ExecuteAsync(request, delegate (IRestResponse response)
            {
                if (handler != null)
                {
                    ApiEventArgs apiEventArgs = new ApiEventArgs(api);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        apiEventArgs.Success = true;
                    }
                    else
                    {
                        GetResultMsg(response, apiEventArgs);
                    }
                    Trace.TraceInformation("{0} {1} {2} {3}", DateTime.Now, client.BaseUrl?.ToString() + api, body, apiEventArgs.Success);
                    handler(apiEventArgs);
                }
            });
        }

        private static void ForceCanonicalPathAndQuery(Uri uri)
        {
            _ = uri.PathAndQuery;
            FieldInfo field = typeof(Uri).GetField("m_Flags", BindingFlags.Instance | BindingFlags.NonPublic);
            ulong flags = (ulong)field.GetValue(uri);
            flags &= 0xFFFFFFFFFFFFFFCFuL;
            field.SetValue(uri, flags);
        }
        public static string GetNewLocalFilePath(string SavePath, string file)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            string ext = Path.GetExtension(file);
            string filename = name + ext;
            string localPath = Path.Combine(SavePath, filename);
            int i = 0;
            while (File.Exists(localPath))
            {
                i++;
                filename = $"{name}-{i}{ext}";
                localPath = Path.Combine(SavePath, filename);
            }
            return localPath;
        }
        public static bool HttpDownloadRawImage(string baseUrl, string accessPwd, string srcName, string destName, ApiDelegate completeHandler)
        {
            lock (lockObj)
            {
                Uri uri = new Uri(baseUrl + "/raw/" + srcName);
                HttpWebRequest webrequest = null;
                HttpWebResponse webresponse = null;
                MemoryStream ms2 = new MemoryStream();
                ForceCanonicalPathAndQuery(uri);
                int retry = 0;
                while (retry < 3)
                {
                    try
                    {
                        webrequest = (HttpWebRequest)WebRequest.Create(uri);
                        webrequest.ServicePoint.ConnectionLimit = 1000;
                        webrequest.Credentials = new NetworkCredential("CamFi", accessPwd);
                        webrequest.Timeout = 5000;
                        webrequest.ReadWriteTimeout = 5000;
                        Trace.TraceInformation("{0} try to download {1} {2}", DateTime.Now, uri.AbsolutePath, destName);
                        webresponse = (HttpWebResponse)webrequest.GetResponse();
                        Trace.TraceInformation("{0} begin to download {1} {2}", DateTime.Now, uri.AbsolutePath, destName);
                        retry = 3;
                    }
                    catch (Exception err)
                    {
                        Trace.TraceError("{0} {1} get response error {2}, retry {3}", DateTime.Now, baseUrl, err.Message, retry);
                        retry++;
                        Thread.Sleep(1000);
                        if (retry == 3)
                        {
                            if (completeHandler != null)
                            {
                                ApiEventArgs result = new ApiEventArgs(uri.AbsolutePath);
                                result.Success = false;
                                result.Msg = srcName;
                                completeHandler(result);
                            }
                            return false;
                        }
                    }
                }
                if (webresponse.StatusCode != HttpStatusCode.OK)
                {
                    if (completeHandler != null)
                    {
                        ApiEventArgs result3 = new ApiEventArgs(uri.AbsolutePath);
                        result3.Success = false;
                        result3.Msg = srcName;
                        completeHandler(result3);
                    }
                    return false;
                }
                BinaryReader sr = new BinaryReader(webresponse.GetResponseStream());
                int bufLength = 1024;
                byte[] buf = new byte[bufLength];
                long total = webresponse.ContentLength;
                Trace.TraceInformation("content-length {0}", total);
                long totalRead = 0L;
                bool writeFlag = false;
                int read;
                while (true)
                {
                    if (!writeFlag)
                    {
                        writeFlag = true;
                        Trace.TraceInformation("{0} {1} begin to read buffer {2} ", DateTime.Now, baseUrl, destName);
                    }
                    read = sr.Read(buf, 0, bufLength);
                    if (read == 0)
                    {
                        break;
                    }
                    ms2.Write(buf, 0, read);
                    totalRead += read;
                    if (total < totalRead + 1024 && read != 1024)
                    {
                        Trace.TraceInformation("{0} read length {1} total {2}", DateTime.Now, read, totalRead);
                    }
                }
                Trace.TraceInformation("{0} read length {1} total {2}", DateTime.Now, read, totalRead);
                Trace.TraceInformation("break");
                Trace.TraceInformation("{0} end while {1} /{2}", DateTime.Now, totalRead, total);
                if (ms2.Length < 512)
                {
                    Trace.TraceInformation("{0} {1} {2} write to memory stream <512 false", DateTime.Now, baseUrl, destName);
                    return false;
                }
                try
                {
                    Trace.TraceInformation("{0} {1} {2} begin to write file", DateTime.Now, baseUrl, destName);
                    FileStream writer = File.OpenWrite(destName + ".tmp");
                    writer.Write(ms2.ToArray(), 0, (int)ms2.Length);
                    ms2.Close();
                    Trace.TraceInformation("{0} {1} {2} close to write file", DateTime.Now, baseUrl, destName);
                    writer.Close();
                    if (File.Exists(destName))
                    {
                        File.Delete(destName);
                    }
                    File.Move(destName + ".tmp", destName);
                    Trace.TraceInformation("{0} {1} {2} end to write file", DateTime.Now, baseUrl, destName);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("{0} HttpDownloadImage error {1}", DateTime.Now, ex.Message);
                }
                if (completeHandler != null)
                {
                    ApiEventArgs result2 = new ApiEventArgs(uri.AbsolutePath);
                    result2.Success = true;
                    result2.Msg = srcName;
                    completeHandler(result2);
                }
                return true;
            }
        }
        private const int UnEscapeDotsAndSlashes = 0x2000000;
        public static void LeaveDotsAndSlashesEscaped(Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException("uri");
            }

            FieldInfo fieldInfo = uri.GetType().GetField("m_Syntax", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fieldInfo == null)
            {
                throw new MissingFieldException("'m_Syntax' field not found");
            }
            object uriParser = fieldInfo.GetValue(uri);

            fieldInfo = typeof(UriParser).GetField("m_Flags", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fieldInfo == null)
            {
                throw new MissingFieldException("'m_Flags' field not found");
            }
            object uriSyntaxFlags = fieldInfo.GetValue(uriParser);

            // Clear the flag that we don't want
            uriSyntaxFlags = (int)uriSyntaxFlags & ~UnEscapeDotsAndSlashes;

            fieldInfo.SetValue(uriParser, uriSyntaxFlags);
        }
        public static bool HttpDownloadRawImage2(string baseUrl, string accessPwd, string srcName, string SavePath, string destName, ApiDelegate completeHandler, HttpDownloadProgressDelegate progressHandler = null)
        {
            Stream responseStream = null;
            HttpWebResponse response = null;
            HttpWebRequest request = null;
            long iTotalSize = 0L;
            long total = 0L;
            try
            {
                string name = Uri.EscapeDataString(srcName);
                string url = $"{baseUrl}/raw/{name}";
                if (srcName.StartsWith("/tmp/sd/DCIM/"))
                {
                    url = $"{baseUrl}/cache/image/{name}";
                }
                Uri uri = new Uri(url);
                LeaveDotsAndSlashesEscaped(uri);
                request = WebRequest.CreateHttp(uri);
                request.Credentials = new NetworkCredential("CamFi", accessPwd);
                ServicePointManager.DefaultConnectionLimit = 512;
                //Transmitter.Output("下载地址:" + request.Address);
                response = request.GetResponse() as HttpWebResponse;
                responseStream = response.GetResponseStream();
                responseStream.ReadTimeout = 15000;
                total = response.ContentLength;
                if (File.Exists(destName))
                {
                    if (new FileInfo(destName).Length == total)
                    {
                        if (completeHandler != null)
                        {
                            ApiEventArgs result4 = new ApiEventArgs(srcName);
                            result4.Success = true;
                            result4.Msg = srcName;
                            completeHandler(result4);
                        }
                        return true;
                    }
                    destName = GetNewLocalFilePath(SavePath, destName);
                }
                byte[] bArr = new byte[DOWNLOAD_PART_SIZE];
                int progress = 0;
                int lastprogress = -1;
                int size = responseStream.Read(bArr, 0, bArr.Length);
                //Console.WriteLine("{0} HttpDownloadRawImage2 444 {1}", DateTime.Now, url);
                if (File.Exists(destName + ".filepart"))
                {
                    File.Delete(destName + ".filepart");
                }
                using (FileStream fs = new FileStream(destName + ".filepart", FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                {
                    while (size > 0)
                    {
                        iTotalSize += size;
                        fs.Write(bArr, 0, size);
                        size = responseStream.Read(bArr, 0, bArr.Length);
                        progress = (int)(iTotalSize * 100 / total);
                        if (progressHandler != null && lastprogress != progress)
                        {
                            lastprogress = progress;
                            //Console.WriteLine("{0} HttpDownloadRawImage2 {2}% {1}", DateTime.Now, url, lastprogress);
                            progressHandler(lastprogress);
                        }
                    }
                }
                Trace.TraceInformation("{0} read length {1} total {2}/{3} {4}", DateTime.Now, size, iTotalSize, total, DOWNLOAD_PART_SIZE);
            }
            catch (Exception e2)
            {
                Trace.TraceError("{0} HttpDownloadImage error {1} {2}", DateTime.Now, e2.Message, e2.StackTrace);
                if (completeHandler != null)
                {
                    ApiEventArgs result3 = new ApiEventArgs(srcName);
                    result3.Success = false;
                    result3.Msg = srcName;
                    completeHandler(result3);
                }
                File.Delete(destName + ".filepart");
                return false;
            }
            finally
            {
                responseStream?.Close();
                response?.Close();
                request?.Abort();
            }
            try
            {
                if (File.Exists(destName))
                {
                    File.Delete(destName);
                }
                File.Move(destName + ".filepart", destName);
                if (total == iTotalSize)
                {
                    if (completeHandler != null)
                    {
                        ApiEventArgs result2 = new ApiEventArgs(srcName);
                        result2.Success = true;
                        result2.Msg = srcName;
                        completeHandler(result2);
                    }
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                Trace.TraceError("{0} HttpDownloadImage error2 {1} {2}", DateTime.Now, e.Message, e.StackTrace);
                if (completeHandler != null)
                {
                    ApiEventArgs result = new ApiEventArgs(srcName);
                    result.Success = false;
                    result.Msg = srcName;
                    completeHandler(result);
                }
                return false;
            }
        }
        public static bool HttpDownloadRawImage3(string baseUrl, string accessPwd, string srcName, string destName, ApiDelegate completeHandler, ApiDelegate progressHandler = null)
        {
            if (File.Exists(destName + ".filepart"))
            {
                File.Delete(destName + ".filepart");
            }
            try
            {
                string url = $"{baseUrl}/raw/{srcName}";
                string api = $"raw/{srcName}";
                Console.WriteLine("{0} HttpDownloadRawImage3 111 {1}", DateTime.Now, url);
                FileStream writer = File.OpenWrite(destName + ".filepart");
                try
                {
                    Console.WriteLine("{0} HttpDownloadRawImage3 222 {1}", DateTime.Now, url);
                    RestRequest req = new RestRequest(api, Method.GET);
                    Console.WriteLine("{0} HttpDownloadRawImage3 333 {1}", DateTime.Now, url);
                    req.ResponseWriter = delegate (Stream responseStream)
                    {
                        Console.WriteLine("{0} HttpDownloadRawImage3 444 {1}", DateTime.Now, url);
                        using (responseStream)
                        {
                            Console.WriteLine("{0} HttpDownloadRawImage3 555 {1}", DateTime.Now, url);
                            responseStream.CopyTo(writer);
                            Console.WriteLine("{0} HttpDownloadRawImage3 666 {1}", DateTime.Now, url);
                        }
                    };
                    Console.WriteLine("{0} HttpDownloadRawImage3 777 {1}", DateTime.Now, url);
                    client.DownloadData(req);
                    Console.WriteLine("{0} HttpDownloadRawImage3 888 {1}", DateTime.Now, url);
                }
                finally
                {
                    if (writer != null)
                    {
                        ((IDisposable)writer).Dispose();
                    }
                }
                Console.WriteLine("{0} HttpDownloadRawImage3 999 {1}", DateTime.Now, url);
            }
            catch (Exception e2)
            {
                Trace.TraceError("{0} HttpDownloadImage error {1} {2}", DateTime.Now, e2.Message, e2.StackTrace);
                if (completeHandler != null)
                {
                    ApiEventArgs result3 = new ApiEventArgs(srcName);
                    result3.Success = false;
                    result3.Msg = srcName;
                    completeHandler(result3);
                }
                return false;
            }
            try
            {
                if (File.Exists(destName))
                {
                    File.Delete(destName);
                }
                File.Move(destName + ".filepart", destName);
                if (completeHandler != null)
                {
                    ApiEventArgs result2 = new ApiEventArgs(srcName);
                    result2.Success = true;
                    result2.Msg = srcName;
                    completeHandler(result2);
                }
                return true;
            }
            catch (Exception e)
            {
                Trace.TraceError("{0} HttpDownloadImage error2 {1} {2}", DateTime.Now, e.Message, e.StackTrace);
                if (completeHandler != null)
                {
                    ApiEventArgs result = new ApiEventArgs(srcName);
                    result.Success = false;
                    result.Msg = srcName;
                    completeHandler(result);
                }
                return false;
            }
        }

        private static void GetResultMsg(IRestResponse res, IApiResult args)
        {
            try
            {
                if (res.StatusCode == HttpStatusCode.InternalServerError)
                {
                    try
                    {
                        JObject msgObj = JObject.Parse(res.Content);
                        args.Msg = msgObj["message"].ToString();
                        return;
                    }
                    catch
                    {
                        args.Msg = res.Content;
                        return;
                    }
                }
                if (res.StatusDescription != null)
                {
                    args.Msg = res.StatusDescription;
                }
                else if (res.ErrorMessage != null)
                {
                    args.Msg = res.ErrorMessage;
                }
            }
            catch
            {
            }
        }
    }
}
