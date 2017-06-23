using System;
using System.Linq;
using System.Collections.Generic;

using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo
{
    [Robot("SMA + Knoxville", TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class SMAKnoxville : Robot
    {
        [Parameter("SMA Period", DefaultValue = 1000)]
        public int SMAPeriod { get; set; }

        [Parameter("Knoxville Divergence Period", DefaultValue = 30)]
        public int KDPeriod { get; set; }

        [Parameter("Exit Periods", DefaultValue = 30)]
        public int ExitPeriods { get; set; }

        [Parameter("Periods Between Trades", DefaultValue = 15)]
        public int PeriodsBetweenTrades { get; set; }

        [Parameter("Emergency Stop Loss", DefaultValue = 500)]
        public int StopLoss { get; set; }

        [Parameter("Lots", DefaultValue = 0.01, MinValue = 0.01, Step = 0.01)]
        public double Lots { get; set; }

        [Parameter("Buy Signals", DefaultValue = true)]
        public bool BuySignalsFlag { get; set; }

        [Parameter("Sell Signals", DefaultValue = true)]
        public bool SellSignalsFlag { get; set; }

        [Parameter("Reverse", DefaultValue = false)]
        public bool ReverseFlag { get; set; }

        private Queue<Position> OpenPositions = new Queue<Position>();
        private Queue<int> PositionTimers = new Queue<int>();
        private bool BullDFlag = false;
        private bool BearDFlag = false;
        private int MinPeriod = 4;
        private int CurrentPeriodsBetTrades = 0;
        private MomentumOscillator _momentum;
        private RelativeStrengthIndex _rsi;
        private SimpleMovingAverage _simpleMovingAverage;


        protected override void OnStart()
        {
        }

        protected override void OnBar()
        {
            _momentum = Indicators.MomentumOscillator(MarketSeries.Close, 20);
            _rsi = Indicators.RelativeStrengthIndex(MarketSeries.Close, 21);
            _simpleMovingAverage = Indicators.SimpleMovingAverage(MarketSeries.Close, SMAPeriod);

            BullDFlag = false;
            BearDFlag = false;
            CalculateKnoxvilleDivergence();

            // Checking entry signals.
            if (CurrentPeriodsBetTrades >= PeriodsBetweenTrades)
            {
                //Bullish signals.
                if (_simpleMovingAverage.Result.Last(1) < MarketSeries.Close.Last(1) && BullDFlag == true)
                {
                    var _TradeType = ReverseFlag ? TradeType.Sell : TradeType.Buy;
                    ExecuteOrder(_TradeType);
                }
                //Bearish signals.
                else if (_simpleMovingAverage.Result.Last(1) > MarketSeries.Close.Last(1) && BearDFlag == true)
                {
                    var _TradeType = ReverseFlag ? TradeType.Buy : TradeType.Sell;
                    ExecuteOrder(_TradeType);
                }
            }

            // Checking exit signals.
            var counter = PositionTimers.Count;
            for (int i = 0; i < counter; i++)
            {
                var timer = PositionTimers.Dequeue();
                if (timer == 0)
                {
                    CloseNextPosition();
                }
                else
                {
                    --timer;
                    PositionTimers.Enqueue(timer);
                }
            }

            ++CurrentPeriodsBetTrades;
        }

        private void CalculateKnoxvilleDivergence()
        {
            for (int i = MinPeriod; i <= KDPeriod; i++)
            {
                if (_momentum.Result.Last(1) > _momentum.Result.Last(i + 1))
                {
                    if (MarketSeries.Close.Last(1) < MarketSeries.Close.Last(i + 1))
                    {
                        if (MarketSeries.Low.Last(1) <= MarketSeries.Low.Minimum(i + 1))
                        {
                            if (_rsi.Result.Minimum(i + 1) <= 30)
                            {
                                BullDFlag = true;
                            }
                        }
                    }
                }
                else if (_momentum.Result.Last(1) < _momentum.Result.Last(i + 1))
                {
                    if (MarketSeries.Close.Last(1) > MarketSeries.Close.Last(i + 1))
                    {
                        if (MarketSeries.High.Last(1) >= MarketSeries.High.Maximum(i + 1))
                        {
                            if (_rsi.Result.Maximum(i + 1) >= 70)
                            {
                                BearDFlag = true;
                            }
                        }
                    }
                }
            }
        }

        private void CloseNextPosition()
        {
            var Result = ClosePosition(OpenPositions.Dequeue());
            if (Result.IsSuccessful)
            {
            }
        }

        private void ExecuteOrder(TradeType _TradeType)
        {
            var Result = ExecuteMarketOrder(_TradeType, Symbol, Symbol.NormalizeVolume(Symbol.QuantityToVolume(Lots)), "Trade", StopLoss, 0);
            if (Result.IsSuccessful)
            {
                OpenPositions.Enqueue(Result.Position);
                PositionTimers.Enqueue(ExitPeriods);
                CurrentPeriodsBetTrades = 0;
            }
        }
    }
}
