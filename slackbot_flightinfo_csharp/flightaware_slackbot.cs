using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using slackbot_flightinfo_csharp.shared_classes;

namespace slackbot_flightinfo_csharp
{
    public class payload
    {
        public string Token { get; set; }
        public string Team_Id { get; set; }
        public string Team_Domain { get; set; }
        public string Channel_Id { get; set; }
        public string Channel_Name { get; set; }
        public string User_Id { get; set; }
        public string User_Name { get; set; }
        public string Command { get; set; }
        public string Text { get; set; }
        public string Response_Url { get; set; }

    }

    public class FlightawareSlackbot
    {

        public static async Task<string> Run(HttpRequestMessage req, TraceWriter log,
            IAsyncCollector<payload> weatherQueue, IAsyncCollector<payload> flightStatusQueue, ICollector<string> outputDocument)
        {
            log.Info($"C# HTTP trigger function processed a request. Command used={req.RequestUri}");

            string jsonContent = await req.Content.ReadAsStringAsync();
            log.Info(jsonContent);
            string res = null;

            payload json = new payload
            {
                Token = (jsonContent.Split('&')[0]).Split('=')[1],
                Team_Id = (jsonContent.Split('&')[1]).Split('=')[1],
                Team_Domain = (jsonContent.Split('&')[2]).Split('=')[1],
                Channel_Id = (jsonContent.Split('&')[3]).Split('=')[1],
                Channel_Name = (jsonContent.Split('&')[4]).Split('=')[1],
                User_Id = (jsonContent.Split('&')[5]).Split('=')[1],
                User_Name = (jsonContent.Split('&')[6]).Split('=')[1],
                Command = (jsonContent.Split('&')[7]).Split('=')[1],
                Text = (jsonContent.Split('&')[8]).Split('=')[1],
                Response_Url = (jsonContent.Split('&')[9]).Split('=')[1]
            };

            string document = JsonConvert.SerializeObject(json);
            outputDocument.Add(document);

            string command = Uri.EscapeDataString(json.Command);

            switch (command)
            {
                case "%252Fmetar":
                    // add to weatherQueue queue
                    await weatherQueue.AddAsync(json);
                    break;
                case "%252Fflightstatus":
                    // add to flightStatus queue
                    await flightStatusQueue.AddAsync(json);
                    break;
            }

            res =
                $"Hey, {json.User_Name}, I'm working on getting your information, hold on tight...";
            
            return res;
        }
    }

