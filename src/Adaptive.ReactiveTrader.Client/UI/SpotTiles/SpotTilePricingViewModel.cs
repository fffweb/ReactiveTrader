﻿using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Adaptive.ReactiveTrader.Client.Concurrency;
using Adaptive.ReactiveTrader.Client.Domain;
using Adaptive.ReactiveTrader.Client.Domain.Instrumentation;
using Adaptive.ReactiveTrader.Client.Domain.Models;
using Adaptive.ReactiveTrader.Client.Domain.Models.Execution;
using Adaptive.ReactiveTrader.Client.Domain.Models.Pricing;
using Adaptive.ReactiveTrader.Client.Domain.Models.ReferenceData;
using Adaptive.ReactiveTrader.Shared.Extensions;
using Adaptive.ReactiveTrader.Shared.Logging;
using Adaptive.ReactiveTrader.Shared.UI;
using PropertyChanged;

namespace Adaptive.ReactiveTrader.Client.UI.SpotTiles
{
    [ImplementPropertyChanged]
    public class SpotTilePricingViewModel : ViewModelBase, ISpotTilePricingViewModel
    {
        public IOneWayPriceViewModel Bid { get; private set; }
        public IOneWayPriceViewModel Ask { get; private set; }
        public string Notional { get; set; }
        public string Spread { get; private set; }
        public string DealtCurrency { get; private set; }
        public PriceMovement Movement { get; private set; }
        public string SpotDate { get; private set; }
        public bool IsSubscribing { get; private set; }
        public bool IsStale { get; private set; }

        private readonly SerialDisposable _priceSubscription;
        private readonly ICurrencyPair _currencyPair;
        private readonly ISpotTileViewModel _parent;
        private readonly IPriceLatencyRecorder _priceLatencyRecorder;
        private readonly IConcurrencyService _concurrencyService;
        private readonly IConstantRatePump _constantRatePump;
        private decimal? _previousRate;
        private SpotTileSubscriptionMode _subscriptionMode;

        private volatile IPrice _latestPrice;
        private IPrice _currentPrice;
        private readonly ILog _log;
        //HACK: call stack >	Adaptive.ReactiveTrader.Client.dll!Adaptive.ReactiveTrader.Client.UI.SpotTiles.SpotTilePricingViewModel.SpotTilePricingViewModel(Adaptive.ReactiveTrader.Client.Domain.Models.ReferenceData.ICurrencyPair currencyPair, Adaptive.ReactiveTrader.Client.UI.SpotTiles.SpotTileSubscriptionMode spotTileSubscriptionMode, Adaptive.ReactiveTrader.Client.UI.SpotTiles.ISpotTileViewModel parent, System.Func<Adaptive.ReactiveTrader.Client.Domain.Models.Direction, Adaptive.ReactiveTrader.Client.UI.SpotTiles.ISpotTilePricingViewModel, Adaptive.ReactiveTrader.Client.UI.SpotTiles.IOneWayPriceViewModel> oneWayPriceFactory, Adaptive.ReactiveTrader.Client.Domain.IReactiveTrader reactiveTrader, Adaptive.ReactiveTrader.Client.Concurrency.IConcurrencyService concurrencyService, Adaptive.ReactiveTrader.Client.Concurrency.IConstantRatePump constantRatePump, Adaptive.ReactiveTrader.Shared.Logging.ILoggerFactory loggerFactory) Line 51	C#
        //      Adaptive.ReactiveTrader.Client.dll!Adaptive.ReactiveTrader.Client.UI.SpotTiles.SpotTileViewModel.SpotTileViewModel(Adaptive.ReactiveTrader.Client.Domain.Models.ReferenceData.ICurrencyPair currencyPair, Adaptive.ReactiveTrader.Client.UI.SpotTiles.SpotTileSubscriptionMode spotTileSubscriptionMode, System.Func<Adaptive.ReactiveTrader.Client.Domain.Models.ReferenceData.ICurrencyPair, Adaptive.ReactiveTrader.Client.UI.SpotTiles.SpotTileSubscriptionMode, Adaptive.ReactiveTrader.Client.UI.SpotTiles.ISpotTileViewModel, Adaptive.ReactiveTrader.Client.UI.SpotTiles.ISpotTilePricingViewModel> pricingFactory, System.Func<Adaptive.ReactiveTrader.Client.Domain.Models.Execution.ITrade, Adaptive.ReactiveTrader.Client.UI.SpotTiles.ISpotTileViewModel, Adaptive.ReactiveTrader.Client.UI.SpotTiles.ISpotTileAffirmationViewModel> affirmationFactory, System.Func<string, Adaptive.ReactiveTrader.Client.UI.SpotTiles.ISpotTileViewModel, Adaptive.ReactiveTrader.Client.UI.SpotTiles.ISpotTileErrorViewModel> errorFactory, System.Func<Adaptive.ReactiveTrader.Client.UI.SpotTiles.ISpotTileConfigViewModel> configFactory) Line 38	C#
        //      Adaptive.ReactiveTrader.Client.dll!Adaptive.ReactiveTrader.Client.UI.SpotTiles.SpotTilesViewModel.HandleCurrencyPairUpdate(Adaptive.ReactiveTrader.Client.Domain.Models.ReferenceData.ICurrencyPairUpdate update) Line 83	C#
 	    //      Adaptive.ReactiveTrader.Client.dll!Adaptive.ReactiveTrader.Client.UI.SpotTiles.SpotTilesViewModel.LoadSpotTiles.AnonymousMethod__12_0(System.Collections.Generic.IEnumerable<Adaptive.ReactiveTrader.Client.Domain.Models.ReferenceData.ICurrencyPairUpdate> currencyPairs) Line 68	C#
 	
