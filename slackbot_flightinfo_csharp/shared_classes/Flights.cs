using Newtonsoft.Json.Linq;

namespace slackbot_flightinfo_csharp.shared_classes
{
    public class Flights
    {
        public static JObject FilterFlights(JArray flights, string flightNumber)
        {
            foreach (JToken flight in flights)
            {
                JObject flightObject = (JObject)flight;

                if ((string)flightObject["ident"] == flightNumber)
                {
                    flightObject = (JObject)flight;
                    return flightObject;
                }
            }
            return new JObject();
        }
    }
}