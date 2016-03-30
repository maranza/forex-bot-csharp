using System;
using System.Net;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;

class API1broker
{
	const string baseUrl = "";
	const string refId = "3727";
	public string token = "";
	
	// JSON API response structs
	// overview
	public struct Positions_open
	{
		public string position_id, order_id, symbol, margin, leverage, direction, entry_price, current_bid, current_ask, profit_loss, profit_loss_percent, value, market_close, stop_loss, take_profit, created;
	}
	public struct Orders_open
	{
		public string order_id, symbol, margin, leverage, direction, order_type, order_type_parameter, stop_loss, take_profit, created;
	}
	public struct OvResponse
	{
		public string username, email, deposits_unconfirmed_btc, balance_btc, orders_worth_btc, positions_worth_btc, net_worth_btc;
		public Orders_open[] orders_open;
		public Positions_open[] positions_open;
	}
	public struct Overview
	{
		public string server_time;
		public bool error, warning;
		public OvResponse response;
	}
	// get bars
	public struct Bar
	{
		public string time, o, h, l, c;
	}
	public struct Bars
	{
		public string server_time;
		public bool error, warning;
		public Bar[] response;
	}
	
	// init structs for later usage
	public Overview overview = new Overview();
	public Bars bars = new Bars();
	
	// init
	public API1broker(string token)
	{
		this.token = token;
	}
	
	public string call_api(string url)
	{
		try
		{
			// create request
			WebRequest CallAPI;
			CallAPI = WebRequest.Create(url);
			// response stream
			Stream objStream;
			objStream = CallAPI.GetResponse().GetResponseStream();
			// read stream
			StreamReader objReader = new StreamReader(objStream);
			return objReader.ReadLine();
		}
		catch
		{
			throw new Exception("Failed to connect to API.");
		}
	}

	public void account_overview()
	{
		string url = "https://1broker.com/api/v1/account/overview.php?token=" + this.token;
		this.overview = JsonConvert.DeserializeObject<Overview>(call_api(url));
	}
	
	public void get_bars(string symbol, int resolution)
	{
		string url = "https://1broker.com/api/v1/market/get_bars.php?token=" + this.token;
		url += "&symbol="+symbol;
		url += "&resolution="+resolution;
		this.bars = JsonConvert.DeserializeObject<Bars>(call_api(url));
	}
	
	public void position_edit(int position_id, bool market_close, double stop_loss, double take_profit)
	{
		string url = "https://1broker.com/api/v1/position/edit.php?token=" + this.token;
		url += "&position_id="+position_id;
		url += "&market_close="+market_close;
		url += "&stop_loss="+stop_loss;
		url += "&take_profit="+take_profit;
		call_api(url);
	}
	
	public void order_create(string symbol, double margin, string direction, double leverage, string order_type, double order_type_parameter, double stop_loss, double take_profit)
	{
		string url = "https://1broker.com/api/v1/order/create.php?token=" + this.token;
		url += "&referral_id=" + API1broker.refId;
		url += "&symbol="+symbol;
		url += "&margin="+margin;
		url += "&direction="+direction;
		url += "&leverage="+leverage;
		url += "&order_type="+order_type;
		url += "&order_type_parameter="+order_type_parameter;
		url += "&stop_loss="+stop_loss;
		url += "&take_profit="+take_profit;
		call_api(url);
	}
}



class Program
{	
	const double stop_loss_percent = 0.75;
	const double take_profit_percent = 1.5;
	const double margin = 0.01;
	
	public struct Position
	{
		public string direction, symbol, position_id;
	}
	
	static double get_rate(API1broker.Bar[] bars)
	{
		return double.Parse(bars[bars.GetUpperBound(0)].c);
	}
	
	static double calculate_sma(API1broker.Bar[] bars, int range, int delay)
	{
		double sma = 0;
		for(int i=delay; i<(range+delay); i++)
		{
			sma += double.Parse(bars[bars.GetUpperBound(0)-i].c);
		}
		sma /= range;
		return sma;
	}
	
