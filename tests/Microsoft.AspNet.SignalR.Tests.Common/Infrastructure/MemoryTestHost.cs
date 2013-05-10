﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Client.Transports;
using Microsoft.AspNet.SignalR.Configuration;
using Microsoft.AspNet.SignalR.Hosting.Memory;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.AspNet.SignalR.StressServer.Connections;
using Microsoft.AspNet.SignalR.Tests;
using Microsoft.AspNet.SignalR.Tests.Common;
using Microsoft.AspNet.SignalR.Tracing;

namespace Microsoft.AspNet.SignalR.FunctionalTests.Infrastructure
{
    public class MemoryTestHost : ITestHost
    {
        private readonly MemoryHost _host;
        private readonly TextWriterTraceListener _listener;
        private ITraceManager _traceManager;

        private static string[] _traceSources = new[] {
            "SignalR.Transports.WebSocketTransport",
            "SignalR.Transports.ServerSentEventsTransport",
            "SignalR.Transports.ForeverFrameTransport",
            "SignalR.Transports.LongPollingTransport",
            "SignalR.Transports.TransportHeartBeat"
        };

        public MemoryTestHost(MemoryHost host, string logPath)
        {
            _host = host;
            _listener = new TextWriterTraceListener(logPath + ".transports.log");
            Disposables = new List<IDisposable>();
            ExtraData = new Dictionary<string, string>();
        }

        public string Url
        {
            get
            {
                return "http://memoryhost";
            }
        }

        public IClientTransport Transport { get; set; }

        public Func<IClientTransport> TransportFactory { get; set; }

        public TextWriter ClientTraceOutput { get; set; }

        public IDictionary<string, string> ExtraData { get; private set; }

        public IDependencyResolver Resolver { get; set; }

        public IList<IDisposable> Disposables
        {
            get;
            private set;
        }

        public void Initialize(int? keepAlive,
                               int? connectionTimeout,
                               int? disconnectTimeout,
                               bool enableAutoRejoiningGroups)
        {
            var dr = Resolver ?? new DefaultDependencyResolver();
            _traceManager = dr.Resolve<ITraceManager>();
            _traceManager.Switch.Level = SourceLevels.Verbose;

            foreach (var sourceName in _traceSources)
            {
                TraceSource source = _traceManager[sourceName];
                source.Listeners.Add(_listener);
            }

            _host.Configure(app =>
            {
                var configuration = dr.Resolve<IConfigurationManager>();

                if (connectionTimeout != null)
                {
                    configuration.ConnectionTimeout = TimeSpan.FromSeconds(connectionTimeout.Value);
                }

                if (disconnectTimeout != null)
                {
                    configuration.DisconnectTimeout = TimeSpan.FromSeconds(disconnectTimeout.Value);
                }

                if (!keepAlive.HasValue)
                {
                    configuration.KeepAlive = null;
                }
                // Set only if the keep-alive was changed from the default value.
                else if (keepAlive.Value != -1)
                {
                    configuration.KeepAlive = TimeSpan.FromSeconds(keepAlive.Value);
                }

                Initializer.ConfigureRoutes(app, dr);
            });
        }

        public Task Get(string uri, bool disableWrites)
        {
            return _host.Get(uri, disableWrites);
        }

        public Task Post(string uri, IDictionary<string, string> data)
        {
            return _host.Post(uri, data, isLongRunning: false);
        }

        public void Dispose()
        {
            _host.Dispose();

            foreach (var sourceName in _traceSources)
            {
                _traceManager[sourceName].Listeners.Remove(_listener);
            }

            _listener.Dispose();

            foreach (var d in Disposables)
            {
                d.Dispose();
            }
        }

        public void Shutdown()
        {
            Dispose();
        }
    }
}
