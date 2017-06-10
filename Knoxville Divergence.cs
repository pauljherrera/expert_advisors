using System;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;
using cAlgo.Indicators;

namespace cAlgo
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class KnoxvilleDivergence : Indicator
    {
        [Parameter("Period", DefaultValue = 30)]
        public int Period { get; set; }

        [Output("Bullish Divergence", Color = Colors.Green, PlotType = PlotType.Points, Thickness = 5)]
        public IndicatorDataSeries BullishDiv { get; set; }

        [Output("Bearish Divergence", Color = Colors.Red, PlotType = PlotType.Points, Thickness = 5)]
        public IndicatorDataSeries BearishDiv { get; set; }

        private bool BullDFlag = false;
        private bool BearDFlag = false;
        private int MinPeriod = 4;
        private MomentumOscillator _momentum;
        private RelativeStrengthIndex _rsi;

        protected override void Initialize()
        {
            _momentum = Indicators.MomentumOscillator(MarketSeries.Close, 20);
            _rsi = Indicators.RelativeStrengthIndex(MarketSeries.Close, 21);
        }

        public override void Calculate(int index)
        {
            BullDFlag = false;
            BearDFlag = false;

            if (index < Period)
                BullishDiv[index] = BearishDiv[index] = double.NaN;
            else
            {
                // Calculate divergence presence.
                for (int i = MinPeriod; i <= Period; i++)
                {
                    if (_momentum.Result.LastValue > _momentum.Result.Last(i))
                    {
                        if (MarketSeries.Close.LastValue < MarketSeries.Close.Last(i))
                        {
                            if (MarketSeries.Low.LastValue <= MarketSeries.Low.Minimum(i))
                            {
                                if (_rsi.Result.Minimum(i) <= 30)
                                {
                                    BullDFlag = true;
                                }
                            }
                        }
                    }
                    else if (_momentum.Result.LastValue < _momentum.Result.Last(i))
                    {
                        if (MarketSeries.Close.LastValue > MarketSeries.Close.Last(i))
                        {
                            if (MarketSeries.High.LastValue >= MarketSeries.High.Maximum(i))
                            {
                                if (_rsi.Result.Maximum(i) >= 70)
                                {
                                    BearDFlag = true;
                                }
                            }
                        }
                    }
                }

                // Draw indicator.
                BullishDiv[index] = BullDFlag == true ? MarketSeries.Low.LastValue - 10 * Symbol.PipSize : double.NaN;
                BearishDiv[index] = BearDFlag == true ? MarketSeries.High.LastValue + 10 * Symbol.PipSize : double.NaN;
            }
        }
    }
}
