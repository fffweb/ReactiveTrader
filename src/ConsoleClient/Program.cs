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
using Adaptive.ReactiveTrader.Server.ReferenceData;

namespace ConsoleClient
{
    class Test
    {
        public Test()
        {
            //var s = Program.GetConections();

            //Program.Initialize().Subscribe(
            //    _ =>
            //    {
            //        //connected sucessfully
            //        var a = Program.GetSpotStreamForConnection("USDJPY").Subscribe(p =>
            //            Console.WriteLine("ask:{0} bid:{1} CreationTimestamp {2} Mid {3},SpotDate {4},ValueDate {5},Symbol {6}", p.Ask, p.Bid, p.CreationTimestamp, p.Mid, p.SpotDate, p.ValueDate, p.Symbol)
            //           );
            //        //TODO should return disposible
            //        //return a;
            //    },
            //    ex =>
            //    {

            //    },
            //    () => //TODO: what's difference between () =>  and _ =>
            //    {
            //        Console.WriteLine("connection commpleted");
            //    });
        }
    }
    class Program
    {
        static void Main(string[] args)
        {
            conifg();
            _connectionStream.Subscribe(
                _ => Console.WriteLine("hi"),
                ex => Console.WriteLine("e"),
                () => { });
            _connectionStream.Subscribe(
                _ => Console.WriteLine("hi"),
                ex => Console.WriteLine("e"),
                () => { });
            var t = new Test();


            GetPriceStream("USDJPY").Subscribe(
                p =>
                {
                    //connected sucessfully
                    //var a = Program.GetSpotStreamForConnection("USDJPY").Subscribe(p =>
                    Console.WriteLine("ask:{0} bid:{1} CreationTimestamp {2} Mid {3},SpotDate {4},ValueDate {5},Symbol {6}", p.Ask, p.Bid, p.CreationTimestamp, p.Mid, p.SpotDate, p.ValueDate, p.Symbol);
                       //);
                    //TODO should return disposible
                    //return a;
                },
                ex =>
                {

                },
                () => //TODO: what's difference between () =>  and _ =>
                {
                    Console.WriteLine("connection commpleted");
                });
            GetPriceStream("EURUSD").Subscribe(
                p =>
                {
                    //connected sucessfully
                    //var a = Program.GetSpotStreamForConnection("USDJPY").Subscribe(p =>
                    Console.WriteLine("ask:{0} bid:{1} CreationTimestamp {2} Mid {3},SpotDate {4},ValueDate {5},Symbol {6}", p.Ask, p.Bid, p.CreationTimestamp, p.Mid, p.SpotDate, p.ValueDate, p.Symbol);
                    //);
                    //TODO should return disposible
                    //return a;
                },
                ex =>
                {

                },
                () => //TODO: what's difference between () =>  and _ =>
                {
                    Console.WriteLine("connection commpleted");
                });
            Console.ReadKey();
        }
        

        static void conifg()
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

            _hubConnection = new HubConnection(address);
            _hubConnection.Headers.Add(ServiceConstants.Server.UsernameHeader, username);

            //CreateStatus(_hubConnection).Subscribe(
            //    s => _statusStream.OnNext(new ConnectionInfo(s, address, TransportName)),
            //    _statusStream.OnError,
            //    _statusStream.OnCompleted);
            _hubConnection.Error += exception => Console.WriteLine("There was a connection error with " + address, exception);

            
            PricingHubProxy = _hubConnection.CreateHubProxy(proxy);
            _connectionStream = GetConections();
            //try
            //{
            //    //Console.WriteLine("Connecting to {0} via {1}", Address, TransportName);
            //    await _hubConnection.Start();
            //    //_statusStream.OnNext(new ConnectionInfo(ConnectionStatus.Connected, Address, TransportName));
            //    //observer.OnNext(Unit.Default);

            //}
            //catch (Exception e)
            //{
            //    //Console.WriteLine("An error occurred when starting SignalR connection", e);
            //    //observer.OnError(e);
            //}

