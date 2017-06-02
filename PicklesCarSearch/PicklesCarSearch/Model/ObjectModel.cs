using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PicklesCarSearch.Model
{

    public class Rootobject
    {
        public string Id { get; set; }
        public string Project { get; set; }
        public string Iteration { get; set; }
        public DateTime Created { get; set; }
        public Prediction[] Predictions { get; set; }
    }

    public class Prediction
    {
        public string TagId { get; set; }
        public string Tag { get; set; }
        public float Probability { get; set; }
    }


    public class CarInfo
    {
        public string Make { get; set; }
        public string Year { get; set; }
    }

    public class Car
    {
        public string bloblurl { get; set; }
        public string colour { get; set; }
        public string branchname { get; set; }
        public string branchaddress { get; set; }
        public bool isfixedpriced { get; set; }
        public string make { get; set; }
        public string model { get; set; }
        public int year { get; set; }
        public object auctiondate { get; set; }
        public float price { get; set; }
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTime Timestamp { get; set; }
        public string ETag { get; set; }
        public string pageurl { get; set; }

    }
}