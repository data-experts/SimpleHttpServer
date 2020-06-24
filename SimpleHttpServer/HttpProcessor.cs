// Copyright (C) 2016 by David Jeske, Barend Erasmus and donated to the public domain

using log4net;
using SimpleHttpServer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace SimpleHttpServer
{
    public class HttpProcessor
    {

        #region Fields

        private const int MAX_POST_SIZE = 10 * 1024 * 1024; // 10MB

        private readonly List<Route> _routes = new List<Route>();

        private static readonly ILog _log = LogManager.GetLogger(typeof(HttpProcessor));

        public Func<HttpRequest, HttpResponse> NotFoundOverrideCallable = null;

        #endregion

        #region Constructors

        public HttpProcessor()
        {
        }

        #endregion

        #region Public Methods
        public void HandleClient(TcpClient tcpClient)
        {
            using (var inputStream = GetInputStream(tcpClient))
            {
                using (var outputStream = GetOutputStream(tcpClient))
                {

                    var request = GetRequest(inputStream);

                    // route and handle the request...
                    var response = RouteRequest(inputStream, outputStream, request);

                    Console.WriteLine("{0} {1}", response.StatusCode, request.Url);
                    // build a default response for errors
                    if (response.Content == null)
                    {
                        if (response.StatusCode != "200")
                        {
                            response.ContentAsUTF8 = string.Format("{0} {1} <p> {2}", response.StatusCode, request.Url, response.ReasonPhrase);
                        }
                    }

                    WriteResponse(outputStream, response);
                    outputStream.Flush();
                }
            }
        }

        // this formats the HTTP response...
        private static void WriteResponse(Stream stream, HttpResponse response)
        {
            if (response.Content == null)
            {
                response.Content = new byte[] { };
            }

            // default to text/html content type
            if (!response.Headers.ContainsKey("Content-Type"))
            {
                response.Headers["Content-Type"] = "text/html";
            }

            response.Headers["Content-Length"] = response.Content.Length.ToString();

            Write(stream, string.Format("HTTP/1.0 {0} {1}\r\n", response.StatusCode, response.ReasonPhrase));
            Write(stream, string.Join("\r\n", response.Headers.Select(x => string.Format("{0}: {1}", x.Key, x.Value))));
            Write(stream, "\r\n\r\n");

            stream.Write(response.Content, 0, response.Content.Length);
        }

        public void AddRoute(Route route) => _routes.Add(route);

        #endregion

        #region Private Methods

        private static string Readline(Stream stream)
        {
            int next_char;
            var data = "";
            while (true)
            {
                next_char = stream.ReadByte();
                if (next_char == '\n') { break; }
                if (next_char == '\r') { continue; }
                if (next_char == -1) { Thread.Sleep(1); continue; };
                data += Convert.ToChar(next_char);
            }
            return data;
        }

        private static void Write(Stream stream, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }

        protected virtual Stream GetOutputStream(TcpClient tcpClient) => tcpClient.GetStream();

        protected virtual Stream GetInputStream(TcpClient tcpClient) => tcpClient.GetStream();

        protected virtual HttpResponse RouteRequest(Stream inputStream, Stream outputStream, HttpRequest request)
        {

            var routes = _routes.Where(x => Regex.Match(request.Url, x.UrlRegex).Success).ToList();

            if (!routes.Any())
            {
                return NotFoundOverrideCallable == null ? HttpBuilder.NotFound() : NotFoundOverrideCallable(request);
            }
            var route = routes.SingleOrDefault(x => x.Method == request.Method);

            if (route == null)
                return new HttpResponse()
                {
                    ReasonPhrase = "Method Not Allowed",
                    StatusCode = "405",
                };

            // extract the path if there is one
            var match = Regex.Match(request.Url, route.UrlRegex);
            request.Path = match.Groups.Count > 1 ? match.Groups[1].Value : request.Url;

            // trigger the route handler...
            request.Route = route;
            try
            {
                return route.Callable(request);
            }
            catch (Exception ex)
            {
                _log.Error(ex);
                return HttpBuilder.InternalServerError();
            }

        }

        private HttpRequest GetRequest(Stream inputStream)
        {
            //Read Request Line
            var request = Readline(inputStream);

            var tokens = request.Split(' ');
            if (tokens.Length != 3)
            {
                throw new Exception("invalid http request line");
            }
            var method = tokens[0].ToUpper();
            var url = tokens[1];
            var protocolVersion = tokens[2];

            //Read Headers
            var headers = new Dictionary<string, string>();
            string line;
            while ((line = Readline(inputStream)) != null)
            {
                if (line.Equals(""))
                {
                    break;
                }

                var separator = line.IndexOf(':');
                if (separator == -1)
                {
                    throw new Exception("invalid http header line: " + line);
                }
                var name = line.Substring(0, separator);
                var pos = separator + 1;
                while ((pos < line.Length) && (line[pos] == ' '))
                {
                    pos++;
                }

                var value = line.Substring(pos, line.Length - pos);
                headers.Add(name, value);
            }

            string content = null;
            if (headers.ContainsKey("Content-Length"))
            {
                var totalBytes = Convert.ToInt32(headers["Content-Length"]);
                var bytesLeft = totalBytes;
                var bytes = new byte[totalBytes];

                while (bytesLeft > 0)
                {
                    var buffer = new byte[bytesLeft > 1024 ? 1024 : bytesLeft];
                    var n = inputStream.Read(buffer, 0, buffer.Length);
                    buffer.CopyTo(bytes, totalBytes - bytesLeft);

                    bytesLeft -= n;
                }

                content = Encoding.ASCII.GetString(bytes);
            }


            return new HttpRequest()
            {
                Method = method,
                Url = url,
                Headers = headers,
                Content = content
            };
        }

        #endregion


    }
}