	static void Main()
	{
		Console.WriteLine("Your API token:");
		string token = Console.ReadLine();
		Console.WriteLine("Initializing...");
		API1broker conn = new API1broker(token);
		// say hi!
		Console.WriteLine("Hello, "+conn.overview.response.username+"!");
		Console.WriteLine("Your balance: "+conn.overview.response.balance_btc);
		
		List<Position> open_positions = new List<Position>();
		double sma5 = 0;
		double prev_sma5 = 0;
		double sma20 = 0;
		double prev_sma20 = 0;
		while (true)
		{
			// update
			conn.account_overview();
			conn.get_bars("EURUSD", 3600);
			if (conn.overview.error==true || conn.bars.error==true) throw new Exception("Error fetching 1broker info.");
			
			// check for open positions
			int open_positions_count = conn.overview.response.positions_open.GetLength(0);
			
			// ...and orders
			int open_orders_count = conn.overview.response.orders_open.GetLength(0);
			
			// put positions in struct
			open_positions.Clear();
			for(int i=0; i<open_positions_count; i++)
			{
				Position MyPosition = new Position();
				MyPosition.symbol = conn.overview.response.positions_open[i].symbol;
				MyPosition.direction = conn.overview.response.positions_open[i].direction;
				MyPosition.position_id = conn.overview.response.positions_open[i].position_id;
				open_positions.Add(MyPosition);
			}
			
			// calculate rate
			double rate = get_rate(conn.bars.response);
			
			// calculate sma's, update old sma's
			sma5 = calculate_sma(conn.bars.response, 5, 0);
			prev_sma5 = calculate_sma(conn.bars.response, 5, 1);
			sma20 = calculate_sma(conn.bars.response, 20, 0);
			prev_sma20 = calculate_sma(conn.bars.response, 20, 1);
			Console.WriteLine("sma(5): "+sma5);
			Console.WriteLine("previous sma(5): "+prev_sma5);
			Console.WriteLine("sma(20): "+sma20);
			Console.WriteLine("previous sma(20): "+prev_sma20);
			
			// calculate crosses
			int cross = 0;
			// fast over slow
			if ((sma5 > sma20) && (prev_sma5 <= prev_sma20)) cross = 1;
			// fast below slow
			else if ((sma5 < sma20) && (prev_sma5 >= prev_sma20)) cross = -1;
			// no crosses
			else cross = 0;
			
			Console.WriteLine("cross: "+cross);
			
			// do actions according to crosses
			// if cross = 1, close long (if open), open short
			if (cross == 1)
			{
				for (int i=0; i<open_positions_count; i++)
				{
					if (open_positions[i].direction == "long")
					{
						// market close
						conn.position_edit(int.Parse(open_positions[i].position_id), true, 0.0, 0.0);
						Console.WriteLine("Closed position "+open_positions[i].position_id+".");
					}
				}
				double stop_loss = rate+rate*stop_loss_percent/100;
				double take_profit = rate-rate*take_profit_percent/100;
				if (open_positions_count == 0 && open_orders_count == 0)
				{
					conn.order_create("EURUSD", margin, "short", 2.0, "Market", 0.0, stop_loss, take_profit);
					Console.WriteLine("Opened short position.");
				}
			}
			// if cross = -1, close short (if open), open long
			else if (cross == -1)
			{
				for (int i=0; i<open_positions_count; i++)
				{
					if (open_positions[i].direction == "short")
					{
						// market close
						conn.position_edit(int.Parse(open_positions[i].position_id), true, 0.0, 0.0);
						Console.WriteLine("Closed position "+open_positions[i].position_id+".");
					}
				}
				double stop_loss = rate-rate*stop_loss_percent/100;
				double take_profit = rate+rate*take_profit_percent/100;
				if (open_positions_count == 0 && open_orders_count == 0)
				{
					conn.order_create("EURUSD", margin, "long", 2.0, "Market", 0.0, stop_loss, take_profit);
					Console.WriteLine("Opened long position.");
				}
			}
			// if cross = 0, do nothing
			else if (cross == 0) Console.WriteLine("No crosses, doing nothing.");

			// sleep
			System.Threading.Thread.Sleep(2000);
		}
	}
}
