using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Tooling.Connector;
using RentRefactor.Enums;
using RentRefactor.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrmEarlyBound;

namespace RentRefactor.Services
{
    public class RentService
    {
        private readonly CrmRepository<crc6f_rent> _rentRepository;
        private readonly CrmRepository<crc6f_cartransferreport> _carTransferReport;

        private Random rnd = new Random();
        private List<string> attributesList;
        private CrmServiceContext _context;
        private CrmServiceClient _service;
        private List<crc6f_car_class> _carClassRecords;
        private List<Contact> _customerRecords;
        private List<crc6f_car_class> _cars = null;
        private static DateTime start = new DateTime(2019, 1, 1);
        private static DateTime end = new DateTime(2020, 12, 31);

        //trying one way to store location
        private Dictionary<Int32, string> locationOptionSet = new Dictionary<Int32, string> {
                    { 1, "Airport" },
                    { 2, "City Center" },
                    { 3, "Office" }
                };

        //status code and their probability
        private List<KeyValuePair<StatusCodeOptionSet, double>> statusCodeProbability = new List<KeyValuePair<StatusCodeOptionSet, double>> {
                    new KeyValuePair<StatusCodeOptionSet, double>(StatusCodeOptionSet.Created, 0.05),
                    new KeyValuePair<StatusCodeOptionSet, double>(StatusCodeOptionSet.Confirmed, 0.05),
                    new KeyValuePair<StatusCodeOptionSet, double>(StatusCodeOptionSet.Renting, 0.05),
                    new KeyValuePair<StatusCodeOptionSet, double>(StatusCodeOptionSet.Returned, 0.75),
                    new KeyValuePair<StatusCodeOptionSet, double>(StatusCodeOptionSet.Canceled, 0.1)
                };

        public RentService(CrmServiceClient service, CrmServiceContext context)
        {
            _context = context;
            _service = service;
            _rentRepository = new CrmRepository<crc6f_rent>(service);
            _carTransferReport = new CrmRepository<crc6f_cartransferreport>(service);
            _carClassRecords = (from carClass in _context.crc6f_car_classSet select carClass).ToList();
            _customerRecords = (from customer in _context.ContactSet where customer["ownerid"] == "94D249CC-60E1-EB11-BACB-000D3A4AF503" select customer).ToList();
        }


        public void GenerateRents(int counts)
        {
            for (int i = 1; i <= counts; i++)
            {
                GenerateRandomRent(i);
            }
        }
        private void GenerateRandomRent(int rentNumber)
        {
            DateTime reservedPickup = RandomDateTime(start, end, rnd);
            DateTime reservedHandover = RandomDateTime(reservedPickup, reservedPickup.AddDays(30), rnd);

            var pickupLocation = GenerateRandomLocation();
            var returnLocation = GenerateRandomLocation();

            var selectedCustomer = _customerRecords[rnd.Next(_customerRecords.Count)];
            EntityReference selectedCustomerref = selectedCustomer.ToEntityReference();

            var selectedCarClass = _carClassRecords[rnd.Next(_carClassRecords.Count)];
            var carsClass = selectedCarClass.Attributes["crc6f_car_classid"];
            EntityReference selectedCarClassref = selectedCarClass.ToEntityReference();

            var carsInClassRecords = (from carInClass in _context.crc6f_carSet where carInClass.crc6f_car_class == carsClass select carInClass).ToList();
            var selectedCar = carsInClassRecords[rnd.Next(carsInClassRecords.Count)];
            EntityReference selectedCarref = selectedCar.ToEntityReference();

            var selectedStatusCode = probalityBasedRandom(statusCodeProbability, rnd).Key;
            var stateCode = crc6f_rentState.Active;
            if (selectedStatusCode == StatusCodeOptionSet.Returned || selectedStatusCode == StatusCodeOptionSet.Canceled)
            {
                stateCode = crc6f_rentState.Inactive;
            }
            else
            {
                stateCode = crc6f_rentState.Active;
            }

            var rent = new crc6f_rent
            {
                crc6f_name = $"Rent {rentNumber}",
                crc6f_Reservedpickup = reservedPickup,
                crc6f_reserved_handover = reservedHandover,
                crc6f_Pickuplocation = pickupLocation,
                crc6f_return_location = returnLocation,
                crc6f_customer = selectedCustomerref,
                crc6f_car_class = selectedCarClassref,
                crc6f_car = selectedCarref,
                StateCode = stateCode,
                StatusCode = new OptionSetValue(((int)selectedStatusCode)),
                crc6f_paid = setPaidProbabilyty(selectedStatusCode)

            };

            if (selectedStatusCode == StatusCodeOptionSet.Renting || selectedStatusCode == StatusCodeOptionSet.Returned)
            {
                Guid pickupReportGuid = createTransferReport(false, rentNumber, selectedCarref, reservedPickup);
                rent.crc6f_pickup_report = new EntityReference("crc6f_cartransferreport", pickupReportGuid);

            }

            //create return report
            if (selectedStatusCode == StatusCodeOptionSet.Returned)
            {
                Guid returnReportGuid = createTransferReport(true, rentNumber, selectedCarref, reservedHandover);
                rent.crc6f_return_report = new EntityReference("crc6f_cartransferreport", returnReportGuid);
                
            }


            //set price field
            decimal moneyValue = ((Money)selectedCarClass.crc6f_price).Value * (reservedHandover.Date - reservedPickup.Date).Days;
            if (pickupLocation.Value != 3)
                moneyValue = moneyValue + 100;
            if (returnLocation.Value != 3)
                moneyValue = moneyValue + 100;
            rent.crc6f_price = new Money((decimal)moneyValue);

            Guid guid = _rentRepository.Create(rent);
            Console.WriteLine(guid.ToString());

        }


