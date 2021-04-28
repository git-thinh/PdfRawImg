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

namespace PdfRawImg
{
    public class ModuleFilePdf : HttpModule
    {
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
                                    for (int i = 0; i < pageTotal; i++)
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
                        if (!string.IsNullOrEmpty(key))
                        {
                            var arr = m_files.Keys.Where(x => x.Contains(key)).ToArray();

                            if (m_files.TryGetValue(imgName, out buffer) && buffer.Length > 0)
                            {
                                response.Status = HttpStatusCode.OK;
                                response.ContentType = "image/jpeg";
                                response.Body.Write(buffer, 0, buffer.Length);
                            }
                        }
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


    class Program
    {
        static void Main(string[] args)
        {
            HttpServer server = new HttpServer();
            server.Add(new ModuleFilePdf());
            server.Start(IPAddress.Any, 12345);
            Console.ReadLine();

        }

    }
}
