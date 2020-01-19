using System;
using System.Data;
using System.Net.Http;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System.Configuration;


namespace UppdateraKurser
{
    static class GlobalVars
    {
        public static string conString = "";
        public static string quote = "\"";
        public static string TablesToUpdate = "";
        public static int Delay = 0;
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
                                Logger("DEBUG", table + "." + symbol + " " + CurrentOpenPrice);
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
                }

            }
            else
                GetStockPriceForTable(GlobalVars.TablesToUpdate);
        }

    }

}
