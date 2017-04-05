using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace slackbot_flightinfo_csharp.shared_classes
{
    public class GenericHelper
    {
        public static string GetEnvironmentVariable(string name)
        {
            return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        }

        public static HttpResponseMessage SendMessageToSlack(string responseUri, object message)
        {
            var serializedPayload = JsonConvert.SerializeObject(message);
            HttpClient client = new HttpClient();
            var response = client.PostAsync(responseUri, new StringContent(serializedPayload, Encoding.UTF8, "application/json")).Result;
            return response;
        }

        public static async Task<string> FlightAwareGet(string uri)
        {
            string flightawareUser = GetEnvironmentVariable("flightaware_user");
            string flightawareApi = GetEnvironmentVariable("flightaware_api");

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($"{flightawareUser}:{flightawareApi}")));

            var responseAirlineFlightSchedules = await client.GetAsync(uri);
            var responseContentAirlineFlightSchedules = responseAirlineFlightSchedules.Content;
            string result = null;
            using (var reader = new StreamReader(await responseContentAirlineFlightSchedules.ReadAsStreamAsync()))
            {
                result = await reader.ReadToEndAsync();
            }
            return result;
        }

        public static DateTime FromUnixTime(long unixTime)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(unixTime);
        }

        public static DateTime ConvertFromUtcToLocal(DateTime utc)
        {
            TimeZoneInfo aestZone = TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");
            DateTime aestTime = TimeZoneInfo.ConvertTimeFromUtc(utc, aestZone);
            return aestTime;
        }
    }
}