        public static DateTime RandomDateTime(DateTime dateTimeFrom, DateTime dateTimeTo, Random rnd)
        {

            TimeSpan timeSpan = dateTimeTo - dateTimeFrom;
            TimeSpan newSpan = new TimeSpan(0, rnd.Next(0, (int)timeSpan.TotalMinutes), 0);
            DateTime newDate = dateTimeFrom + newSpan;

            //Return the value
            return newDate;
        }

        protected OptionSetValue GenerateRandomLocation() => new OptionSetValue(locationOptionSet.ElementAt(rnd.Next(locationOptionSet.Count())).Key);

        private static KeyValuePair<StatusCodeOptionSet, double> probalityBasedRandom(List<KeyValuePair<StatusCodeOptionSet, double>> elements, Random rnd)
        {
            double diceRoll = rnd.NextDouble();
            double cumulative = 0.0;
            for (int j = 0; j < elements.Count; j++)
            {
                cumulative += elements[j].Value;
                if (diceRoll < cumulative)
                {
                    return elements[j];
                }
            }
            return elements[1];
        }

        private Guid createTransferReport(bool type, int rentNumber, EntityReference selectedCarref, DateTime reservedTime)
        {
            var report = new crc6f_cartransferreport
            {
                crc6f_name = "Sample report " + (type? "return ":"pickup ") + rentNumber.ToString(),
                crc6f_car = selectedCarref,
                crc6f_Type = type,
                crc6f_date = reservedTime
            };

            double probability = rnd.NextDouble();
            if (probability <= 0.05)
            {
                report.crc6f_damages = true;
                report.crc6f_damage_description = "damage";
            }

            Guid pickupReportGuid = _carTransferReport.Create(report);
            Console.WriteLine("Sample report " + (type ? "return " : "pickup ") + rentNumber.ToString());
            return pickupReportGuid;
        }

        private bool setPaidProbabilyty(StatusCodeOptionSet selectedStatusCode)
        {
            double paidProbability = rnd.NextDouble();
            if (selectedStatusCode == StatusCodeOptionSet.Confirmed && paidProbability < 0.9)
            {
                return true;
            }

            if (selectedStatusCode == StatusCodeOptionSet.Renting && paidProbability < 0.999)
            {
                return true;
            }

            if (selectedStatusCode == StatusCodeOptionSet.Returned && paidProbability < 0.9998)
            {
                return true;
            }
            return false;
        }
    } 
}
    
