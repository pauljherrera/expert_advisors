using System;
using System.Linq;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo
{
    [Robot("Tick Speed", TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class TickSpeed : Robot
    {
        [Parameter("Lots", DefaultValue = 0.1, MinValue = 0.01, Step = 0.01)]
        public double Lots { get; set; }

        [Parameter("Use Number of Ticks", DefaultValue = true)]
        public bool ShowVolume { get; set; }

        [Parameter("Use Price Variation", DefaultValue = false)]
        public bool ShowVariation { get; set; }

        [Parameter("Period (In seconds)", DefaultValue = 5, MinValue = 1)]
        public int Period { get; set; }

        [Parameter("Entry level", DefaultValue = 10, MinValue = 1)]
        public int EntryLevel { get; set; }

        [Parameter("Exit level", DefaultValue = 5, MinValue = 1)]
        public int ExitLevel { get; set; }

        [Parameter("Trend Strength (in %)", DefaultValue = 70, MinValue = 1, MaxValue = 100)]
        public int TrendStrength { get; set; }


        private Queue<DateTime> TimeQueue = new Queue<DateTime>();
        private Queue<double> PriceQueue = new Queue<double>();
        private Queue<double> DeltaPriceQueue = new Queue<double>();
        private Queue<double> PositivePriceQueue = new Queue<double>();
        private Queue<double> NegativePriceQueue = new Queue<double>();
        private Stack<double> PriceStack = new Stack<double>();
        private Colors Color = Colors.LightBlue;
        private Position OpenPosition = null;

        protected override void OnStart()
        {
            var price = MarketSeries.Close.LastValue;
            TimeQueue.Enqueue(Server.Time);
            PriceQueue.Enqueue(price);
            PriceStack.Push(price);
            DrawChart();
        }

        protected override void OnTick()
        {
            // Storing data for this tick.
            var price = MarketSeries.Close.LastValue;
            TimeQueue.Enqueue(Server.Time);
            PriceQueue.Enqueue(price);

            var deltaPrice = price - PriceStack.Peek();
            DeltaPriceQueue.Enqueue(Math.Abs(deltaPrice));
            if (deltaPrice >= 0)
            {
                PositivePriceQueue.Enqueue(Math.Abs(deltaPrice));
                NegativePriceQueue.Enqueue(0);
            }
            else
            {
                NegativePriceQueue.Enqueue(Math.Abs(deltaPrice));
                PositivePriceQueue.Enqueue(0);
            }
            PriceStack.Push(price);

            // Updating chart and getting the last values.
            var LastValue = DrawChart();
            var LastPositivePeriod = GetTicksVariation(PositivePriceQueue, Server.Time.AddSeconds(-Period), Server.Time.AddSeconds(1));
            var LastNegativePeriod = GetTicksVariation(NegativePriceQueue, Server.Time.AddSeconds(-Period), Server.Time.AddSeconds(1));
            Print("+{0}, -{1}", LastPositivePeriod, LastNegativePeriod);

            // Entry Logic
            double PositivePercentage = 0;
            double NegativePercentage = 0;
            var TotalVariation = LastPositivePeriod + LastNegativePeriod;
            if (TotalVariation > 0)
            {
                PositivePercentage = LastPositivePeriod / TotalVariation * 100;
                NegativePercentage = LastNegativePeriod / TotalVariation * 100;
            }

            if (LastValue >= EntryLevel && PositivePercentage >= TrendStrength && OpenPosition == null)
            {
                ExecuteOrder(TradeType.Buy);
            }
            else if (LastValue >= EntryLevel && NegativePercentage >= TrendStrength && OpenPosition == null)
            {
                ExecuteOrder(TradeType.Sell);
            }
            else if (LastValue <= ExitLevel && OpenPosition != null)
                CloseOpenPosition();

        }

        protected override void OnBar()
        {
            ClearQueues();
        }

        protected override void OnStop()
        {
            // Put your deinitialization logic here
        }

        private void ClearQueues()
        {
            var sel = from t in TimeQueue
                where t >= Server.Time.AddSeconds(-Period * 10)
                select t;
            if (sel.Count() > 0)
            {
                var selQueue = new Queue<DateTime>(sel.ToList());
                var first = selQueue.Dequeue();
                var firstIndex = TimeQueue.ToList().IndexOf(first);

                //Clearing Queues.
                PriceQueue = new Queue<double>(PriceQueue.ToList().Skip(firstIndex));
                DeltaPriceQueue = new Queue<double>(DeltaPriceQueue.ToList().Skip(firstIndex));
                PositivePriceQueue = new Queue<double>(PositivePriceQueue.ToList().Skip(firstIndex));
                NegativePriceQueue = new Queue<double>(NegativePriceQueue.ToList().Skip(firstIndex));
                PriceStack = new Stack<double>(PriceStack.ToList().Skip(firstIndex));
                TimeQueue = new Queue<DateTime>(TimeQueue.ToList().Skip(firstIndex));
            }
        }

        private void CloseOpenPosition()
        {
            var Result = ClosePosition(OpenPosition);
            if (Result.IsSuccessful)
            {
                OpenPosition = null;
            }
        }

        private int DrawChart()
        {
            if (ShowVolume)
                return DrawTicksVolume();
            else if (ShowVariation)
                return DrawTicksVariation();
            else
                return 0;
        }

        private int DrawTicksVariation()
        {
            int PeriodB = Period * 2;
            int PeriodC = Period * 3;
            int PeriodD = Period * 4;
            int PeriodE = Period * 5;
            int PeriodF = Period * 6;
            int PeriodG = Period * 7;
            int PeriodH = Period * 8;
            int PeriodI = Period * 9;
            int PeriodJ = Period * 10;

            int nA = GetTicksVariation(DeltaPriceQueue, Server.Time.AddSeconds(-Period), Server.Time.AddSeconds(1));
            int nB = GetTicksVariation(DeltaPriceQueue, Server.Time.AddSeconds(-PeriodB), Server.Time.AddSeconds(-Period));
            int nC = GetTicksVariation(DeltaPriceQueue, Server.Time.AddSeconds(-PeriodC), Server.Time.AddSeconds(-PeriodB));
            int nD = GetTicksVariation(DeltaPriceQueue, Server.Time.AddSeconds(-PeriodD), Server.Time.AddSeconds(-PeriodC));
            int nE = GetTicksVariation(DeltaPriceQueue, Server.Time.AddSeconds(-PeriodE), Server.Time.AddSeconds(-PeriodD));
            int nF = GetTicksVariation(DeltaPriceQueue, Server.Time.AddSeconds(-PeriodF), Server.Time.AddSeconds(-PeriodE));
            int nG = GetTicksVariation(DeltaPriceQueue, Server.Time.AddSeconds(-PeriodG), Server.Time.AddSeconds(-PeriodF));
            int nH = GetTicksVariation(DeltaPriceQueue, Server.Time.AddSeconds(-PeriodH), Server.Time.AddSeconds(-PeriodG));
            int nI = GetTicksVariation(DeltaPriceQueue, Server.Time.AddSeconds(-PeriodI), Server.Time.AddSeconds(-PeriodH));
            int nJ = GetTicksVariation(DeltaPriceQueue, Server.Time.AddSeconds(-PeriodJ), Server.Time.AddSeconds(-PeriodI));

            ChartObjects.DrawText("Title", "Price variation per period (in pipettes)", StaticPosition.TopLeft, Color);
            ChartObjects.DrawText("A", GetStringToDraw(1, Period, nA), StaticPosition.TopLeft, Color);
            ChartObjects.DrawText("B", GetStringToDraw(2, PeriodB, nB), StaticPosition.TopLeft, Color);
            ChartObjects.DrawText("C", GetStringToDraw(3, PeriodC, nC), StaticPosition.TopLeft, Color);
            ChartObjects.DrawText("D", GetStringToDraw(4, PeriodD, nD), StaticPosition.TopLeft, Color);
            ChartObjects.DrawText("E", GetStringToDraw(5, PeriodE, nE), StaticPosition.TopLeft, Color);
            ChartObjects.DrawText("F", GetStringToDraw(6, PeriodF, nF), StaticPosition.TopLeft, Color);
            ChartObjects.DrawText("G", GetStringToDraw(7, PeriodG, nG), StaticPosition.TopLeft, Color);
            ChartObjects.DrawText("H", GetStringToDraw(8, PeriodH, nH), StaticPosition.TopLeft, Color);
            ChartObjects.DrawText("I", GetStringToDraw(9, PeriodI, nI), StaticPosition.TopLeft, Color);
            ChartObjects.DrawText("J", GetStringToDraw(10, PeriodJ, nJ), StaticPosition.TopLeft, Color);

            return nA;
        }

        private int DrawTicksVolume()
        {
            int PeriodB = Period * 2;
            int PeriodC = Period * 3;
            int PeriodD = Period * 4;
            int PeriodE = Period * 5;
            int PeriodF = Period * 6;
            int PeriodG = Period * 7;
            int PeriodH = Period * 8;
            int PeriodI = Period * 9;
            int PeriodJ = Period * 10;

            int nA = GetNumberOfTicks(Server.Time.AddSeconds(-Period), Server.Time.AddSeconds(1));
            int nB = GetNumberOfTicks(Server.Time.AddSeconds(-PeriodB), Server.Time.AddSeconds(-Period));
            int nC = GetNumberOfTicks(Server.Time.AddSeconds(-PeriodC), Server.Time.AddSeconds(-PeriodB));
            int nD = GetNumberOfTicks(Server.Time.AddSeconds(-PeriodD), Server.Time.AddSeconds(-PeriodC));
            int nE = GetNumberOfTicks(Server.Time.AddSeconds(-PeriodE), Server.Time.AddSeconds(-PeriodD));
            int nF = GetNumberOfTicks(Server.Time.AddSeconds(-PeriodF), Server.Time.AddSeconds(-PeriodE));
            int nG = GetNumberOfTicks(Server.Time.AddSeconds(-PeriodG), Server.Time.AddSeconds(-PeriodF));
            int nH = GetNumberOfTicks(Server.Time.AddSeconds(-PeriodH), Server.Time.AddSeconds(-PeriodG));
            int nI = GetNumberOfTicks(Server.Time.AddSeconds(-PeriodI), Server.Time.AddSeconds(-PeriodH));
            int nJ = GetNumberOfTicks(Server.Time.AddSeconds(-PeriodJ), Server.Time.AddSeconds(-PeriodI));

            ChartObjects.DrawText("Title", "Number of ticks per period", StaticPosition.TopLeft, Color);
            ChartObjects.DrawText("A", GetStringToDraw(1, Period, nA), StaticPosition.TopLeft, Color);
            ChartObjects.DrawText("B", GetStringToDraw(2, PeriodB, nB), StaticPosition.TopLeft, Color);
            ChartObjects.DrawText("C", GetStringToDraw(3, PeriodC, nC), StaticPosition.TopLeft, Color);
            ChartObjects.DrawText("D", GetStringToDraw(4, PeriodD, nD), StaticPosition.TopLeft, Color);
            ChartObjects.DrawText("E", GetStringToDraw(5, PeriodE, nE), StaticPosition.TopLeft, Color);
            ChartObjects.DrawText("F", GetStringToDraw(6, PeriodF, nF), StaticPosition.TopLeft, Color);
            ChartObjects.DrawText("G", GetStringToDraw(7, PeriodG, nG), StaticPosition.TopLeft, Color);
            ChartObjects.DrawText("H", GetStringToDraw(8, PeriodH, nH), StaticPosition.TopLeft, Color);
            ChartObjects.DrawText("I", GetStringToDraw(9, PeriodI, nI), StaticPosition.TopLeft, Color);
            ChartObjects.DrawText("J", GetStringToDraw(10, PeriodJ, nJ), StaticPosition.TopLeft, Color);

            return nA;
        }

        private void ExecuteOrder(TradeType _TradeType)
        {
            var Result = ExecuteMarketOrder(_TradeType, Symbol, Symbol.NormalizeVolume(Symbol.QuantityToVolume(Lots)));
            if (Result.IsSuccessful)
            {
                OpenPosition = Result.Position;
            }
        }

        private int GetTicksVariation(Queue<double> queue, DateTime time1, DateTime time2)
        {
            var sel = from t in TimeQueue
                where t >= time1 && t < time2
                select t;
            if (sel.Count() > 0)
            {
                var selQueue = new Queue<DateTime>(sel.ToList());
                var selStack = new Stack<DateTime>(sel.ToList());
                var first = selQueue.Dequeue();
                var last = selStack.Pop();
                var firstIndex = TimeQueue.ToList().IndexOf(first);
                var lastIndex = TimeQueue.ToList().IndexOf(last);
                var deltas = queue.ToList().Take(lastIndex + 1).Skip(firstIndex);
                return Convert.ToInt16(Math.Round(Enumerable.Sum(deltas) / (Symbol.PipSize / 10), 1));
            }

            return 0;
        }

        private int GetNumberOfTicks(DateTime time1, DateTime time2)
        {
            var sel = from t in TimeQueue
                where t >= time1 && t < time2
                select t;
            return sel.Count();
        }

        private string GetStringToDraw(int NumberOfJumps, int Period, int NumberOfTicks)
        {
            var str = String.Concat(Enumerable.Repeat("\n", NumberOfJumps)) + string.Format("{0,-3} seconds: ", Period) + NumberOfTicks.ToString() + " " + String.Concat(Enumerable.Repeat("-", NumberOfTicks));
            return str;
        }
    }
}
