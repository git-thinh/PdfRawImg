using host.Http;
using host.Http.HttpModules;
using host.Http.Sessions;
using PdfiumViewer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using TeamDev.Redis;

namespace PdfRawImg
{
    public class ModuleFilePdf : HttpModule
    {
        const int MAX_ = 5;
        static ConcurrentDictionary<string, byte[]> m_files = new ConcurrentDictionary<string, byte[]>() { };
        public override bool Process(IHttpRequest request, IHttpResponse response, IHttpSession session)
        {
            byte[] buffer;
            string file, key, page;
            try
            {
                switch (request.Uri.AbsolutePath)
                {
                    case "/pdf2img":
                        file = request.QueryString["file"].Value;
                        if (!string.IsNullOrEmpty(file))
                        {
                            file = HttpUtility.UrlDecode(file);
                            if (File.Exists(file))
                            {
                                using (var doc = PdfDocument.Load(file))
                                {
                                    int pageTotal = doc.PageCount;
                                    key = DocumentStatic.buildId(pageTotal, new FileInfo(file).Length);

                                    int dpiX = 96, dpiY = 96;
                                    int max = pageTotal;
                                    if (pageTotal > MAX_) max = MAX_; else max = pageTotal;
                                    for (int i = 0; i < max; i++)
                                    {
                                        var p = doc.PageSizes[i];
                                        using (var image = doc.Render(i, (int)p.Width, (int)p.Height, dpiX, dpiY, false))
                                        {
                                            using (var ms = new MemoryStream())
                                            {
                                                image.Save(ms, ImageFormat.Jpeg);
                                                m_files.TryAdd(string.Format("{0}-{1}", key, i), ms.ToArray());
                                            }
                                        }
                                    }
                                }

                                response.Status = HttpStatusCode.OK;
                                buffer = Encoding.ASCII.GetBytes("OK:" + key);
                                response.Body.Write(buffer, 0, buffer.Length);
                            }
                            return true;
                        }
                        break;
                    case "/image":
                        key = request.QueryString["key"].Value;
                        page = request.QueryString["page"].Value;
                        string imgName = string.Format("{0}-{1}", key, page);
                        if (m_files.ContainsKey(imgName))
                        {
                            if (m_files.TryGetValue(imgName, out buffer) && buffer.Length > 0)
                            {
                                response.Status = HttpStatusCode.OK;
                                response.ContentType = "image/jpeg";
                                response.Body.Write(buffer, 0, buffer.Length);
                            }
                        }
                        break;
                    case "/clear":
                        key = request.QueryString["key"].Value;
                        if (string.IsNullOrEmpty(key))
                            m_files.Clear();
                        else
                        {
                            var arr = m_files.Keys.Where(x => x.Contains(key)).ToArray();
                            foreach (var name in arr) m_files.TryRemove(name, out byte[] bf);
                        }
                        response.Status = HttpStatusCode.OK;
                        buffer = Encoding.ASCII.GetBytes("OK");
                        response.Body.Write(buffer, 0, buffer.Length);
                        break;
                }
            }
            catch (Exception ex)
            {
                response.Status = HttpStatusCode.OK;
                buffer = Encoding.ASCII.GetBytes("ERR:" + ex.Message + Environment.NewLine + ex.StackTrace);
                response.Body.Write(buffer, 0, buffer.Length);
            }
            return false;
        }
    }

    public class myEntity
    {
        [DocumentStoreKey]
        public string mykeyproperty { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var redis = new RedisDataAccessProvider();
            redis.Configuration.Host = "127.0.0.1";
            redis.Configuration.Port = 1000;
            redis.Connect();

            //redis.SendCommand(RedisCommand.SET, "mykey", "myvalue");

            redis.WaitComplete();


            HttpServer server = new HttpServer();
            server.Add(new ModuleFilePdf());
            server.Start(IPAddress.Any, 12345);
            Console.ReadLine();
        }
    }
}
