using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using NORCE.Drilling.Well.ModelShared;




string localHostName = "https://localhost:5001/";
string devHostName = "https://dev.digiwells.no/";
string wellHostBase = "Well/api/";


// Create clients to access databases from dev/ environment
Client wellClient = ClientSetup(devHostName, wellHostBase);
// Create clients to access databases from local/ environment
Client wellLocalClient = ClientSetup(localHostName, wellHostBase);
// Get all wells and cartographic projection sets from the APIs
List<Well> wells = (List<Well>) (await wellClient.GetAllWellAsync()).ToList();

// Update local database with data from dev/ database
foreach (var well in wells)
{
    // Update each well in local database with the one from dev database
    Console.WriteLine($"Updating well with ID {well.MetaInfo!.ID} in local database...");
    await wellLocalClient.PostWellAsync(well);
}
// Test if the new method works...
Guid slotID = wells[0].SlotID!.Value;
List<Well> wellsBySlotId = (List<Well>) (await wellLocalClient.GetAllWellBySlotIdAsync(slotID)).ToList();
Console.WriteLine($"Number of wells with slot ID {slotID} in local database: {wellsBySlotId.Count}");



// Functions
Client ClientSetup(string _hostName, string _hostBase)
{
    HttpClient httpClient;
    Client api;
    var handler = new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
    };
    httpClient = new HttpClient(handler)
    {
        BaseAddress = new Uri(_hostName + _hostBase)
    };
    httpClient.DefaultRequestHeaders.Accept.Clear();
    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    api = new Client(httpClient.BaseAddress.ToString(), httpClient);
    return api;
}
