using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace AzureFunctions_Part_2
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;
        private readonly string _storageconnection;
        private readonly TableClient _customer;
        private readonly TableClient _product;
        private readonly TableClient _order;
        private readonly BlobContainerClient _blobContainerClient;

        public Function1(ILogger<Function1> logger)
        {
            _logger = logger;
            _storageconnection = "DefaultEndpointsProtocol=https;AccountName=st10440685cldv6212;AccountKey=AHwk5TO+VzZ3ce97q9WVZj1Xu+dI0B435jIiH/zGq9l73iBS6klqlRKD4pXaLctwGuSHltgjn69g+AStqWPYhw==;EndpointSuffix=core.windows.net";
            var serviceClient = new TableServiceClient(_storageconnection);
            _customer = serviceClient.GetTableClient("Customer");
            _product = serviceClient.GetTableClient("Product");
            _order = serviceClient.GetTableClient("Order");
            _blobContainerClient = new BlobContainerClient(_storageconnection, "images");
            _blobContainerClient.CreateIfNotExists();
        }

        // ------------------- QUEUE TRIGGERS -------------------

        [Function(nameof(QueueCustomerSender))]
        public async Task QueueCustomerSender([QueueTrigger("customer-queue", Connection = "connection")] QueueMessage message)
        {
            _logger.LogInformation("QueueCustomerSender triggered with message: {messageText}", message.MessageText);
            await _customer.CreateIfNotExistsAsync();

            var customer = JsonSerializer.Deserialize<CustomerEntity>(message.MessageText);
            if (customer == null)
            {
                _logger.LogError("Failed to deserialize CustomerEntity.");
                return;
            }

            customer.RowKey = Guid.NewGuid().ToString();
            customer.PartitionKey = "Customer";

            await _customer.AddEntityAsync(customer);
            _logger.LogInformation($"Customer saved successfully with RowKey: {customer.RowKey}");
        }

        [Function(nameof(QueueProductSender))]
        public async Task QueueProductSender([QueueTrigger("product-queue", Connection = "connection")] QueueMessage message)
        {
            _logger.LogInformation("QueueProductSender triggered with message: {messageText}", message.MessageText);
            await _product.CreateIfNotExistsAsync();

            var product = JsonSerializer.Deserialize<ProductEntity>(message.MessageText);
            if (product == null)
            {
                _logger.LogError("Failed to deserialize ProductEntity.");
                return;
            }

            product.RowKey = Guid.NewGuid().ToString();
            product.PartitionKey = "Product";

            await _product.AddEntityAsync(product);
            _logger.LogInformation($"Product saved successfully with RowKey: {product.RowKey}");
        }

        [Function(nameof(QueueOrderSender))]
        public async Task QueueOrderSender([QueueTrigger("order-queue", Connection = "connection")] QueueMessage message)
        {
            _logger.LogInformation("QueueOrderSender triggered with message: {messageText}", message.MessageText);
            await _order.CreateIfNotExistsAsync();

            var order = JsonSerializer.Deserialize<OrderEntity>(message.MessageText);
            if (order == null)
            {
                _logger.LogError("Failed to deserialize OrderEntity.");
                return;
            }

            order.RowKey = Guid.NewGuid().ToString();
            order.PartitionKey = "Order";
            order.Timestamp = DateTimeOffset.UtcNow;

            await _order.AddEntityAsync(order);
            _logger.LogInformation($"Order saved successfully with RowKey: {order.RowKey}");
        }

        // ------------------- GET ALL DATA -------------------

        [Function("GetCustomers")]
        public async Task<HttpResponseData> GetCustomers([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "customers")] HttpRequestData req)
        {
            _logger.LogInformation("Fetching all customers...");
            try
            {
                var customers = _customer.Query<CustomerEntity>().ToList();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(customers);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customers");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync("Error retrieving customers.");
                return error;
            }
        }

        [Function("GetProducts")]
        public async Task<HttpResponseData> GetProducts([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "products")] HttpRequestData req)
        {
            _logger.LogInformation("Fetching all products...");
            try
            {
                var products = _product.Query<ProductEntity>().ToList();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(products);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving products");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync("Error retrieving products.");
                return error;
            }
        }

        [Function("GetOrders")]
        public async Task<HttpResponseData> GetOrders([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders")] HttpRequestData req)
        {
            _logger.LogInformation("Fetching all orders...");
            try
            {
                var orders = _order.Query<OrderEntity>().ToList();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(orders);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving orders");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync("Error retrieving orders.");
                return error;
            }
        }

        // ------------------- ADD FUNCTIONS -------------------

        [Function("AddCustomer")]
        public async Task<HttpResponseData> AddCustomer([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customers")] HttpRequestData req)
        {
            _logger.LogInformation("AddCustomer function received a request.");
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            var customer = JsonSerializer.Deserialize<CustomerEntity>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (customer == null || string.IsNullOrEmpty(customer.Customer_Name) || string.IsNullOrEmpty(customer.email))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid customer data.");
                return badResponse;
            }

            customer.PartitionKey = "Customer";
            customer.RowKey = Guid.NewGuid().ToString();

            await _customer.AddEntityAsync(customer);
            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteStringAsync("Customer added successfully.");
            return response;
        }

        [Function("AddProduct")]
        public async Task<HttpResponseData> AddProduct([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "products")] HttpRequestData req)
        {
            _logger.LogInformation("AddProduct function received a request.");
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            var product = JsonSerializer.Deserialize<ProductEntity>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (product == null || string.IsNullOrEmpty(product.ProductName) || product.Price == null || product.Price <= 0)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid product data.");
                return badResponse;
            }

            product.PartitionKey = "Product";
            product.RowKey = Guid.NewGuid().ToString();

            await _product.AddEntityAsync(product);
            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteStringAsync("Product added successfully.");
            return response;
        }

        [Function("AddOrder")]
        public async Task<HttpResponseData> AddOrder([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders")] HttpRequestData req)
        {
            _logger.LogInformation("AddOrder function received a request.");
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            var order = JsonSerializer.Deserialize<OrderEntity>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (order == null || string.IsNullOrEmpty(order.CustomerId) || string.IsNullOrEmpty(order.ProductId) || order.Quantity <= 0)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid order data.");
                return badResponse;
            }

            order.PartitionKey = "Order";
            order.RowKey = Guid.NewGuid().ToString();
            order.Timestamp = DateTimeOffset.UtcNow;

            await _order.AddEntityAsync(order);

            // Queue message for async processing
            var queueClient = new QueueClient(_storageconnection, "order-queue");
            await queueClient.CreateIfNotExistsAsync();
            await queueClient.SendMessageAsync(JsonSerializer.Serialize(order));

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteStringAsync("Order added and queued successfully.");
            return response;
        }

        [Function("StoreFileFunction")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("Received file info for queueing.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var fileEntity = JsonSerializer.Deserialize<FileEntity>(requestBody);

            if (fileEntity == null)
            {
                _logger.LogError("Invalid file info received.");
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid file data.");
                return badResponse;
            }

            // Add to Azure Queue
            QueueClient queueClient = new QueueClient(_storageconnection, "file-queue");
            await queueClient.CreateIfNotExistsAsync();
            await queueClient.SendMessageAsync(JsonSerializer.Serialize(fileEntity));

            _logger.LogInformation($"Queued file info: {fileEntity.Name}");

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync($"File info for '{fileEntity.Name}' queued successfully.");
            return response;
        }

    }
}