    public class GetWeather
    {
        public static async Task Run(payload weatherQueue, TraceWriter log)
        {
            log.Info($"Starting to retrieve weather for {weatherQueue.Text}.");
            string airportCode = null;
            string weather = null;
            string airportInfo = null;

            switch (weatherQueue.Text.Length)
            {
                case 3:
                    log.Info("This is an IATA code, need to get the ICAO code first...");
                    airportCode = (await ConvertIataToIcao(weatherQueue.Text)).ToUpper();
                    break;
                case 4:
                    log.Info("This is an ICAO code, we can continue from here...");
                    airportCode = weatherQueue.Text.ToUpper();
                    break;
                default:
                    log.Error("Not a valid airport code.");
                    break;
            }

            var airportInfoResultUri =
                $"https://flightxml.flightaware.com/json/FlightXML2/AirportInfo?airportCode={airportCode}";

            airportInfo = await GenericHelper.FlightAwareGet(airportInfoResultUri);

            JObject airResultJson = JObject.Parse(airportInfo);
            string airportName = airResultJson["AirportInfoResult"].SelectToken("name").Value<string>();
            log.Info(airportName);
            var uri =
                $"https://flightxml.flightaware.com/json/FlightXML2/MetarEx?airport={airportCode}&howMany=1";
            weather = await GenericHelper.FlightAwareGet(uri);

            JObject resultJson = JObject.Parse(weather);
            // get latest metar from results
            JToken metar = resultJson["MetarExResult"].SelectToken("metar").Value<JToken>()[0];
            string airport = metar["airport"].ToString();
            string windDirection = metar["wind_direction"].ToString();
            string windSpeed = metar["wind_speed"].ToString();
            string windGusts = metar["wind_speed_gust"].ToString();
            string visibility = metar["raw_data"].ToString().Split()[3];
            // find the air pressure in the raw data
            string pressure = null;
            // hPa regex
            var regexHp = new Regex(@"Q\d{4}");
            var resultsHp = regexHp.Matches(metar["raw_data"].ToString());
            if (resultsHp.Count == 0)
            {
                // apparently an american code, so need to get the inch HG value
                var regexHg = new Regex(@"A\d{4}");
                var resultsHg = regexHg.Matches(metar["raw_data"].ToString());
                pressure = $"{resultsHg[0].Value} inHg";
            }
            else
            {
                pressure = $"{resultsHp[0].Value} hPa";
            }
            string clouds = metar["cloud_friendly"].ToString();
            string cloudsAltitude = metar["cloud_altitude"].ToString();
            string cloudType = metar["cloud_type"].ToString();
            string tempAir = metar["temp_air"].ToString();

            log.Info(pressure);

            var slackResponseUri = HttpUtility.UrlDecode(weatherQueue.Response_Url);
            var jsonPayload = new
            {
                text =
                $"{weatherQueue.User_Name} here is your weather request for {airportName} / {airport} \n" +
                $"Wind = {windDirection} / {windSpeed} kts \n" +
                $"Wind Gusts = {windGusts} kts \n" +
                $"Visibility = {visibility} \n" +
                $"QNH = {pressure} \n" +
                $"Clouds = {clouds} \n" +
                $"Clouds altitude = {cloudsAltitude} ft {cloudType} \n" +
                $"Temperature = {tempAir} C \n" +
                $"Raw Report = {metar["raw_data"].ToString()}"
            };

            GenericHelper.SendMessageToSlack(slackResponseUri, jsonPayload);
        }

        public class Metar
        {
            public string airport { get; set; }
            public int time { get; set; }
            public string cloud_friendly { get; set; }
            public int cloud_altitude { get; set; }
            public string cloud_type { get; set; }
            public string conditions { get; set; }
            public int pressure { get; set; }
            public int temp_air { get; set; }
            public int temp_dewpoint { get; set; }
            public int temp_relhum { get; set; }
            public string visibility { get; set; }
            public string wind_friendly { get; set; }
            public int wind_direction { get; set; }
            public int wind_speed { get; set; }
            public int wind_speed_gust { get; set; }
            public string raw_data { get; set; }
        }

        public static async Task<string> ConvertIataToIcao(string iata)
        {
            string data = null;
            var uri =
                $"http://www.airport-data.com/api/ap_info.json?iata={iata}";
            HttpClient client = new HttpClient();
            var response = await client.GetAsync(uri);
            var airResponseContent = response.Content;

            using (var reader = new StreamReader(await airResponseContent.ReadAsStreamAsync()))
            {
                data = await reader.ReadToEndAsync();
            }

            JObject airResultJson = JObject.Parse(data);

            return airResultJson["icao"].Value<string>();
        }

    }

