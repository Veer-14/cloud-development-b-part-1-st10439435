using Azure;
using Azure.Data.Tables;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureFunctions_Part_2
{
    internal class OrderEntity : ITableEntity
    {

        public string OrderId { get; set; } = Guid.NewGuid().ToString();
        [Required(ErrorMessage = "Please enter a customer")]
        public string CustomerId { get; set; }
        [Required(ErrorMessage = "Please enter a Product")]
        public string ProductId { get; set; }
        [Required(ErrorMessage = "Please enter a Quantity")]
        public int Quantity { get; set; }
        [Required(ErrorMessage = "Please enter an Order Date")]
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        public string? PartitionKey { get; set; }

        public string? RowKey { get; set; }
        public ETag ETag { get; set; }

        public DateTimeOffset? Timestamp { get; set; }
    }
}
