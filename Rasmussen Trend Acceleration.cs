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
    public class TrendAcceleration : Robot
    {
        [Parameter("Evaluation Time in seconds", DefaultValue = 15)]
        public int EvaluationTime { get; set; }

        [Parameter("Phase 2 Acceleration Threshold", DefaultValue = 1.5)]
        public double Phase2AccelerationThreshold { get; set; }

        [Parameter("Entry Acceleration Threshold", DefaultValue = 1.5)]
        public double EntryAccelerationThreshold { get; set; }

        [Parameter("Entry Acceleration Periods", DefaultValue = 1, MinValue = 0)]
        public int EntryAccelerationPeriods { get; set; }

        [Parameter("Exit Acceleration Threshold", DefaultValue = 0.5)]
        public double ExitAccelerationThreshold { get; set; }

        [Parameter("Exit Acceleration Periods", DefaultValue = 1, MinValue = 0)]
        public int ExitAccelerationPeriods { get; set; }

        [Parameter("Volume (Lots)", DefaultValue = 1, MinValue = 0.01, Step = 0.01)]
        public double Volume { get; set; }

        private double Acceleration = 0;
        private double PreviousPrice;
        private double CurrentPrice;
        private double PreviousSpeed = 0;
        private double CurrentSpeed = 0;
        private double BaseSpeed = 0;
        private int EntryAccelerationPeriodsCounter = -1;
        private int ExitAccelerationPeriodsCounter = -1;
        private bool Phase2Flag = false;
        private Position OpenPosition;


        protected override void OnStart()
        {
            // Initializing the OnTimer method.
            Timer.Start(EvaluationTime);

            // Setting initial values.
            PreviousPrice = MarketSeries.Close.LastValue;
        }

        protected override void OnTimer()
        {
            // Getting price, speed and acceleration.
            CurrentPrice = MarketSeries.Close.LastValue;
            CurrentSpeed = (CurrentPrice - PreviousPrice) / EvaluationTime * Math.Pow(10, Symbol.Digits);
            Acceleration = CurrentSpeed / PreviousSpeed;

            // Checking if the acceleration threshold was trespassed.
            if (Acceleration > Phase2AccelerationThreshold && Phase2Flag == false)
            {
                BaseSpeed = PreviousSpeed;
                Phase2Flag = true;
            }
            Print("Acceleration: {0}", Acceleration);
            Print("Phase 2 flag: {0}", Phase2Flag);
            // Phase 2 logic.
            if (Phase2Flag == true)
            {
                // Acceleration compared with the base price.
                var Phase2Acceleration = CurrentSpeed / BaseSpeed;

                // Increasing the periods.
                if (OpenPosition == null)
                    ++EntryAccelerationPeriodsCounter;
                Print("Entry counter: {0}", EntryAccelerationPeriodsCounter);
                // Entry logic.
                if (EntryAccelerationPeriodsCounter >= EntryAccelerationPeriods)
                {
                    if (Phase2Acceleration >= EntryAccelerationThreshold)
                    {
                        // Getting the direction of the market.
                        var _TradeType = CurrentSpeed > 0 ? TradeType.Buy : TradeType.Sell;

                        var Result = ExecuteMarketOrder(_TradeType, Symbol, Symbol.NormalizeVolume(Symbol.QuantityToVolume(Volume)));
                        if (Result.IsSuccessful)
                        {
                            OpenPosition = Result.Position;
                        }
                    }

                    // If the acceleration wasn't sustained, reset the variables.
                    if (OpenPosition == null)
                        Phase2Flag = false;
                    EntryAccelerationPeriodsCounter = -1;
                }

                // Exit logic.
                if (OpenPosition != null && Phase2Acceleration <= ExitAccelerationThreshold)
                {
                    ++ExitAccelerationPeriodsCounter;

                    if (ExitAccelerationPeriodsCounter >= ExitAccelerationPeriods)
                    {
                        CloseOpenPosition();
                    }
                }
                else
                {
                    ExitAccelerationPeriodsCounter = -1;
                }

                // Sanity check.
                Print("Exit Counter: {0}", ExitAccelerationPeriodsCounter);
                //Print("Current Price: {0}", CurrentPrice);
                //Print("Acceleration: {0}", Acceleration);

            }

            PreviousPrice = CurrentPrice;
            PreviousSpeed = CurrentSpeed;

        }

        protected override void OnStop()
        {

        }

        private void CloseOpenPosition()
        {
            var Result = ClosePosition(OpenPosition);
            OpenPosition = null;
            Phase2Flag = false;
            ExitAccelerationPeriodsCounter = -1;
            EntryAccelerationPeriodsCounter = -1;
            BaseSpeed = 0;
        }

    }
}
