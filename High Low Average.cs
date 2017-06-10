using System;
using System.Linq;
using System.Collections.Generic;

using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;
using cAlgo.Indicators;

namespace cAlgo
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AutoRescale = false, AccessRights = AccessRights.None)]
    public class HighLowAverage : Indicator
    {
        [Parameter(DefaultValue = 14)]
        public int Periods { get; set; }

        [Output("High", Color = Colors.Turquoise)]
        public IndicatorDataSeries ResultHigh { get; set; }

        [Output("Low", Color = Colors.Turquoise)]
        public IndicatorDataSeries ResultLow { get; set; }

        public override void Calculate(int index)
        {
            double sumHigh = 0.0;
            double sumLow = 0.0;

            for (int i = index - Periods; i < index; i++)
            {
                sumHigh += MarketSeries.High[i];
                sumLow += MarketSeries.Low[i];
            }
            ResultHigh[index] = sumHigh / Periods;
            ResultLow[index] = sumLow / Periods;
        }
    }
}