    public class GetFlightStatus
    {
        public static async Task Run(payload flightStatusQueue, TraceWriter log)
        {
            log.Info($"Starting to retrieve flight info for {flightStatusQueue.Text}.");

            string flightNumber = flightStatusQueue.Text.ToUpper();
            string airlineIata = flightNumber.Substring(0, 2);

            //convert the airline's IATA code to ICAO
            var uri = "http://avcodes.co.uk/airlcoderes.asp";
            HttpClient client = new HttpClient();
            var values = new Dictionary<string, string>
            {
                {"status", "Y"},
                {"iataairl", airlineIata}
            };

            var content = new FormUrlEncodedContent(values);

            var response = await client.PostAsync(uri, content);
            var responseContent = response.Content;
            string result = null;
            using (var reader = new StreamReader(await responseContent.ReadAsStreamAsync()))
            {
                result = await reader.ReadToEndAsync();
            }

            var regex = new Regex(@"ICAO Code:<br />&nbsp;\D{3}");
            var matches = regex.Matches(result);

            string icaoCode = $"{(matches[0].Value).Split(';')[1].Substring(0, 3)}";
            log.Info(icaoCode);

            // get the flight number from the input string
            string flightno = flightNumber.Substring(2);
            Int32 today = (Int32) (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            Int32 tomorrow = (Int32) (DateTime.UtcNow.AddDays(1).Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            var uriAirlineFlightSchedules =
                $"https://flightxml.flightaware.com/json/FlightXML2/AirlineFlightSchedules?startDate={today}&endDate={tomorrow}&airline={icaoCode}&flightno={flightno}";

            string airlineFlightSchedules = await GenericHelper.FlightAwareGet(uriAirlineFlightSchedules);

            log.Info(airlineFlightSchedules);
            JObject jsonAirlineFlightSchedules = JObject.Parse(airlineFlightSchedules);
            JArray airlineFlightSchedulesResults = jsonAirlineFlightSchedules["AirlineFlightSchedulesResult"]
                .SelectToken("data")
                .Value<JArray>();
            JArray actualFlights = Flights.FilterFlights(airlineFlightSchedulesResults, $"{icaoCode}{flightno}");

            foreach (JToken flight in actualFlights)
            {

                string flightident =
                    $"{icaoCode}{flightno}@{flight.SelectToken("departuretime").Value<string>()}";
                var uriFlightInfoEx =
                    $"https://flightxml.flightaware.com/json/FlightXML2/FlightInfoEx?ident={flightident}&howMany=2";
                string flightInfoExs = await GenericHelper.FlightAwareGet(uriFlightInfoEx);

                JObject jsonFlightInfoEx = JObject.Parse(flightInfoExs);
                JArray flightInfo = jsonFlightInfoEx["FlightInfoExResult"].SelectToken("flights").Value<JArray>();

                string flightIdent = flight.SelectToken("ident").Value<string>();
                string origin = flight.SelectToken("origin").Value<string>();
                string destination = flight.SelectToken("destination").Value<string>();
                string typeOfAircraft = flight.SelectToken("aircrafttype").Value<string>();

                DateTime filedDepartureTime = GenericHelper.FromUnixTime(flightInfo[0].SelectToken("filed_departuretime").Value<long>());
                DateTime estimatedArrivalTime =
                    GenericHelper.FromUnixTime(flightInfo[0].SelectToken("estimatedarrivaltime").Value<long>());
                string estimatedTimeEnroute = flightInfo[0].SelectToken("filed_ete").Value<string>();
                TimeSpan estimatedTimeEnrouteTimeSpan = TimeSpan.Parse(estimatedTimeEnroute);

                DateTime estimatedDepartureTime = estimatedArrivalTime.Add(-estimatedTimeEnrouteTimeSpan);
                TimeSpan delay = estimatedDepartureTime - filedDepartureTime;

                var slackResponseUri = HttpUtility.UrlDecode(flightStatusQueue.Response_Url);
                //{GenericHelper.ConvertFromUtcToLocal(GenericHelper.FromUnixTime(flightInfo[0].SelectToken("filed_departuretime").Value<long>()))}" 
                // if you haven't set your Azure Web App time zone
                var jsonPayload = new
                {
                    text =
                    $"*{flightStatusQueue.User_Name} here is your flight info for Flight # {icaoCode}{flightno} / {flightIdent}* \n" +
                    $"From = {origin} / {flightInfo[0].SelectToken("originName").Value<string>()}\n" +
                    $"To = {destination} / {flightInfo[0].SelectToken("destinationName").Value<string>()} \n" +
                    $"Type of Aircraft = {typeOfAircraft} \n" +
                    $"Filed Departure time = {GenericHelper.FromUnixTime(flightInfo[0].SelectToken("filed_departuretime").Value<long>())} \n" +
                    $"Estimated Departure time = {estimatedDepartureTime} \n" +
                    $"Estimated Arrival time = {GenericHelper.FromUnixTime(flightInfo[0].SelectToken("estimatedarrivaltime").Value<long>())} \n" +
                    $"Current delay = {delay} \n" +
                    $"Estimated Flight time = {estimatedTimeEnroute} \n"
                };

                GenericHelper.SendMessageToSlack(slackResponseUri, jsonPayload);
                log.Info(jsonPayload.ToString());
            }
        }
    }
}

