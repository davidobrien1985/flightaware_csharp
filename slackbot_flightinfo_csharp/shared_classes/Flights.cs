using Newtonsoft.Json.Linq;

namespace slackbot_flightinfo_csharp.shared_classes
{
    public class Flights
    {
        public static JArray FilterFlights(JArray flights, string flightNumber)
        {
            JArray results = new JArray();

            foreach (JToken flight in flights)
            {
                
                JObject flightObject = (JObject)flight;

                if ((string)flightObject["ident"] == flightNumber)
                {
                    flightObject = (JObject)flight;
                    results.Add(flightObject);
                }
                
            }
            return results;
        }
    }
}