        public SpotTilePricingViewModel(ICurrencyPair currencyPair, SpotTileSubscriptionMode spotTileSubscriptionMode, ISpotTileViewModel parent,
            Func<Direction, ISpotTilePricingViewModel, IOneWayPriceViewModel> oneWayPriceFactory,
            IReactiveTrader reactiveTrader,
            IConcurrencyService concurrencyService,
            IConstantRatePump constantRatePump,
            ILoggerFactory loggerFactory)
        {
            _currencyPair = currencyPair;
            _subscriptionMode = spotTileSubscriptionMode;
            _parent = parent;
            _priceLatencyRecorder = reactiveTrader.PriceLatencyRecorder;
            _concurrencyService = concurrencyService;
            _constantRatePump = constantRatePump;
            _log = loggerFactory.Create(typeof(SpotTilePricingViewModel));

            _priceSubscription = new SerialDisposable();
            Bid = oneWayPriceFactory(Direction.SELL, this);
            Ask = oneWayPriceFactory(Direction.BUY, this);
            Notional = "1000000";
            DealtCurrency = currencyPair.BaseCurrency;
            SpotDate = "SP";
            IsSubscribing = true;

            SubscribeForPrices();
        }

        public void Dispose()
        {
            _priceSubscription.Dispose();
        }

        public string Symbol { get { return String.Format("{0} / {1}", _currencyPair.BaseCurrency, _currencyPair.CounterCurrency); } }

        public SpotTileSubscriptionMode SubscriptionMode
        {
            get { return _subscriptionMode; }
            set
            {
                if (_subscriptionMode != value)
                {
                    _subscriptionMode = value;
                    SubscribeForPrices();
                }
            }
        }

        public SpotTileExecutionMode ExecutionMode
        {
            get { return Bid.ExecutionMode; }
            set
            {
                Bid.ExecutionMode = value;
                Ask.ExecutionMode = value;
            }
        }

        private void SubscribeForPrices()
        {
            switch (SubscriptionMode)
            {
                case SpotTileSubscriptionMode.OnDispatcher:
                    SubscribeForPricesOnDispatcher();
                    break;
                case SpotTileSubscriptionMode.ObserveLatestOnDispatcher:
                    SubscribeForPricesLatestOnDispatch();
                    break;
                case SpotTileSubscriptionMode.Conflate:
                    SubscribeForPricesConflate();
                    break;
                case SpotTileSubscriptionMode.ConstantRate:
                    SubscribeForPricesConstantRate();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void SubscribeForPricesOnDispatcher()
        {
            _priceSubscription.Disposable = _currencyPair.PriceStream
                                        .SubscribeOn(_concurrencyService.TaskPool)
                                        .ObserveOn(_concurrencyService.Dispatcher)
                                        .Subscribe(OnPrice, OnError);
        }

        private void SubscribeForPricesLatestOnDispatch()
        {
            _priceSubscription.Disposable = _currencyPair.PriceStream
                                        .SubscribeOn(_concurrencyService.TaskPool)
                                        .ObserveLatestOn(_concurrencyService.Dispatcher)
                                        .Subscribe(OnPrice, OnError);
        }

        private void SubscribeForPricesConflate()
        {
            _priceSubscription.Disposable = _currencyPair.PriceStream
                                        .SubscribeOn(_concurrencyService.TaskPool)
                                        .Conflate(TimeSpan.FromMilliseconds(125), _concurrencyService.Dispatcher)
                                        .Subscribe(OnPrice, OnError);
        }

        private void SubscribeForPricesConstantRate()
        {
            var ps = _currencyPair.PriceStream
                                  .SubscribeOn(_concurrencyService.TaskPool)
                                  .Subscribe(price =>
                                      {
                                          _latestPrice = price;
                                      }, OnError);

            var el = _constantRatePump.Tick.Subscribe(_ =>
                {
                    if (_currentPrice != _latestPrice && _latestPrice != null)
                    {
                        OnPrice(_latestPrice);
                    }
                });

            _priceSubscription.Disposable = new CompositeDisposable(ps, el);
        }

        private void OnPrice(IPrice price)
        {
            IsSubscribing = false;
            IsStale = price.IsStale;

            if (price.IsStale)
            {
                Bid.OnStalePrice();
                Ask.OnStalePrice();
                Spread = string.Empty;
                _previousRate = null;
                Movement = PriceMovement.None;
                SpotDate = "SP";
            }
            else
            {
                if (_previousRate.HasValue)
                {
                    if (price.Mid > _previousRate.Value)
                        Movement = PriceMovement.Up;
                    else if (price.Mid < _previousRate.Value)
                        Movement = PriceMovement.Down;
                    else
                        Movement = PriceMovement.None;
                }
                _previousRate = price.Mid;

                Bid.OnPrice(price.Bid);
                Ask.OnPrice(price.Ask);
                Spread = PriceFormatter.GetFormattedSpread(price.Spread, _currencyPair.RatePrecision, _currencyPair.PipsPosition);
                SpotDate = "SP. " + price.ValueDate.ToString("dd MMM");

                _priceLatencyRecorder.OnRendered(price);

            }
            _currentPrice = price;
        }


        private void OnError(Exception ex)
        {
            _log.Error("Failed to get prices for " + _currencyPair.Symbol, ex);
        }

        public void OnTrade(ITrade trade)
        {
            _parent.OnTrade(trade);
        }

        public void OnExecutionError(string message)
        {
            _parent.OnExecutionError(message);
        }
    }
}
