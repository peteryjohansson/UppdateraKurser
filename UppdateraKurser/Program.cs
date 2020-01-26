using System;
using System.Data;
using System.Net.Http;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System.Configuration;



/* Den större delan av aktiekurser använder jag finnhub.io för att uppdatera då jag får göra mer frekvent API anrop än vad man är tillåten mot Alpha Vantage.
 * Däremot så har inte finnhub alla svenska aktier, de som de saknar hämtar jag istället med Alpha Vantage
 * Dock så måste jag ändra symbolen från .ST till .STO i tabellen för att detta ska funka.
 * 
 * 
 * 
 * 
 * 
 * 
 */

namespace UppdateraKurser
{
    static class GlobalVars
    {
        public static string conString = "";
        public static string quote = "\"";
        public static string TablesToUpdate = "";
        public static int Delay = 0;
        public static string SpecialAktier = "";
    }

    public class Program
    {

        public static void Logger(string type, string message)
        {
            try
            {
                using (MySqlConnection connection = new MySqlConnection(GlobalVars.conString))
                {
                    connection.Open();

                    string CommandText = "INSERT INTO log set type = @type, message = @message";
                    MySqlCommand command = new MySqlCommand(CommandText, connection);

                    command.Parameters.AddWithValue("@type", type);
                    command.Parameters.AddWithValue("@message", message);
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public class FinnhubData
        {
            public decimal C { get; set; }
            public decimal H { get; set; }
            public decimal L { get; set; }
            public decimal O { get; set; }
            public decimal PC { get; set; }
        }

        public class AlphaVantageData
        {
            public DateTime Timestamp { get; set; }
            public decimal Open { get; set; }

            public decimal High { get; set; }
            public decimal Low { get; set; }

            public decimal Close { get; set; }
            public decimal Volume { get; set; }
        }

        public static void GetStockPriceForTable(string table)
        {

            // key: bo5suuvrh5rbvm1sl1t0   https://finnhub.io/dashboard
            // https://finnhub.io/api/v1/quote?symbol=AAPL&token=bo5suuvrh5rbvm1sl1t0


            string mysqlcmnd = "SELECT * FROM money." + table + ";";
            string apiKey = "bo5suuvrh5rbvm1sl1t0";

            DataTable dt = new DataTable();
            var client = new System.Net.WebClient();

            try
            {
                using (MySqlConnection connection = new MySqlConnection(GlobalVars.conString))
                {
                    connection.Open();

                    using (MySqlCommand myCommand = new MySqlCommand(mysqlcmnd, connection))
                    {
                        using (MySqlDataAdapter mysqlDa = new MySqlDataAdapter(myCommand))
                            mysqlDa.Fill(dt);

                        foreach (DataRow row in dt.Rows)
                        {
                            string symbol = row[9].ToString();

                            //Logger("DEBUG", "Symbol: " + symbol);

                            try
                            {
                                string url = $"https://finnhub.io/api/v1/quote?symbol={symbol}&token={apiKey}";

                                string responseBody = client.DownloadString(url);
                                FinnhubData StockData = JsonConvert.DeserializeObject<FinnhubData>(responseBody);
                                decimal CurrentOpenPrice = StockData.C;

                                System.Threading.Thread.Sleep(GlobalVars.Delay);
                                UpdateStock(table, symbol, CurrentOpenPrice);
                                Logger("INFO", table + "." + symbol + " " + CurrentOpenPrice);
                            }
                            catch (Exception ex)
                            {
                                Logger("ERROR", symbol + " " + ex.Message);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger("ERROR", ex.Message);
            }

        }

        public static void GetStockPriceSpecial(string table)
        {
            //  Welcome to Alpha Vantage! Here is your API key: APA3UI90FWXJA9IM
            //  string ApiURL = "https://www.alphavantage.co/query?function=TIME_SERIES_INTRADAY&symbol=MSFT&interval=1min&apikey=APA3UI90FWXJA9IM";
            // NOTERA: För att använda detta så måste stockholms börsens aktier markeras med STO och inte ST

            GlobalVars.SpecialAktier = ConfigurationManager.AppSettings["SpecialAktier"];
            string mysqlcmnd = "";

            if (GlobalVars.SpecialAktier.Contains("_"))
            {
                mysqlcmnd = "SELECT * FROM money." + table + " WHERE Symbol = ";
                string[] aktier = GlobalVars.SpecialAktier.Split('_');
                string aktietemp = GlobalVars.quote + aktier[0] + GlobalVars.quote;

                for(int i=1;i < aktier.Length;i++)
                {
                    aktietemp = aktietemp + " OR Symbol = " + GlobalVars.quote + aktier[i] + GlobalVars.quote;
                }

                mysqlcmnd = mysqlcmnd + aktietemp + ";";
            }
            else
            { 
                mysqlcmnd = "SELECT * FROM money." + table + " WHERE Symbol = "+ GlobalVars.quote + GlobalVars.SpecialAktier + GlobalVars.quote + ";";
            }


            DataTable dt = new DataTable();
            var client = new System.Net.WebClient();
            var apiKey = "APA3UI90FWXJA9IM";


            try
            {
                using (MySqlConnection connection = new MySqlConnection(GlobalVars.conString))
                {
                    connection.Open();

                    using (MySqlCommand myCommand = new MySqlCommand(mysqlcmnd, connection))
                    {

                        using (MySqlDataAdapter mysqlDa = new MySqlDataAdapter(myCommand))
                        mysqlDa.Fill(dt);

                        foreach (DataRow row in dt.Rows)
                        {
                            string symbol = row[9].ToString();
                            string responseBody = "";

                            try
                            {
                                string url = $"https://www.alphavantage.co/query?function=TIME_SERIES_INTRADAY&symbol={symbol}&interval=1min&apikey={apiKey}&datatype=csv";
                                responseBody = client.DownloadString(url);
                                string[] tmpString = responseBody.Split(new string[] {System.Environment.NewLine},StringSplitOptions.None);
                                tmpString = tmpString[1].Split(',');
                                decimal CurrentOpenPrice = decimal.Parse(tmpString[1]);

                                System.Threading.Thread.Sleep(GlobalVars.Delay);
                                UpdateStock(table, symbol, CurrentOpenPrice);
                                Logger("DEBUG", table + "." + symbol + " " + CurrentOpenPrice);

                                System.Threading.Thread.Sleep(1000);


                            }
                            catch (Exception ex)
                            {
                                Logger("ERROR", table + "." + symbol + " " + responseBody + " " + ex.Message);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger("ERROR", ex.Message);
            }
        }

        public static void UpdateStock(string table, string symbol, decimal Kurs)
        {

            string mysqlcmnd = "UPDATE money." + table + " SET Kurs =  " + Kurs + " WHERE Symbol = " + GlobalVars.quote + symbol + GlobalVars.quote + ";";

            try
            {
                using (MySqlConnection connection = new MySqlConnection(GlobalVars.conString))
                {
                    connection.Open();
                    MySqlCommand command = new MySqlCommand(mysqlcmnd, connection);
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Logger("ERROR", ex.Message);
            }
        }

        public static float ConvertExchangeRates(string Valuta)
        {
            //https://api.exchangeratesapi.io/latest?base=USD

            string url = $"https://api.exchangeratesapi.io/latest?base={Valuta}";
            var client = new System.Net.WebClient();

            try
            {
                string responseBody = client.DownloadString(url);
                int position = responseBody.IndexOf("SEK");
                string substring = responseBody.Substring(position + 5, 20);
                int endposition = substring.IndexOf(",");
                string rate = substring.Substring(0, endposition - 1);
                return float.Parse(rate);

            }
            catch (Exception ex)
            {
                Logger("ERROR", "Problem med att hämta Exchange rates " + ex.Message);
                return 0;
            }

        }


        static void Main(string[] args)
        {

            GlobalVars.conString = ConfigurationManager.AppSettings["MySqlConnectionString"];
            GlobalVars.TablesToUpdate = ConfigurationManager.AppSettings["TablesToUpdate"];
            GlobalVars.Delay = Int32.Parse(ConfigurationManager.AppSettings["Delay"]);

            if (GlobalVars.TablesToUpdate.Contains("_"))
            {
                string[] tables = GlobalVars.TablesToUpdate.Split('_');

                foreach (string table in tables)
                {
                    GetStockPriceForTable(table);
                    GetStockPriceSpecial(table);
                }

            }
            else
            {
                GetStockPriceForTable(GlobalVars.TablesToUpdate);
                GetStockPriceSpecial(GlobalVars.TablesToUpdate);
            }
        }

    }

}
