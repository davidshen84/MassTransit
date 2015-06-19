// Copyright 2007-2015 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit.Transports.InMemory
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Monitoring.Introspection;


    /// <summary>
    /// Caches InMemory transport instances so that they are only created and used once
    /// </summary>
    public class InMemoryTransportCache :
        IReceiveTransportProvider,
        ISendTransportProvider,
        IBusHostControl
    {
        readonly Uri _baseUri = new Uri("loopback://localhost/");
        readonly ConcurrentDictionary<string, InMemoryTransport> _transports;

        public InMemoryTransportCache()
        {
            _transports = new ConcurrentDictionary<string, InMemoryTransport>(StringComparer.OrdinalIgnoreCase);
        }

        public IEnumerable<Uri> TransportAddresses
        {
            get { return _transports.Keys.Select(x => new Uri(_baseUri, x)); }
        }

        async Task IProbeSite.Probe(ProbeContext context)
        {
            ProbeContext scope = context.CreateScope("host");
            scope.Set(new
            {
                Type = "In Memory",
            });

            foreach (var transport in _transports)
            {
                var transportScope = scope.CreateScope("queue");
                transportScope.Set(new
                {
                    Name = transport.Key,
                });
            }
        }

        public HostHandle Start()
        {
            return new Handle(this);
        }

        public IReceiveTransport GetReceiveTransport(string queueName)
        {
            return _transports.GetOrAdd(queueName, name => new InMemoryTransport(new Uri(_baseUri, name)));
        }

        public async Task<ISendTransport> GetSendTransport(Uri address)
        {
            string queueName = address.AbsolutePath;
            if (queueName.StartsWith("/"))
                queueName = queueName.Substring(1);

            return _transports.GetOrAdd(queueName, name => new InMemoryTransport(new Uri(_baseUri, name)));
        }


        class Handle :
            HostHandle
        {
            readonly InMemoryTransportCache _cache;

            public Handle(InMemoryTransportCache cache)
            {
                _cache = cache;
            }

            public async Task Stop(CancellationToken cancellationToken = default(CancellationToken))
            {
                Parallel.ForEach(_cache._transports.Values, x => x.Dispose());
            }

            public void Dispose()
            {
                Parallel.ForEach(_cache._transports.Values, x => x.Dispose());
            }
        }
    }
}