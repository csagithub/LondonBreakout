using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;
using FxScreenshot;

//Re-calc for trailing stop
//The high and low for the session


namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
    public class LondonBreakout : Robot
    {
        private const string Buy = "Buy";
        private const string Sell = "Sell";
        private const string High = "High";
        private const string Low = "Low";

        [Parameter("Take Profit Pips", DefaultValue = 20)]
        public double TakeProfit { get; set; }

        [Parameter("Trailing Stop Trigger (pips)", DefaultValue = 20)]
        public int TrailingStopTrigger { get; set; }

        [Parameter("Trailing Stop Step (pips)", DefaultValue = 10)]
        public int TrailingStopStep { get; set; }

        [Parameter("Risk Percent", DefaultValue = 2.0)]
        public double RiskPercent { get; set; }

        private double high { get; set; }

        private double volume { get; set; }

        private double low { get; set; }
        private DateTime open { get; set; }

        private double londonDiff { get; set; }

        private double distance { get; set; }
        private TimeSpan utcOffset { get; set; }

        protected override void OnStart()
        {
            var londonTZ = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
            var utc = TimeZoneInfo.FindSystemTimeZoneById("UTC");

            var now = DateTimeOffset.UtcNow;
            TimeSpan londonOffset = londonTZ.GetUtcOffset(now);
            utcOffset = utc.GetUtcOffset(now);
            TimeSpan diff = londonOffset - utcOffset;
            londonDiff = diff.TotalHours;

            //Print("Server Time: " + Server.Time);
            //Print("Server Time(UTC): " + Server.TimeInUtc);
            //Print("London Time: " + TimeZoneInfo.ConvertTime(Server.Time, Server.Time, londonTZ));

            Positions.Opened += Positions_Opened;
            Positions.Closed += Positions_Closed;
        }

        private void Positions_Opened(PositionOpenedEventArgs obj)
        {
            var pos = obj.Position;

            if (pos.TradeType == TradeType.Sell && pos.Label == (Sell + SymbolName))
            {
                var buyOrder = PendingOrders.Where(x => x.Label == (Buy + SymbolName)).FirstOrDefault();

                if (buyOrder != null)
                {
                    var rc = CancelPendingOrder(buyOrder);

                    if (!rc.IsSuccessful)
                    {
                        Print("Cancel Penidng Order Failed: " + rc.Error.Value);
                    }
                }

                Log.WriteRpt(Server.Time, pos.Label, TradeType.Sell.ToString(), "Open", pos.EntryPrice.ToString(), pos.StopLoss.HasValue ? ((double)pos.StopLoss).ToString() : "", pos.TakeProfit.HasValue ? ((double)pos.TakeProfit).ToString() : "", pos.VolumeInUnits.ToString(), Account.Balance.ToString(), pos.Pips.ToString(),
                high.ToString(), low.ToString(), "");
            }
            else if (pos.TradeType == TradeType.Buy && pos.Label == (Buy + SymbolName))
            {
                var sellOrder = PendingOrders.Where(x => x.Label == (Sell + SymbolName)).FirstOrDefault();

                if (sellOrder != null)
                {
                    var rc = CancelPendingOrder(sellOrder);

                    if (!rc.IsSuccessful)
                    {
                        Print("Cancel Penidng Order Failed: " + rc.Error.Value);
                    }
                }

                Log.WriteRpt(Server.Time, pos.Label, TradeType.Buy.ToString(), "Open", pos.EntryPrice.ToString(), pos.StopLoss.HasValue ? ((double)pos.StopLoss).ToString() : "", pos.TakeProfit.HasValue ? ((double)pos.TakeProfit).ToString() : "", pos.VolumeInUnits.ToString(), Account.Balance.ToString(), pos.Pips.ToString(),
                high.ToString(), low.ToString(), "");
            }
        }

        protected override void OnTick()
        {
            Position posSell = Positions.Find(Sell + SymbolName);

            if (posSell != null)
            {
                //Print(string.Format("SELL - Pips: {0}, TrailStop: {1}", posSell.Pips, posSell.HasTrailingStop));

                double distance = posSell.EntryPrice - Symbol.Ask;

                if (distance >= (TrailingStopTrigger * Symbol.PipSize))
                {
                    double newSLprice = Symbol.Ask + (Symbol.PipSize * TrailingStopStep);

                    if (posSell.StopLoss == null || newSLprice < posSell.StopLoss)
                    {
                        var rc = ModifyPosition(posSell, newSLprice, null);
                        var pos = rc.Position;
                        Log.WriteRpt(Server.Time, pos.Label, TradeType.Sell.ToString(), "Trailing", pos.EntryPrice.ToString(), pos.StopLoss.HasValue ? ((double)pos.StopLoss).ToString() : "", pos.TakeProfit.HasValue ? ((double)pos.TakeProfit).ToString() : "", pos.VolumeInUnits.ToString(), Account.Balance.ToString(), pos.Pips.ToString(),
                        high.ToString(), low.ToString(), "");
                    }
                    else
                    {
                        //Print(string.Format("SELL newSLprice ({0}) < stop loss ({1}) = {2}", newSLprice, posSell.StopLoss, newSLprice < posSell.StopLoss));
                    }
                }
                else
                {
                    //Log.WriteRpt(Server.Time, TradeType.Sell.ToString(), "Sell Not Trailing", posSell.EntryPrice.ToString(), posSell.StopLoss.HasValue ? ((double)posSell.StopLoss).ToString() : "", posSell.TakeProfit.HasValue ? ((double)posSell.TakeProfit).ToString() : "", posSell.VolumeInUnits.ToString(), Account.Balance.ToString(), posSell.Pips.ToString(), high.ToString(),
                    //low.ToString(), string.Format("distance: {0}, entry: {1}, ask: {2}", distance, posSell.EntryPrice, Symbol.Ask));
                }

                return;
            }

            Position posBuy = Positions.Find(Buy + SymbolName);

            if (posBuy != null)
            {
                double distance = Symbol.Bid - posBuy.EntryPrice;

                if (distance >= (TrailingStopTrigger * Symbol.PipSize))
                {
                    var newSLprice = Symbol.Bid - (Symbol.PipSize * TrailingStopStep);

                    if (posBuy.StopLoss == null || newSLprice > posBuy.StopLoss)
                    {
                        var rc = ModifyPosition(posBuy, newSLprice, null);
                        var pos = rc.Position;
                        Log.WriteRpt(Server.Time, pos.Label, TradeType.Buy.ToString(), "Trailing", pos.EntryPrice.ToString(), pos.StopLoss.HasValue ? ((double)pos.StopLoss).ToString() : "", pos.TakeProfit.HasValue ? ((double)pos.TakeProfit).ToString() : "", pos.VolumeInUnits.ToString(), Account.Balance.ToString(), pos.Pips.ToString(),
                        high.ToString(), low.ToString(), "");
                    }
                    else
                    {
                        //Print(string.Format("BUY newSLprice ({0}) > stop loss ({1}) = {2}", newSLprice, posBuy.StopLoss, newSLprice > posBuy.StopLoss));
                    }
                }
                else
                {
                    //Log.WriteRpt(Server.Time, TradeType.Buy.ToString(), "Buy Not Trailing", posBuy.EntryPrice.ToString(), posBuy.StopLoss.HasValue ? ((double)posBuy.StopLoss).ToString() : "", posBuy.TakeProfit.HasValue ? ((double)posBuy.TakeProfit).ToString() : "", posBuy.VolumeInUnits.ToString(), Account.Balance.ToString(), posBuy.Pips.ToString(), high.ToString(),
                    //low.ToString(), string.Format("distance: {0}, entry: {1}, ask: {2}", distance, posBuy.EntryPrice, Symbol.Bid));
                }
            }
        }

        protected override void OnBar()
        {
            base.OnBar();

            var open = MarketSeries.OpenTime.Last(1);

            //was the last candle the prior candle to the london open?
            if (open.AddHours(londonDiff).Hour == 8)
            {
                Log.WriteHdr();

                var highs = new List<double> 
                {
                    MarketSeries.High.Last(2),
                    MarketSeries.High.Last(3),
                    MarketSeries.High.Last(4)
                };
                var lows = new List<double> 
                {
                    MarketSeries.Low.Last(2),
                    MarketSeries.Low.Last(3),
                    MarketSeries.Low.Last(4)
                };

                high = highs.Max();
                low = lows.Min();

                double riskPerTrade = (Account.Balance * RiskPercent) / 100;
                volume = Symbol.NormalizeVolumeInUnits(Account.Balance / Symbol.Bid * riskPerTrade / Symbol.TickValue * Symbol.TickSize);
                //long maxVolume = Symbol.NormalizeVolume((lots > 0 ? Symbol.QuantityToVolume(lots) : Account.Balance * Account.Leverage * openRatio / 100), RoundingMode.Down);

                var icon = Chart.DrawIcon("lonB" + SymbolName, ChartIconType.DownArrow, MarketSeries.OpenTime.Last(1), MarketSeries.High.Last(1) + 0.002, Color.Yellow);
                icon.IsInteractive = true;

                //var lonLine = Chart.DrawVerticalLine("LonB" + SymbolName, open, Color.Yellow);
                //lonLine.IsInteractive = true;

                distance = (high - low) / Symbol.PipSize;

                //Only take trades where the risk is less than 40 pips
                if (distance >= 10 && distance <= 40)
                {
                    var EP = high + 0.0005;
                    //find stop loss for buy ... add 10 pips
                    var SL = Math.Abs(EP - low) / Symbol.PipSize;
                    var rc = PlaceStopOrder(TradeType.Buy, Symbol.Name, volume, EP, Buy + SymbolName, SL, null, null, null, false);
                    var pos = rc.PendingOrder;
                    Log.WriteRpt(Server.Time, pos.Label, TradeType.Buy.ToString(), "Pending", pos.TargetPrice.ToString(), pos.StopLoss.HasValue ? ((double)pos.StopLoss).ToString() : "", pos.TakeProfit.HasValue ? ((double)pos.TakeProfit).ToString() : "", pos.VolumeInUnits.ToString(), Account.Balance.ToString(), "0",
                    high.ToString(), low.ToString(), "");

                    var highLine = Chart.DrawHorizontalLine(High + SymbolName, EP, Color.Green, 1);
                    highLine.IsInteractive = true;

                    EP = low - 0.0005;
                    SL = Math.Abs(high - EP) / Symbol.PipSize;
                    rc = PlaceStopOrder(TradeType.Sell, Symbol.Name, volume, EP, Sell + SymbolName, SL, null, null, null, false);
                    pos = rc.PendingOrder;
                    Log.WriteRpt(Server.Time, pos.Label, TradeType.Sell.ToString(), "Pending", pos.TargetPrice.ToString(), pos.StopLoss.HasValue ? ((double)pos.StopLoss).ToString() : "", pos.TakeProfit.HasValue ? ((double)pos.TakeProfit).ToString() : "", pos.VolumeInUnits.ToString(), Account.Balance.ToString(), "0",
                    high.ToString(), low.ToString(), "");

                    var lowLine = Chart.DrawHorizontalLine(Low + SymbolName, EP, Color.Red, 1);
                    lowLine.IsInteractive = true;
                }
            }
            //Bar after US Open
            else if (open.AddHours(londonDiff).Hour == 14)
            {
                var icon = Chart.DrawIcon("usaB" + SymbolName, ChartIconType.DownArrow, open.AddHours(londonDiff), MarketSeries.High.Last(1) + 0.002, Color.DeepPink);
                icon.IsInteractive = true;
            }
            else if (open.AddHours(londonDiff).Hour == 15)
            {
                foreach (var pos in Positions)
                {
                    ClosePosition(pos);
                }
            }
            else if (open.AddHours(londonDiff).Hour == 16)
            {
                var tm = open.AddHours(londonDiff);
                Capture.Desktop(string.Format("c:\\FxImages\\{0}_{1}_{2}_{3}.jpg", Symbol.Name, tm.Day, tm.DayOfWeek.ToString(), tm.Month));
                //Log.CloseRpt(string.Format("c:\\FxImages\\{0}_{1}_{2}_{3}.csv", Symbol.Name, tm.Day, tm.DayOfWeek.ToString(), tm.Month));
                Log.CloseRpt(string.Format("c:\\FxImages\\{0}_SingleRpt.csv", Symbol.Name));

                Chart.RemoveAllObjects();
            }
        }

        private void Positions_Closed(PositionClosedEventArgs obj)
        {
            var pos = obj.Position;
            string reason = string.Empty;

            switch (obj.Reason)
            {
                case PositionCloseReason.Closed:
                    reason = "Position was closed by trader";
                    break;

                case PositionCloseReason.StopLoss:
                    reason = "Position was closed by Stop Loss";
                    break;

                case PositionCloseReason.StopOut:
                    reason = "Position was closed because Stop Out level reached";
                    break;

                case PositionCloseReason.TakeProfit:
                    reason = "Position was closed by Take Profit";
                    break;
            }

            if (pos.TradeType == TradeType.Sell && pos.Label == (Sell + SymbolName))
            {
                Log.WriteRpt(Server.Time, pos.Label, TradeType.Sell.ToString(), "Close", pos.EntryPrice.ToString(), pos.StopLoss.HasValue ? ((double)pos.StopLoss).ToString() : "", pos.TakeProfit.HasValue ? ((double)pos.TakeProfit).ToString() : "", pos.VolumeInUnits.ToString(), Account.Balance.ToString(), pos.Pips.ToString(),
                high.ToString(), low.ToString(), reason);
            }
            else if (pos.TradeType == TradeType.Buy && pos.Label == (Buy + SymbolName))
            {
                Log.WriteRpt(Server.Time, pos.Label, TradeType.Buy.ToString(), "Close", pos.EntryPrice.ToString(), pos.StopLoss.HasValue ? ((double)pos.StopLoss).ToString() : "", pos.TakeProfit.HasValue ? ((double)pos.TakeProfit).ToString() : "", pos.VolumeInUnits.ToString(), Account.Balance.ToString(), pos.Pips.ToString(),
                high.ToString(), low.ToString(), reason);
            }
        }

        protected override void OnStop()
        {
            Print("Stopping");

            foreach (var pos in Positions)
            {
                ClosePosition(pos);
            }
        }
    }
}
