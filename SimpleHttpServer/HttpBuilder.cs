using SimpleHttpServer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Threading.Tasks;

namespace SimpleHttpServer
{
    class HttpBuilder
    {
        private static ResourceManager _resourceManager = null;

        public static HttpResponse InternalServerError()
        {
            using (var stream = ResourceManager.GetStream("resources/pages/500.html"))
            {
                using (var reader = new StreamReader(stream))
                {
                    var content = reader.ReadToEnd();

                    return new HttpResponse()
                    {
                        ReasonPhrase = "InternalServerError",
                        StatusCode = "500",
                        ContentAsUTF8 = content
                    };
                }
            }
        }

        public static HttpResponse NotFound()
        {
            using (var stream = ResourceManager.GetStream("resources/pages/404.html"))
            {
                using (var reader = new StreamReader(stream))
                {
                    var content = reader.ReadToEnd();

                    return new HttpResponse()
                    {
                        ReasonPhrase = "NotFound",
                        StatusCode = "404",
                        ContentAsUTF8 = content
                    };
                }
            }
        }

        private static ResourceManager ResourceManager
        {
            get
            {
                if (_resourceManager != null) return _resourceManager;
                var a = Assembly.GetExecutingAssembly();
                _resourceManager = new ResourceManager("SimpleHttpServer.g", a);
                return _resourceManager;
            }
        }
    }
}
