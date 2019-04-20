using Adaptive.ReactiveTrader.Client.Domain;
using Adaptive.ReactiveTrader.Shared.DTO.Pricing;
using Adaptive.ReactiveTrader.Shared.Logging;
using Adaptive.ReactiveTrader.Client.Domain.ServiceClients;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Client;
using System.Reactive.Linq;
using Adaptive.ReactiveTrader.Shared;
using System.Reactive.Disposables;
using System.Reactive;
using Adaptive.ReactiveTrader.Client.Domain.Transport;
using Adaptive.ReactiveTrader.Shared.Extensions;
using System.Threading;

namespace ConsoleClient
{
    class Program
    {
        static void Main(string[] args)
        {
            init();
            Console.ReadKey();
        }

        static async void init()
        {
            //var _loggerFactory =  new DebugLoggerFactory();
            //var _log = _loggerFactory.Create(typeof(ReactiveTrader));
            //var _connectionProvider = new ConnectionProvider(username, servers, _loggerFactory);

            //var pricingServiceClient = new PricingServiceClient(_connectionProvider, ILoggerFactory);
            //IObservable<PriceDto> a = pricingServiceClient.GetSpotStream("currencyPair");
            var username = "WPF-" + new Random().Next(1000);

            var address = "http://localhost:8080"; //"http://111.230.221.237";//
            var proxy = ServiceConstants.Server.PricingHub;// "stockTicker";// )
                                                           // ReactiveTrader Server gui singalr version is 1.2, while client version is 2.0
                                                           //"You are using a version of the client that isn't compatible with the server. Client version 2.1, server version 1.3."

            var _hubConnection = new HubConnection(address);
            _hubConnection.Headers.Add(ServiceConstants.Server.UsernameHeader, username);

            //CreateStatus(_hubConnection).Subscribe(
            //    s => _statusStream.OnNext(new ConnectionInfo(s, address, TransportName)),
            //    _statusStream.OnError,
            //    _statusStream.OnCompleted);
            _hubConnection.Error += exception => Console.WriteLine("There was a connection error with " + address, exception);

            
            var PricingHubProxy = _hubConnection.CreateHubProxy(proxy);
            try
            {
                //_log.InfoFormat("Connecting to {0} via {1}", Address, TransportName);
                await _hubConnection.Start();
                //_statusStream.OnNext(new ConnectionInfo(ConnectionStatus.Connected, Address, TransportName));
                //observer.OnNext(Unit.Default);

            }
            catch (Exception e)
            {
                //_log.Error("An error occurred when starting SignalR connection", e);
                //observer.OnError(e);
            }

            Thread.Sleep(2000);

            var a = GetSpotStreamForConnection("USDJPY", PricingHubProxy).Subscribe(p => Console.WriteLine(p));
        }
        private static IObservable<PriceDto> GetSpotStreamForConnection(string currencyPair, IHubProxy pricingHubProxy)
        {
            return Observable.Create<PriceDto>(observer =>
            {
                //HACK convert singalr to rx, pricingHubProxy.On<PriceDto>
                // subscribe to price feed first, otherwise there is a race condition 
                var priceSubscription = pricingHubProxy.On<PriceDto>(ServiceConstants.Client.OnNewPrice, p =>
                {
                    if (p.Symbol == currencyPair)
                    {
                        observer.OnNext(p);
                    }
                });

                // send a subscription request
                //_log.InfoFormat("Sending price subscription for currency pair {0}", currencyPair);
                var subscription = SendSubscription(currencyPair, pricingHubProxy)
                    .Subscribe(
                        _ => Console.WriteLine("Subscribed to {0}", currencyPair),
                        observer.OnError);


                var unsubscriptionDisposable = Disposable.Create(() =>
                {
                    // send unsubscription when the observable gets disposed
                    //_log.InfoFormat("Sending price unsubscription for currency pair {0}", currencyPair);
                    SendUnsubscription(currencyPair, pricingHubProxy)
                        .Subscribe(
                            _ => Console.WriteLine("Unsubscribed from {0}", currencyPair),
                            ex =>
                                Console.WriteLine("An error occurred while sending unsubscription request for {0}:{1}", currencyPair, ex.Message));
                });

                return new CompositeDisposable { priceSubscription, unsubscriptionDisposable, subscription };
            })
            .Publish()
            .RefCount();
        }

        private static IObservable<Unit> SendSubscription(string currencyPair, IHubProxy pricingHubProxy)
        {
            return Observable.FromAsync(
                () => pricingHubProxy.Invoke(ServiceConstants.Server.SubscribePriceStream,
                new PriceSubscriptionRequestDto { CurrencyPair = currencyPair }));
        }

        private static IObservable<Unit> SendUnsubscription(string currencyPair, IHubProxy pricingHubProxy)
        {
            return Observable.FromAsync(
                () => pricingHubProxy.Invoke(ServiceConstants.Server.UnsubscribePriceStream,
                new PriceSubscriptionRequestDto { CurrencyPair = currencyPair }));
        }

        private static IObservable<ConnectionStatus> CreateStatus(HubConnection _hubConnection)
        {
            var closed = Observable.FromEvent(h => _hubConnection.Closed += h, h => _hubConnection.Closed -= h).Select(_ => ConnectionStatus.Closed);
            var connectionSlow = Observable.FromEvent(h => _hubConnection.ConnectionSlow += h, h => _hubConnection.ConnectionSlow -= h).Select(_ => ConnectionStatus.ConnectionSlow);
            var reconnected = Observable.FromEvent(h => _hubConnection.Reconnected += h, h => _hubConnection.Reconnected -= h).Select(_ => ConnectionStatus.Reconnected);
            var reconnecting = Observable.FromEvent(h => _hubConnection.Reconnecting += h, h => _hubConnection.Reconnecting -= h).Select(_ => ConnectionStatus.Reconnecting);
            return Observable.Merge(closed, connectionSlow, reconnected, reconnecting)
                .TakeUntilInclusive(status => status == ConnectionStatus.Closed); // complete when the connection is closed (it's terminal, SignalR will not attempt to reconnect anymore)
        }
    }
}
