using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FreightService
{
    class Order
    {
        public string orderNumber;
        public string destination;
        public Flight flight;
    }
    class Flight
    {

        public int flightnumber;
        public string departure;
        public string arrival;
        public int day;

        public int currentCapacity;
    }
    class FlightSchedule
    {
        [JsonIgnore]
        const int MaxDailyFlights = 3;
        [JsonIgnore]
        const int FlightCapacity = 20;

        public int totalFlights;
        public Dictionary<int, Flight[]> schedule;
        public FlightSchedule()
        {
            totalFlights = 0;
            schedule = new Dictionary<int, Flight[]>();
        }
        public Dictionary<int, Flight[]> getSchedule()
        {
            return schedule;
        }
        public void addSchedule(Flight newFlight)
        {
            int day = (newFlight.flightnumber + MaxDailyFlights - 1) / MaxDailyFlights;
            newFlight.day = day;
            if(schedule.ContainsKey(day))
            {
                Flight[] current = schedule[day];
                int index = (newFlight.flightnumber % MaxDailyFlights) - 1;
                if (index < 0)
                    index = 2;
                current[index] = newFlight;
                schedule[day] = current;
            }
            else
            {
                Flight[] newFlights = new Flight[MaxDailyFlights];
                newFlights[0] = newFlight;
                schedule.Add(day, newFlights);
            }
            totalFlights++;
        }
        public Flight getNextFlight(string destination)
        {
            Flight nextFlight = schedule.Values.SelectMany(v => v).FirstOrDefault(f => f!=null && f.arrival.Equals(destination) && f.currentCapacity < FlightCapacity);
            if (nextFlight == null)
                return null;
            nextFlight.currentCapacity++;
            return nextFlight;
        }
    }
    class Program
    {
        static FlightSchedule schedule;
        static bool addSchedule = false;

        static bool getDeparture = false;
        static string departure;

        static bool getArrival = false;
        static string arrival;

        static bool loadOrder = false;
        static bool validCommand = false;

        static bool processArg(String arg)
        {
            switch (arg)
            {
                case "displayschedule":
                    validCommand = true;
                    printSchedules();
                    break;
                case "loadschedule":
                    validCommand = true;
                    loadSchedule();
                    addSchedule = true;
                    break;
                case "loadorders":
                    validCommand = true;
                    loadOrder = true;
                    break;
                case "a":
                    getArrival = true;
                    break;
                case "d":
                    getDeparture = true;
                    break;
                default:
                    validCommand = false;
                    break;
            }
            return validCommand;
        }
        static void loadSchedule()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "schedule.json");
            if (schedule==null && File.Exists(path))
            {
                using (StreamReader file = File.OpenText(path))
                {
                    string json = file.ReadToEnd();
                    schedule = JsonConvert.DeserializeObject<FlightSchedule>(json);
                }
            }
        }

        static List<Order> loadOrders(string orderFileName)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), orderFileName);
            List<Order> orders = new List<Order>();
            if (File.Exists(path))
            {
                using (StreamReader file = File.OpenText(path))
                {
                    string json = file.ReadToEnd();
                    JObject o = JObject.Parse(json);

                    foreach (JProperty property in o.Properties())
                    {
                        orders.Add(new Order { orderNumber = property.Name, destination = (string)property.Value["destination"] });
                    }
                }
            }
            return orders;
        }

        static void saveFlight(Flight f)
        {
            schedule.addSchedule(f);
            saveSchedule();
        }

        static void saveSchedule()
        {
            if (schedule != null)
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), "schedule.json");
                using (StreamWriter writer = new StreamWriter(path, false))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Serialize(writer, schedule);
                }
            }
        }

        static void printSchedules()
        {
            if (schedule == null)
                loadSchedule();
            foreach (KeyValuePair<int, Flight[]> kv in schedule.getSchedule())
            {
                int day = kv.Key;
                for (int i = 0; i < 3; i++)
                {
                    Flight f = kv.Value[i];
                    if (f != null)
                    {
                        Console.WriteLine("Flight: " + f.flightnumber + ", departure: " + f.departure + ", arrival: " + f.arrival + ", day: " + day);
                    }
                }
            }
        }

        static void printLoadedOrders(List<Order> orders)
        {
            if (schedule == null)
                loadSchedule();
            var ordersByDest = orders.GroupBy(o => o.destination);
            foreach(var group in ordersByDest)
            {
                foreach(var order in group)
                {
                    order.flight = schedule.getNextFlight(order.destination);
                }
            }
            foreach(var o in orders)
            {
                if (o.flight == null)
                    Console.WriteLine("order: " + o.orderNumber + ", flightNumber: not scheduled");
                else
                    Console.WriteLine("order: " + o.orderNumber + ", flightNumber: " + o.flight.flightnumber + ", departure: " + o.flight.departure + ", arrival: " + o.flight.arrival + ", day: " + o.flight.day);
            }
        }

        static int Main(string[] args)
        {
            foreach (string arg in args)
            {
                if (arg.StartsWith("/") || arg.StartsWith("-"))
                {
                    if (!processArg(arg.Substring(1).ToLower()))
                    {
                        Console.WriteLine("Invalid command");
                        return -1;
                    }
                    continue;
                }
                else
                {
                    if (getArrival && arg != null)
                    {
                        arrival = arg;
                        getArrival = false;
                    }
                    else if (getDeparture && arg != null)
                    {
                        departure = arg;
                        getDeparture = false;
                    }
                    else if (loadOrder)
                    {
                        loadOrder = false;
                        var orders = loadOrders(arg);
                        if (orders.Count > 0)
                        {
                            printLoadedOrders(orders);
                        }
                        else
                        {
                            Console.WriteLine("Cannot load file");
                            return -1;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid command");
                        return -1;
                    }
                }
            }

            if (addSchedule)
            {
                if (schedule == null)
                    schedule = new FlightSchedule();
                int flightNumber = schedule.totalFlights + 1;
                Flight f = new Flight { flightnumber = flightNumber, arrival = arrival, departure = departure, currentCapacity = 0 };
                saveFlight(f);
            }
            if(loadOrder)
                Console.WriteLine("File not specified");

            return 0;
        }
    }
}
