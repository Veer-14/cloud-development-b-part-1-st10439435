using Azure;
using Azure.Data.Tables;
using System.ComponentModel.DataAnnotations;

namespace CLDV_POE.Models
{
    public class Product : ITableEntity
    {

        [Key]
        public int ProductId { get; set; } 

        [MaxLength(200)]
        public string? ProductName { get; set; }

        public string? ProductDescription { get; set; }

        public double? Price { get; set; }

        public string? ImageUrl { get; set; }

        public string? PartitionKey { get; set; }

        public string? RowKey { get; set; }
        public ETag ETag { get; set; }

        public DateTimeOffset? Timestamp { get; set; }

    }
}
