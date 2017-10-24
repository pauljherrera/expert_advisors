using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
    public class RasmussenMartingalev4 : Robot
    {
        [Parameter("- - - Money Management Variables - - -", DefaultValue = "")]
        public string DoNothing01 { get; set; }

        [Parameter("Initial Quantity (Lots)", DefaultValue = 1, MinValue = 0.01, Step = 0.01)]
        public double InitialQuantity { get; set; }

        [Parameter("Automatic Lots Management", DefaultValue = true)]
        public bool AutomaticMoneyManagementFlag { get; set; }

        [Parameter("Restart Lots Management", DefaultValue = false)]
        public bool RestartLotsManagement { get; set; }

        [Parameter("Martingale Multiplier", DefaultValue = 2.0)]
        public double Multiplier { get; set; }

        [Parameter("Stop Loss", DefaultValue = 100)]
        public double StopLoss { get; set; }

        [Parameter("Trailing Stop Loss", DefaultValue = 20.0)]
        public double TrailingStop { get; set; }

        [Parameter("Take Profit", DefaultValue = 100)]
        public double TakeProfit { get; set; }

        [Parameter("Martingale Overrides Take Profit?", DefaultValue = true)]
        public bool TPOverridenFlag { get; set; }

        [Parameter("Max Martingale attempts", DefaultValue = 4)]
        public int MaxMartingale { get; set; }

        [Parameter("- - - Price Action Variables - - -", DefaultValue = "")]
        public string DoNothing02 { get; set; }

        [Parameter("Number of Candles", DefaultValue = 5, MinValue = 1)]
        public int CandlesNumber { get; set; }

        [Parameter("Follow the Trend", DefaultValue = true)]
        public bool FollowTrend { get; set; }

        [Parameter("Ignore candle direction", DefaultValue = false)]
        public bool IgnoreDirectionFlag { get; set; }

        [Parameter("Use candle difference?", DefaultValue = false)]
        public bool UseCandleDifference { get; set; }

        [Parameter("Candle Difference (in %)", DefaultValue = 50)]
        public double DifferenceBetweenCandles { get; set; }

        [Parameter("Filter tiny candles?", DefaultValue = true)]
        public bool FilterTinyCandles { get; set; }

        [Parameter("Minimum Last Candle Size (in Pips)", DefaultValue = 3)]
        public double MinCandleSize { get; set; }

        [Parameter("- - - Time Sensitive Variables - - -", DefaultValue = "")]
        public string DoNothing03 { get; set; }

        [Parameter("Minutes between trades", DefaultValue = 0)]
        public int MinutesBetweenTrades { get; set; }

        [Parameter("Drop boring trades?", DefaultValue = true)]
        public bool DropTrades { get; set; }

        [Parameter("Boring Trade Duration (Minutes)", DefaultValue = 30, MinValue = 1)]
        public int BoringDuration { get; set; }

        [Parameter("Martingale effect after a boring trade?", DefaultValue = false)]
        public bool MartingaleBoringTradesFlag { get; set; }

        [Parameter("Cycle Period (in hours)", DefaultValue = 24, MinValue = 1)]
        public int Cycle { get; set; }

        [Parameter("Martingale Overrides Cycle Reset?", DefaultValue = true)]
        public bool OverrideCycle { get; set; }

        [Parameter("Friday Shutdown", DefaultValue = true)]
        public bool FridayShutdownFlag { get; set; }

        [Parameter("Friday Shutdown Time", DefaultValue = 21)]
        public int EndOfWeekHour { get; set; }

        private double BarArrayAverage;
        private double StaticVolumeMultiplier = 1;
        private double VolumeMultiplier = 1;
        private double MaxProfit = 0;
        private double CycleProfit = 0;
        private double InitialStopLoss;
        private double InitialTakeProfit;
        private double InitialEquity;
        private double LastCandleSize;
        private Queue<double> BarArray = new Queue<double>();
        private bool TakeProfitFlag = true;
        private bool TrailingStopFlag = false;
        private bool CycleOverridenFlag = false;
        private bool FirstPositionFlag = true;
        private int CurrentMartingale = 0;
        private string volume_file;
        private DateTime InitialTime;
        private DateTime CycleEndTime;
        private DateTime TimeForNewPosition;
        private DateTime BoringTradeTimer;
        private TimeSpan EndOfWeek;
        private Position OpenPosition;

        protected override void OnStart()
        {
            EndOfWeek = TimeSpan.FromHours(EndOfWeekHour) - TimeSpan.FromMinutes(1);
            InitialStopLoss = StopLoss;
            InitialTakeProfit = TakeProfit;
            InitializeCycle();

            for (int i = 1; i <= CandlesNumber; i++)
            {
                BarArray.Enqueue(GetBarHeight(i));
            }

            //Setting the initial equity and volume.
            string path = Directory.GetCurrentDirectory();
            string equity_file = path + "\\RasMart_equity.bin";
            volume_file = path + "\\RasMart_volume.bin";
            if (RestartLotsManagement == true)
            {
                //Equity.
                InitialEquity = Account.Equity;
                WriteToBinaryFile(equity_file, InitialEquity);
                Print("Initial equity is {0}", InitialEquity);

                //Volume.
                WriteToBinaryFile(volume_file, VolumeMultiplier);
                Print("Initial volume multiplier is {0}", VolumeMultiplier);
            }
            else
            {
                //Equity.
                InitialEquity = ReadFromBinaryFile<double>(equity_file);
                Print("Initial equity is {0}", InitialEquity);

                //Volume.
                VolumeMultiplier = ReadFromBinaryFile<double>(volume_file);
                Print("Initial volume multiplier is {0}", VolumeMultiplier);

                //Stop Loss and TP.
                StopLoss = InitialStopLoss * VolumeMultiplier;
                TakeProfit = TPOverridenFlag ? InitialStopLoss * VolumeMultiplier : InitialTakeProfit * VolumeMultiplier;
            }
        }

        protected override void OnBar()
        {
            UpdateBars();

            // Closes positions at the end of the week
            if (Server.Time.DayOfWeek == DayOfWeek.Friday && Server.Time.TimeOfDay >= EndOfWeek && FridayShutdownFlag == true)
            {
                if (OpenPosition != null)
                {
                    CloseOpenPosition();
                    ResetTPFlags();
                }
            }
            // Normal trade logic during the week.
            else
            {
                if (Server.Time >= TimeForNewPosition)
                {
                    if (IgnoreDirectionFlag == true)
                    {
                        //...for a bullish trend.
                        if (LastCandleSize > 0 && OpenPosition == null)
                        {
                            //Checking the trend or countertrend variable.
                            var _TradeType = FollowTrend ? TradeType.Buy : TradeType.Sell;

                            FilterAndExecuteTrades(_TradeType);
                        }
                        //...for a bearish trend.
                        else if (LastCandleSize < 0 && OpenPosition == null)
                        {
                            //Checking the trend or countertrend variable.
                            var _TradeType = FollowTrend ? TradeType.Sell : TradeType.Buy;

                            FilterAndExecuteTrades(_TradeType);
                        }
                    }
                    else
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
        }

        protected override void OnTick()
        {
            Print(CurrentMartingale);
            // Closes positions at the end of the week
            if (Server.Time.DayOfWeek == DayOfWeek.Friday && Server.Time.TimeOfDay >= EndOfWeek && FridayShutdownFlag == true)
            {
                if (OpenPosition != null)
                {
                    Print("Closing all positions");
                    CloseOpenPosition();
                    ResetTPFlags();
                }
            }
            // Normal trade logic during the week.
            else
            {
                //Checking if the cycle has ended;
                if (Server.Time >= CycleEndTime && CycleOverridenFlag == false)
                {
                    CloseOpenPosition();
                    ResetTPFlags();
                    ResetMartingaleVariables();
                    InitializeCycle();
                }

                // Closing trade logic.

                //Checks if the TS is active.
                if (TrailingStopFlag == true)
                {
                    UpdateMaxProfit();
                    UpdateStopLoss();
                }

                //Checks if there's an open position.
                if (OpenPosition != null)
                {
                    // Activates TS when TP is reached.
                    if (OpenPosition.NetProfit >= TakeProfit && TakeProfitFlag == true)
                    {
                        ActivateTrailingStop();
                    }

                    // Stop Loss logic.
                    else if (OpenPosition.NetProfit <= -StopLoss)
                    {
                        CloseOpenPosition();

                        // If the TP was never reached, the position volume is multiplied.
                        if (TakeProfitFlag == true && CurrentMartingale < MaxMartingale)
                            ApplyMartingaleEffect();
                        else
                            ResetMartingaleVariables();

                        ResetTPFlags();
                    }
                    //Closing boring positions.
                    else if (FirstPositionFlag == true && DropTrades == true && Server.Time >= BoringTradeTimer)
                    {
                        var LoserTrade = OpenPosition.NetProfit < 0;
                        CloseOpenPosition();
                        if (MartingaleBoringTradesFlag == true && LoserTrade == true)
                            ApplyMartingaleEffect();
                        ResetTPFlags();
                    }
                }
            }
        }

        protected override void OnStop()
        {
            Print("Closing volume multiplier is {0}", VolumeMultiplier);
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

        private void ApplyMartingaleEffect()
        {
            // This volume multiplier will be affected by the automatic money management.
            VolumeMultiplier = Multiplier * StaticVolumeMultiplier;
            Print("New volume multiplier is {0}", VolumeMultiplier);
            // This volume multiplier will NOT be affected by the automatic money management.
            StaticVolumeMultiplier = Multiplier * StaticVolumeMultiplier;
            StopLoss = InitialStopLoss * VolumeMultiplier;
            TakeProfit = TPOverridenFlag ? InitialStopLoss * VolumeMultiplier : InitialTakeProfit * VolumeMultiplier;
            FirstPositionFlag = false;

            if (OverrideCycle == true)
                CycleOverridenFlag = true;
            if (AutomaticMoneyManagementFlag == true)
                AutomateMoneyManagement();

            // Saving volume multiplier state and updating number of martingales.
            WriteToBinaryFile(volume_file, VolumeMultiplier);
            ++CurrentMartingale;
        }

        private void AutomateMoneyManagement()
        {
            Print("Watch these rebels (Equity and Initial Equity): {0}, {1}", Account.Equity, InitialEquity);
            var MMMultiplier = Account.Equity >= InitialEquity ? Account.Equity / InitialEquity : 1;
            Print("Actual MMM Multiplier is: {0}", MMMultiplier);
            VolumeMultiplier = VolumeMultiplier * MMMultiplier;
            Print("New money managed volume multiplier is {0}", VolumeMultiplier);
            StopLoss = StopLoss * MMMultiplier;
            TakeProfit = TakeProfit * MMMultiplier;
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
            Print("Volume multiplier when executing order is {0}", VolumeMultiplier);
            var Result = ExecuteMarketOrder(_TradeType, Symbol, Symbol.NormalizeVolume(Symbol.QuantityToVolume(InitialQuantity * VolumeMultiplier)));
            if (Result.IsSuccessful)
            {
                OpenPosition = Result.Position;
                if (FirstPositionFlag == true)
                {
                    BoringTradeTimer = AddWorkMinutes(Server.Time, BoringDuration);
                }
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
                if (Math.Abs(BarArray.ToList()[CandlesNumber - 1]) > MinCandleSize * Symbol.PipSize)
                    ExecuteOrder(_TradeType);
            }
            else
                ExecuteOrder(_TradeType);
        }

        private double GetBarHeight(int index)
        {
            double Close = MarketSeries.Close.Last(1);
            double Open = MarketSeries.Open.Last(1);
            LastCandleSize = Close - Open;
            if (IgnoreDirectionFlag)
                return Math.Abs(LastCandleSize);
            else
                return LastCandleSize;
        }

        private void InitializeCycle()
        {
            InitialTime = Server.Time;
            CycleEndTime = AddWorkHours(InitialTime, Cycle);
            CycleProfit = 0;
            Print("A new cycle began. Ends on {0}", CycleEndTime);
        }

        private void ResetTPFlags()
        {
            TakeProfitFlag = true;
            TrailingStopFlag = false;
        }

        private void ResetMartingaleVariables()
        {
            VolumeMultiplier = 1;
            StaticVolumeMultiplier = 1;
            StopLoss = InitialStopLoss;
            TakeProfit = InitialTakeProfit;
            CycleOverridenFlag = false;
            FirstPositionFlag = true;
            CurrentMartingale = 0;
            if (AutomaticMoneyManagementFlag == true)
                AutomateMoneyManagement();
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

        public static void WriteToBinaryFile<T>(string filePath, T objectToWrite, bool append = false)
        {
            using (Stream stream = File.Open(filePath, append ? FileMode.Append : FileMode.Create))
            {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                binaryFormatter.Serialize(stream, objectToWrite);
            }
        }

        public static T ReadFromBinaryFile<T>(string filePath)
        {
            using (Stream stream = File.Open(filePath, FileMode.Open))
            {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                return (T)binaryFormatter.Deserialize(stream);
            }
        }
    }
}


