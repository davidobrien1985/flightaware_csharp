# Azure Function for Flightaware

This C# code supports being integrated to an [Azure Function](http://functions.azure.com/) as a compiled library.
Trigger it from Slack and use one of the following two Slack slash commands:

## Slash commands

`/metar` or `/flightstatus`

Use like this:
`/metar MEL` to get the weather for the airport IATA Code "MEL" (Melbourne Tullamarine/Australia).
`/metar YMML` to get the weather for the airport ICAO Code "YMML" (Melbourne Tullamarine/Australia).

`/flightstatus QF400` to get the next flight (between NOW and 24hrs) for QF (Qantas) flight 400.

## Dependencies

One needs a [flightaware](http://flightaware.com/) user id and API key added to the Azure Functions environment in order for the code to be able to authenticate against FlightAware.
