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
    public class HighLowRangeBot : Robot
    {
        //[Parameter("Martingale Multiplier", DefaultValue = 1)]
        //public double Multiplier { get; set; }

        [Parameter("Initial Quantity (Lots)", DefaultValue = 0.1, MinValue = 0.01, Step = 0.01)]
        public double InitialQuantity { get; set; }

        [Parameter("Periods", DefaultValue = 50, MinValue = 1)]
        public int Periods { get; set; }

        [Parameter("Entry Point (%)", DefaultValue = 10)]
        public double Entry { get; set; }

        [Parameter("Stop Loss (%)", DefaultValue = -50)]
        public double StopLoss { get; set; }

        [Parameter("Take Profit (%)", DefaultValue = 70)]
        public double TakeProfit { get; set; }

        [Parameter("Minutes between trades", DefaultValue = 0)]
        public int MinutesBetweenTrades { get; set; }

        private Queue<double> HighArray = new Queue<double>();
        private Queue<double> LowArray = new Queue<double>();
        private double HighArrayAverage;
        private double LowArrayAverage;
        private double HighLowRange;
        private double BuyLevel;
        private double BuyTPLevel;
        private double BuySLLevel;
        private double SellLevel;
        private double SellTPLevel;
        private double SellSLLevel;
        private double LastTickPrice;
        private double CurrentPrice;
        private double VolumeMultiplier = 1;
        private int Multiplier = 1;
        private bool BuyFlag;
        private bool SellFlag;
        private Position OpenPosition;
        private DateTime TimeForNewPosition;

        protected override void OnStart()
        {
            // Creating the series of highs and lows to calculate the average.
            for (int i = Periods; i > 0; i--)
            {
                HighArray.Enqueue(MarketSeries.High.Last(i));
                LowArray.Enqueue(MarketSeries.Low.Last(i));
            }
            UpdateAverages();
        }

        protected override void OnTick()
        {
            CurrentPrice = MarketSeries.Close.LastValue;
            if (CurrentPrice > HighArrayAverage)
                SellFlag = true;
            else if (CurrentPrice < LowArrayAverage)
                BuyFlag = true;

            // Checking trading availability.
            if (Server.Time >= TimeForNewPosition && OpenPosition == null)
            {
                //Checking for buying signals.
                if (CurrentPrice >= BuyLevel && LastTickPrice < BuyLevel)
                {
                    ExecuteOrder(TradeType.Buy);
                    BuyFlag = false;
                }

                //Checking for selling signals.
                if (CurrentPrice <= SellLevel && LastTickPrice > SellLevel)
                {
                    ExecuteOrder(TradeType.Sell);
                    SellFlag = false;
                }
            }

            if (OpenPosition != null)
            {
                // Closing bullish trades.
                if (OpenPosition.TradeType == TradeType.Buy)
                {
                    if (CurrentPrice >= BuyTPLevel)
                    {
                        CloseOpenPosition();
                        VolumeMultiplier = 1;
                    }
                    else if (CurrentPrice <= BuySLLevel)
                    {
                        CloseOpenPosition();
                        VolumeMultiplier = Multiplier * VolumeMultiplier;
                    }
                }
                // Closing bearish trades.
                else if (OpenPosition.TradeType == TradeType.Sell)
                {
                    if (CurrentPrice <= SellTPLevel)
                    {
                        CloseOpenPosition();
                        VolumeMultiplier = 1;
                    }
                    else if (CurrentPrice >= SellSLLevel)
                    {
                        CloseOpenPosition();
                        VolumeMultiplier = Multiplier * VolumeMultiplier;
                    }
                }
            }

            LastTickPrice = CurrentPrice;
        }

        protected override void OnBar()
        {
            UpdateBars();
            UpdateSL_TP();
        }

        protected override void OnStop()
        {
            // Put your deinitialization logic here
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
            var Result = ClosePosition(OpenPosition);
            if (Result.IsSuccessful)
            {
                OpenPosition = null;
                TimeForNewPosition = AddWorkMinutes(Server.Time, MinutesBetweenTrades);
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

        private void UpdateAverages()
        {
            HighArrayAverage = HighArray.Average();
            LowArrayAverage = LowArray.Average();
            HighLowRange = (HighArrayAverage - LowArrayAverage);
        }

        private void UpdateBars()
        {
            HighArray.Dequeue();
            HighArray.Enqueue(MarketSeries.High.Last(1));

            LowArray.Dequeue();
            LowArray.Enqueue(MarketSeries.Low.Last(1));

            UpdateAverages();
        }

        private void UpdateSL_TP()
        {
            BuyLevel = LowArrayAverage + (Entry / 100 * HighLowRange);
            BuyTPLevel = LowArrayAverage + (TakeProfit / 100 * HighLowRange);
            BuySLLevel = LowArrayAverage + (StopLoss / 100 * HighLowRange);
            SellLevel = HighArrayAverage - (Entry / 100 * HighLowRange);
            SellTPLevel = HighArrayAverage - (TakeProfit / 100 * HighLowRange);
            SellSLLevel = HighArrayAverage - (StopLoss / 100 * HighLowRange);
        }
    }
}