            //Thread.Sleep(2000);



        }

        public static IObservable<int> GetConections()
        {
            return Observable.Create<int>(o =>
            {
                Console.WriteLine("Creating new connection...");
                //var connection = GetNextConnection();

                //var statusSubscription = connection.StatusStream.Subscribe(
                //    _ => { },
                //    ex => o.OnCompleted(),
                //    () =>
                //    {
                //        Console.WriteLine("Status subscription completed");
                //        o.OnCompleted();
                //    });

                var connectionSubscription =
                        Initialize().Subscribe(
                        _ => o.OnNext(1),
                        ex => o.OnCompleted(),
                        o.OnCompleted);

                return new CompositeDisposable { connectionSubscription };
            })
            //.Repeat()
            //.Replay(1);
            .Publish()
            .RefCount();
            //.LazilyConnect(_disposable);
            //return (from connection in Initialize()
            //        from t in GetSpotStreamForConnection(currencyPair)
            //        select t)
            //        //.Merge(disconnected)
            //        .Publish()
            //        .RefCount();
        }
        public static IObservable<Unit> Initialize()
        {
            if (_initialized)
            {
                throw new InvalidOperationException("Connection has already been initialized");
            }
            _initialized = true;

            return Observable.Create<Unit>(async observer =>
            {
                //_statusStream.OnNext(new ConnectionInfo(ConnectionStatus.Connecting, Address, TransportName));

                try
                {
                    //Console.WriteLine("Connecting to {0} via {1}", Address, TransportName);
                    await _hubConnection.Start();
                    //_statusStream.OnNext(new ConnectionInfo(ConnectionStatus.Connected, Address, TransportName));
                    observer.OnNext(Unit.Default);
                }
                catch (Exception e)
                {
                    Console.WriteLine("An error occurred when starting SignalR connection", e);
                    observer.OnError(e);
                }

                return Disposable.Create(() =>
                {
                    try
                    {
                        Console.WriteLine("Stoping connection...");
                        _hubConnection.Stop();
                        Console.WriteLine("Connection stopped");
                    }
                    catch (Exception e)
                    {
                        // we must never throw in a disposable
                        Console.WriteLine("An error occurred while stoping connection", e);
                    }
                });
            })
            .Repeat()
            .Replay()
            .RefCount();
        }
        public static IObservable<PriceDto> GetPriceStream(string currencyPair)
        {
            return (from connection in _connectionStream
                   from tt in GetSpotStreamForConnection(currencyPair)
                   select tt)
                    //.Merge(disconnected)
                    .Publish()
                    .RefCount();
        }
        public static IObservable<PriceDto> GetSpotStreamForConnection(string currencyPair)
        {
            return Observable.Create<PriceDto>(observer =>
            {
                //HACK convert singalr to rx, pricingHubProxy.On<PriceDto>
                // subscribe to price feed first, otherwise there is a race condition 
                var priceSubscription = PricingHubProxy.On<PriceDto>(ServiceConstants.Client.OnNewPrice, p =>
                {
                    if (p.Symbol == currencyPair)
                    {
                        observer.OnNext(p);
                    }
                });

                // send a subscription request
                //Console.WriteLine("Sending price subscription for currency pair {0}", currencyPair);
                var subscription = SendSubscription(currencyPair, PricingHubProxy)
                    .Subscribe(
                        _ => Console.WriteLine("Subscribed to {0}", currencyPair),
                        observer.OnError);


                var unsubscriptionDisposable = Disposable.Create(() =>
                {
                    // send unsubscription when the observable gets disposed
                    //Console.WriteLine("Sending price unsubscription for currency pair {0}", currencyPair);
                    SendUnsubscription(currencyPair, PricingHubProxy)
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

        private static ICurrencyPairRepository a = new CurrencyPairRepository();
        private static HubConnection _hubConnection;
        public static IHubProxy PricingHubProxy;
        private static bool _initialized = false;
        private static IObservable<int> _connectionStream; 
    }
}
