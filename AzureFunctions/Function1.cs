using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using AzureFunctions.Models;


namespace AzureFunctions
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;
        private readonly string _storageConnection;
        private readonly TableClient _customerTable;
        private readonly TableClient _productTable;
        private readonly TableClient _orderTable;
        private readonly BlobContainerClient _blobContainerClient;

        public Function1(ILogger<Function1> logger)
        {
            _logger = logger;

            // Connection string from local.settings.json
            _storageConnection = Environment.GetEnvironmentVariable("AzureWebJobsStorage")
                ?? throw new ArgumentNullException("AzureWebJobsStorage not set");

            var tableServiceClient = new TableServiceClient(_storageConnection);
            _customerTable = tableServiceClient.GetTableClient("Customer");
            _productTable = tableServiceClient.GetTableClient("Product");
            _orderTable = tableServiceClient.GetTableClient("Order");

            _blobContainerClient = new BlobContainerClient(_storageConnection, "images");
            _blobContainerClient.CreateIfNotExists();
        }

        // ------------------- QUEUE TRIGGERS -------------------

        [Function(nameof(QueueCustomerSender))]
        public async Task QueueCustomerSender([QueueTrigger("customer-queue", Connection = "AzureWebJobsStorage")] QueueMessage message)
        {
            _logger.LogInformation("QueueCustomerSender triggered: {msg}", message.MessageText);

            await _customerTable.CreateIfNotExistsAsync();
            var customer = JsonSerializer.Deserialize<CustomerEntity>(message.MessageText);
            if (customer == null) return;

            customer.PartitionKey = "Customer";
            customer.RowKey = Guid.NewGuid().ToString();

            await _customerTable.AddEntityAsync(customer);
            _logger.LogInformation($"Customer saved: {customer.RowKey}");
        }

        [Function(nameof(QueueProductSender))]
        public async Task QueueProductSender([QueueTrigger("product-queue", Connection = "AzureWebJobsStorage")] QueueMessage message)
        {
            _logger.LogInformation("QueueProductSender triggered: {msg}", message.MessageText);

            await _productTable.CreateIfNotExistsAsync();
            var product = JsonSerializer.Deserialize<ProductEntity>(message.MessageText);
            if (product == null) return;

            product.PartitionKey = "Product";
            product.RowKey = Guid.NewGuid().ToString();

            await _productTable.AddEntityAsync(product);
            _logger.LogInformation($"Product saved: {product.RowKey}");
        }

        [Function(nameof(QueueOrderSender))]
        public async Task QueueOrderSender([QueueTrigger("order-queue", Connection = "AzureWebJobsStorage")] QueueMessage message)
        {
            _logger.LogInformation("QueueOrderSender triggered: {msg}", message.MessageText);

            await _orderTable.CreateIfNotExistsAsync();
            var order = JsonSerializer.Deserialize<OrderEntity>(message.MessageText);
            if (order == null) return;

            order.PartitionKey = "Order";
            order.RowKey = Guid.NewGuid().ToString();
            order.Timestamp = DateTimeOffset.UtcNow;

            await _orderTable.AddEntityAsync(order);
            _logger.LogInformation($"Order saved: {order.RowKey}");
        }

        // ------------------- HTTP TRIGGERS (GET ALL) -------------------

        [Function("GetCustomers")]
        public async Task<HttpResponseData> GetCustomers([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "customers")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            try
            {
                var customers = _customerTable.Query<CustomerEntity>().ToList();
                await response.WriteAsJsonAsync(customers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching customers");
                response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteStringAsync("Error retrieving customers.");
            }
            return response;
        }

        [Function("GetProducts")]
        public async Task<HttpResponseData> GetProducts([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "products")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            try
            {
                var products = _productTable.Query<ProductEntity>().ToList();
                await response.WriteAsJsonAsync(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching products");
                response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteStringAsync("Error retrieving products.");
            }
            return response;
        }

        [Function("GetOrders")]
        public async Task<HttpResponseData> GetOrders([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            try
            {
                var orders = _orderTable.Query<OrderEntity>().ToList();
                await response.WriteAsJsonAsync(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching orders");
                response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteStringAsync("Error retrieving orders.");
            }
            return response;
        }

        // ------------------- HTTP TRIGGERS (ADD) -------------------

        [Function("AddCustomer")]
        public async Task<HttpResponseData> AddCustomer([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customers")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.Created);
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            var customer = JsonSerializer.Deserialize<CustomerEntity>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (customer == null || string.IsNullOrEmpty(customer.Customer_Name) || string.IsNullOrEmpty(customer.email))
            {
                response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync("Invalid customer data.");
                return response;
            }

            customer.PartitionKey = "Customer";
            customer.RowKey = Guid.NewGuid().ToString();
            await _customerTable.AddEntityAsync(customer);
            await response.WriteStringAsync("Customer added successfully.");
            return response;
        }

        [Function("AddProduct")]
        public async Task<HttpResponseData> AddProduct([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "products")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.Created);
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            var product = JsonSerializer.Deserialize<ProductEntity>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (product == null || string.IsNullOrEmpty(product.ProductName) || product.Price <= 0)
            {
                response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync("Invalid product data.");
                return response;
            }

            product.PartitionKey = "Product";
            product.RowKey = Guid.NewGuid().ToString();
            await _productTable.AddEntityAsync(product);
            await response.WriteStringAsync("Product added successfully.");
            return response;
        }

        [Function("AddOrder")]
        public async Task<HttpResponseData> AddOrder([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.Created);
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            var order = JsonSerializer.Deserialize<OrderEntity>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (order == null || string.IsNullOrEmpty(order.CustomerId) || string.IsNullOrEmpty(order.ProductId) || order.Quantity <= 0)
            {
                response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync("Invalid order data.");
                return response;
            }

            order.PartitionKey = "Order";
            order.RowKey = Guid.NewGuid().ToString();
            order.Timestamp = DateTimeOffset.UtcNow;
            await _orderTable.AddEntityAsync(order);

            // Add order to queue for async processing
            var queueClient = new QueueClient(_storageConnection, "order-queue");
            await queueClient.CreateIfNotExistsAsync();
            await queueClient.SendMessageAsync(JsonSerializer.Serialize(order));

            await response.WriteStringAsync("Order added and queued successfully.");
            return response;
        }

        // ------------------- FILE UPLOAD -------------------

        [Function("StoreFileFunction")]
        public async Task<HttpResponseData> StoreFile([HttpTrigger(AuthorizationLevel.Function, "post", Route = "files")] HttpRequestData req)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var fileEntity = JsonSerializer.Deserialize<FileEntity>(requestBody);

            if (fileEntity == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid file data.");
                return badResponse;
            }

            // Add to Azure Queue
            var queueClient = new QueueClient(_storageConnection, "file-queue");
            await queueClient.CreateIfNotExistsAsync();
            await queueClient.SendMessageAsync(JsonSerializer.Serialize(fileEntity));

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync($"File info for '{fileEntity.Name}' queued successfully.");
            return response;
        }
    }
}
