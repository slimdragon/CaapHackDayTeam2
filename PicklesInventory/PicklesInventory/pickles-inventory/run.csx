#r "Microsoft.WindowsAzure.Storage"
#r "System.Configuration"
#r "System.Drawing"
#r "Newtonsoft.Json"
using System.Net;
using System.Drawing;
using Microsoft.Azure; // Namespace for CloudConfigurationManager
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Queue; // Namespace for Queue storage types
using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info("C# HTTP trigger function processed a request.");

    // Get request body
    dynamic data = await req.Content.ReadAsAsync<object>();

    log.Info(data.ToString());

    string make = data.make;
    string year = data.year;

    var json = GetSearchJsonResults(make, year);

    return req.CreateResponse(HttpStatusCode.OK, json);
}

static string GetSearchJsonResults(string make, string year)
{
    CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudAccountKey);
    CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
    CloudTable table = tableClient.GetTableReference("inventorydata");

    string partitionKey = make.ToLower() + "-" + year.ToString();

    TableRequestOptions reqOptions = new TableRequestOptions();

    var query = new TableQuery<InventoryDataEntity>().Where(
            TableQuery.GenerateFilterCondition(
            "PartitionKey",
            QueryComparisons.GreaterThanOrEqual,
            partitionKey
    ));

    var searchResults = table.ExecuteQuery(query, reqOptions);

    var dtoList = searchResults.Select(r => new InventoryDataDTO
    {
        BlobUrl = "https://caaphackday.blob.core.windows.net/inventoryimages/" + r.BlobUrl,
        BranchName = r.BranchName,
        BranchAddress = r.BranchAddress,
        Make = r.Make,
        Model = r.Model,
        Year = r.Year,
        IsFixedPriced = r.IsFixedPriced,
        AuctionDate = r.AuctionDate,
        Colour = r.Colour,
        Price = r.Price,
        PageUrl = "https://www.pickles.com.au/cars/item/-/details/vehicle/" + r.LoadNumber
    }).ToList();

    return JsonConvert.SerializeObject(dtoList);
}

static string CloudAccountKey = "DefaultEndpointsProtocol=https;AccountName=caaphackday;AccountKey=RcsMI9BV61yxFF+UvL+xDVJx8sl9pxkiYbz2Vs+Ay7LpyMrKX7Nu3Zg2wewPZPdVsUFOJN2Ajvs/ryxPQWvJug==;EndpointSuffix=core.windows.net";

public class InventoryDataEntity : TableEntity
{
    public InventoryDataEntity() { }

    public string BlobUrl { get; set; }

    public string Colour { get; set; }

    public string BranchName { get; set; }

    public string BranchAddress { get; set; }

    public bool IsFixedPriced { get; set; }

    public string Make { get; set; }

    public string Model { get; set; }

    public int Year { get; set; }

    public DateTime? AuctionDate { get; set; }

    public Double Price { get; set; }

    public string LoadNumber { get; set; }
}


public class InventoryDataDTO
{
    public InventoryDataDTO() { }

    [JsonProperty("bloblurl", Required = Required.Always)]
    public string BlobUrl { get; set; }

    [JsonProperty("colour", Required = Required.Always)]
    public string Colour { get; set; }

    [JsonProperty("branchname", Required = Required.Always)]
    public string BranchName { get; set; }

    [JsonProperty("branchaddress", Required = Required.Always)]
    public string BranchAddress { get; set; }

    [JsonProperty("isfixedpriced", Required = Required.Always)]
    public bool IsFixedPriced { get; set; }

    [JsonProperty("make", Required = Required.Always)]
    public string Make { get; set; }

    [JsonProperty("model", Required = Required.Always)]
    public string Model { get; set; }

    [JsonProperty("year", Required = Required.Always)]
    public int Year { get; set; }

    [JsonProperty("auctiondate", Required = Required.AllowNull)]
    public DateTime? AuctionDate { get; set; }

    [JsonProperty("price", Required = Required.Always)]
    public Double Price { get; set; }

    [JsonProperty("pageurl", Required = Required.Always)]
    public string PageUrl { get; set; }
}