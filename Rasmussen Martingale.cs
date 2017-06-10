using System;
using System.Linq;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class RasmussenMartingale : Robot
    {
        [Parameter("Martingale Multiplier", DefaultValue = 2.0)]
        public double Multiplier { get; set; }

        [Parameter("Initial Quantity (Lots)", DefaultValue = 1, MinValue = 0.01, Step = 0.01)]
        public double InitialQuantity { get; set; }

        [Parameter("Stop Loss", DefaultValue = 100)]
        public double StopLoss { get; set; }

        [Parameter("Take Profit", DefaultValue = 100)]
        public double TakeProfit { get; set; }

        [Parameter("Trailing Stop Loss", DefaultValue = 20.0)]
        public double TrailingStop { get; set; }

        [Parameter("Follow the Trend", DefaultValue = true)]
        public bool FollowTrend { get; set; }

        [Parameter("Number of Candles", DefaultValue = 5, MinValue = 1)]
        public int CandlesNumber { get; set; }

        [Parameter("Minutes between trades", DefaultValue = 0)]
        public int MinutesBetweenTrades { get; set; }

        [Parameter("Cycle Period (in hours)", DefaultValue = 24, MinValue = 1)]
        public int Cycle { get; set; }

        [Parameter("Martingale Overrides Cycle Reset?", DefaultValue = true)]
        public bool OverrideCycle { get; set; }

        [Parameter("Shut Down Amount (in $)", DefaultValue = 500)]
        public double ShutDown { get; set; }

        [Parameter("Use candle difference?", DefaultValue = false)]
        public bool UseCandleDifference { get; set; }

        [Parameter("Difference (in %)", DefaultValue = 50)]
        public double DifferenceBetweenCandles { get; set; }

        [Parameter("Filter tiny candles?", DefaultValue = true)]
        public bool FilterTinyCandles { get; set; }

        [Parameter("Minimum Last Candle Size (Pips)", DefaultValue = 3)]
        public double MinCandleSize { get; set; }

        private double BarArrayAverage;
        private double VolumeMultiplier = 1;
        private double MaxProfit = 0;
        private double CycleProfit = 0;
        private double InitialStopLoss;
        private double InitialTakeProfit;
        private Queue<double> BarArray = new Queue<double>();
        private bool TakeProfitFlag = true;
        private bool TrailingStopFlag = false;
        private bool ShuttedDownFlag = false;
        private bool CycleOverridenFlag = false;
        private DateTime InitialTime;
        private DateTime CycleEndTime;
        private DateTime TimeForNewPosition;
        private Position OpenPosition;

        protected override void OnStart()
        {
            InitialStopLoss = StopLoss;
            InitialTakeProfit = TakeProfit;
            InitializeCycle();

            for (int i = 1; i <= CandlesNumber; i++)
            {
                BarArray.Enqueue(GetBarHeight(i));
            }
        }

        protected override void OnBar()
        {
            UpdateBars();

            if (ShuttedDownFlag == false)
            {
                if (Server.Time >= TimeForNewPosition)
                {
                    //Checking if entry condition has been met...
                    var OverZero = BarArray.Where(x => x > 0);
                    var SubZero = BarArray.Where(x => x < 0);

                    //...for a bullish trend.
                    if (OverZero.Count() == CandlesNumber && OpenPosition == null)
                    {
                        //Checking the trend or countertrend variable.
                        var _TradeType = FollowTrend ? TradeType.Buy : TradeType.Sell;

                        FilterAndExecuteTrades(_TradeType);
                    }

                    //...for a bearish trend.
                    if (SubZero.Count() == CandlesNumber && OpenPosition == null)
                    {
                        //Checking the trend or countertrend variable.
                        var _TradeType = FollowTrend ? TradeType.Sell : TradeType.Buy;

                        FilterAndExecuteTrades(_TradeType);
                    }
                }
            }
        }

        protected override void OnTick()
        {
            //Checking if the cycle has ended;
            if (Server.Time >= CycleEndTime && CycleOverridenFlag == false)
            {
                CloseOpenPosition();
                ResetFlags();
                ResetVariables();
                InitializeCycle();
            }

            if (ShuttedDownFlag == false)
            {
                //Checks if the TS is active.
                if (TrailingStopFlag == true)
                {
                    UpdateMaxProfit();
                    UpdateStopLoss();
                }

                //Checks if there's an open position.
                if (OpenPosition != null)
                {
                    if (OpenPosition.NetProfit >= TakeProfit && TakeProfitFlag == true)
                    {
                        ActivateTrailingStop();
                    }

                    if (OpenPosition.NetProfit <= -StopLoss)
                    {
                        CloseOpenPosition();

                        // If the TP was never reached, the position volume is multiplied.
                        if (TakeProfitFlag == true)
                        {
                            VolumeMultiplier = Multiplier * VolumeMultiplier;
                            StopLoss = InitialStopLoss * VolumeMultiplier;
                            TakeProfit = InitialStopLoss * VolumeMultiplier;
                            if (OverrideCycle == true)
                                CycleOverridenFlag = true;
                        }
                        else
                        {
                            ResetVariables();
                        }

                        ResetFlags();
                    }
                    else
                    {
                        //Checking the shut down properties.
                        if (CycleProfit + OpenPosition.NetProfit <= -ShutDown)
                        {
                            ShuttedDownFlag = true;
                            CloseOpenPosition();
                            Print("Shutted down until next cycle.");
                        }
                    }
                }
            }
        }

        private void ActivateTrailingStop()
        {
            TakeProfitFlag = false;
            TrailingStopFlag = true;
            MaxProfit = OpenPosition.NetProfit;
            UpdateStopLoss();
        }

        private DateTime AddWorkDays(DateTime originalDate, int workDays)
        {
            DateTime tmpDate = originalDate;
            while (workDays > 0)
            {
                tmpDate = tmpDate.AddDays(1);
                if (tmpDate.DayOfWeek < DayOfWeek.Saturday && tmpDate.DayOfWeek > DayOfWeek.Sunday)
                    workDays--;
            }
            return tmpDate;
        }

        private DateTime AddWorkHours(DateTime originalDate, int workHours)
        {
            DateTime tmpDate = originalDate;
            while (workHours > 0)
            {
                tmpDate = tmpDate.AddHours(1);
                if (tmpDate.DayOfWeek < DayOfWeek.Saturday && tmpDate.DayOfWeek > DayOfWeek.Sunday)
                    workHours--;
            }
            return tmpDate;
        }

        private DateTime AddWorkMinutes(DateTime originalDate, int workMinutes)
        {
            DateTime tmpDate = originalDate;
            while (workMinutes > 0)
            {
                tmpDate = tmpDate.AddMinutes(1);
                if (tmpDate.DayOfWeek < DayOfWeek.Saturday && tmpDate.DayOfWeek > DayOfWeek.Sunday)
                    workMinutes--;
            }
            return tmpDate;
        }

        private void CloseOpenPosition()
        {
            if (OpenPosition != null)
            {
                var Result = ClosePosition(OpenPosition);
                if (Result.IsSuccessful)
                {
                    OpenPosition = null;
                    CycleProfit += Result.Position.NetProfit;
                    TimeForNewPosition = AddWorkMinutes(Server.Time, MinutesBetweenTrades);
                }
            }
        }

        private void ExecuteOrder(TradeType _TradeType)
        {
            var Result = ExecuteMarketOrder(_TradeType, Symbol, Symbol.NormalizeVolume(Symbol.QuantityToVolume(InitialQuantity * VolumeMultiplier)));
            if (Result.IsSuccessful)
            {
                OpenPosition = Result.Position;
            }
        }

        private void FilterAndExecuteTrades(TradeType _TradeType)
        {
            //Order execution when using difference between candles.
            if (UseCandleDifference == true)
            {
                var Difference = BarArray.ToList()[CandlesNumber - 1] * 100 / BarArrayAverage - 100;
                if (Difference >= DifferenceBetweenCandles)
                    FilterTinyCandlesAndExecute(_TradeType);
            }
            else
                FilterTinyCandlesAndExecute(_TradeType);
        }

        private void FilterTinyCandlesAndExecute(TradeType _TradeType)
        {
            // Order execution when avoiding tiny candles.
            if (FilterTinyCandles == true)
            {
                if (BarArray.ToList()[CandlesNumber - 1] > MinCandleSize * Symbol.PipSize)
                    ExecuteOrder(_TradeType);
            }
            else
                ExecuteOrder(_TradeType);
        }

        private double GetBarHeight(int index)
        {
            double Close = MarketSeries.Close[MarketSeries.Close.Count - index - 1];
            double Open = MarketSeries.Open[MarketSeries.Open.Count - index - 1];
            return Close - Open;
        }

        private void InitializeCycle()
        {
            InitialTime = Server.Time;
            CycleEndTime = AddWorkHours(InitialTime, Cycle);
            ShuttedDownFlag = false;
            CycleProfit = 0;
            Print("A new cycle began. Ends on {0}", CycleEndTime);
        }

        private void ResetFlags()
        {
            TakeProfitFlag = true;
            TrailingStopFlag = false;
        }

        private void ResetVariables()
        {
            VolumeMultiplier = 1;
            StopLoss = InitialStopLoss;
            TakeProfit = InitialTakeProfit;
            CycleOverridenFlag = false;
        }

        private void UpdateBars()
        {
            BarArray.Dequeue();
            BarArrayAverage = BarArray.Average();
            BarArray.Enqueue(GetBarHeight(1));
        }

        private void UpdateMaxProfit()
        {
            MaxProfit = OpenPosition.NetProfit > MaxProfit ? OpenPosition.NetProfit : MaxProfit;
        }

        private void UpdateStopLoss()
        {
            StopLoss = -MaxProfit + TakeProfit * TrailingStop / 100;
        }
    }
}


