using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Tooling.Connector;
using Microsoft.Xrm.Sdk.Query;
using RentRefactor.Enums;
using RentRefactor.Services;
using CrmEarlyBound;


namespace RentRefactor
{
    class Program
    {
        static void Main(string[] args)
        {

            string connectionString = @"";

            CrmServiceClient service = new CrmServiceClient(connectionString);

            using (CrmServiceContext context = new CrmServiceContext(service))
            {
                var carRentingService = new RentService(service, context);
                carRentingService.GenerateRents(1);
            }

            Console.ReadLine();
        }
    }
}
