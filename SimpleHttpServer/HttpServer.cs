// Copyright (C) 2016 by David Jeske, Barend Erasmus and donated to the public domain

using log4net;
using SimpleHttpServer;
using SimpleHttpServer.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SimpleHttpServer
{

    public class HttpServer
    {
        #region Fields

        private readonly int _port;
        private TcpListener _listener;
        private readonly HttpProcessor _processor;
        private bool _isActive = true;

        #endregion

        private static readonly ILog _log = LogManager.GetLogger(typeof(HttpServer));

        #region Public Methods
        public HttpServer(int port, List<Route> routes, Func<HttpRequest,HttpResponse> notFoundOverrideCallable = null)
        {
            _port = port;
            _processor = new HttpProcessor
            {
                NotFoundOverrideCallable = notFoundOverrideCallable
            };

            foreach (var route in routes)
            {
                _processor.AddRoute(route);
            }
        }

        public void StopListen()
        {
            _isActive = false;
            _listener.Stop();
        }

        public void Listen()
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            while (_isActive)
            {
                var s = _listener.AcceptTcpClient();
                var thread = new Thread(() => _processor.HandleClient(s));
                thread.Start();
                Thread.Sleep(1);
            }
        }

        #endregion

    }
}



