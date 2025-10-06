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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Functions;

namespace QueueFunctions
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;
        private readonly string _storageconnection;
        private TableClient _customer;
        private TableClient _product;
        private TableClient _order;
        private BlobContainerClient _blobContainerClient;

        public Function1(ILogger<Function1> logger)
        {
            _logger = logger;
            _storageconnection = "DefaultEndpointsProtocol=https;AccountName=tasveerstoragest10439435;AccountKey=JYQvAc0maXlPBpcpAvetzG2ptvJT8n7iaHMqf8TKJjoQRBCP3frWiTeq8t1hbtMR5WwSHlFqO3eL+AStdi3TGA==;EndpointSuffix=core.windows.net";
            var serviceClient = new TableServiceClient(_storageconnection);
            _customer = serviceClient.GetTableClient("Customer");
            _product = serviceClient.GetTableClient("Product");
            _order = serviceClient.GetTableClient("orders");
            _blobContainerClient = new BlobContainerClient(_storageconnection, "images");
            _blobContainerClient.CreateIfNotExists();
        }

        #region Queue Triggers

        [Function(nameof(QueueCustomerSender))]
        public async Task QueueCustomerSender([QueueTrigger("customer-queue", Connection = "connection")] QueueMessage message)
        {
            _logger.LogInformation("Processing customer queue message.");

            await _customer.CreateIfNotExistsAsync();

            var customer = JsonSerializer.Deserialize<Customer>(message.MessageText);
            if (customer == null)
            {
                _logger.LogError("Failed to deserialize customer message.");
                return;
            }

            customer.PartitionKey ??= "Customer";
            customer.RowKey ??= Guid.NewGuid().ToString();

            await _customer.AddEntityAsync(customer);
            _logger.LogInformation($"Customer saved with RowKey: {customer.RowKey}");
        }

        [Function(nameof(QueueProductSender))]
        public async Task QueueProductSender([QueueTrigger("product-queue", Connection = "connection")] QueueMessage message)
        {
            _logger.LogInformation("Processing product queue message.");

            await _product.CreateIfNotExistsAsync();

            var product = JsonSerializer.Deserialize<Product>(message.MessageText);
            if (product == null)
            {
                _logger.LogError("Failed to deserialize product message.");
                return;
            }

            product.PartitionKey ??= "Product";
            product.RowKey ??= Guid.NewGuid().ToString();

            await _product.AddEntityAsync(product);
            _logger.LogInformation($"Product saved with RowKey: {product.RowKey}");
        }

        [Function(nameof(QueueOrderSender))]
        public async Task QueueOrderSender([QueueTrigger("order-queue", Connection = "connection")] QueueMessage message)
        {
            _logger.LogInformation("Processing order queue message.");

            await _order.CreateIfNotExistsAsync();

            var orderMsg = JsonSerializer.Deserialize<Order>(message.MessageText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (orderMsg == null)
            {
                _logger.LogError("Failed to deserialize order message.");
                return;
            }

            orderMsg.PartitionKey ??= "Order";
            orderMsg.RowKey ??= Guid.NewGuid().ToString();
            orderMsg.Timestamp = DateTimeOffset.UtcNow;

            await _order.AddEntityAsync(orderMsg);
            _logger.LogInformation($"Order saved with RowKey: {orderMsg.RowKey}");
        }

        #endregion

        #region HTTP Get Endpoints

        [Function("GetCustomers")]
        public async Task<HttpResponseData> GetCustomers([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "customers")] HttpRequestData req)
        {
            try
            {
                var customers = await _customer.QueryAsync<Customer>().ToListAsync();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(customers);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query customers.");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync("Failed to retrieve customers.");
                return error;
            }
        }

        [Function("GetProducts")]
        public async Task<HttpResponseData> GetProducts([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "products")] HttpRequestData req)
        {
            try
            {
                var products = await _product.QueryAsync<Product>().ToListAsync();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(products);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query products.");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync("Failed to retrieve products.");
                return error;
            }
        }

        [Function("GetOrders")]
        public async Task<HttpResponseData> GetOrders([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders")] HttpRequestData req)
        {
            try
            {
                var orders = await _order.QueryAsync<Order>().ToListAsync();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(orders);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"GetOrders failed: {ex.Message}");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync($"Failed to retrieve orders: {ex.Message}");
                return error;
            }
        }

        #endregion

        #region HTTP Post Endpoints

        [Function("AddCustomer")]
        public async Task<HttpResponseData> AddCustomer([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customers")] HttpRequestData req)
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            var customer = JsonSerializer.Deserialize<Customer>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (customer == null || string.IsNullOrEmpty(customer.Customer_Name) || string.IsNullOrEmpty(customer.email))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid customer data.");
                return bad;
            }

            customer.PartitionKey ??= "Customer";
            customer.RowKey ??= Guid.NewGuid().ToString();

            await _customer.AddEntityAsync(customer);

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteStringAsync("Customer added successfully.");
            return response;
        }

        [Function("AddProduct")]
        public async Task<HttpResponseData> AddProduct([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "products")] HttpRequestData req)
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();

            // Deserialize product from JSON
            var product = JsonSerializer.Deserialize<Product>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (product == null || string.IsNullOrEmpty(product.ProductName) || string.IsNullOrEmpty(product.ProductDescription) || product.Price == null)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid product data.");
                return bad;
            }

            // Set PartitionKey and RowKey
            product.PartitionKey ??= "Product";
            product.RowKey ??= Guid.NewGuid().ToString();

            

            // Add product to Table Storage
            await _product.AddEntityAsync(product);

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteStringAsync("Product added successfully with image uploaded to Blob Storage.");
            return response;
        }


        [Function("AddOrder")]
        public async Task<HttpResponseData> AddOrder([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders")] HttpRequestData req)
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            var order = JsonSerializer.Deserialize<Order>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (order == null || string.IsNullOrEmpty(order.CustomerId) || string.IsNullOrEmpty(order.ProductId) || order.Quantity <= 0)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid order data.");
                return bad;
            }

            order.PartitionKey ??= "Order";
            order.RowKey ??= Guid.NewGuid().ToString();
            order.Timestamp = DateTimeOffset.UtcNow;

            await _order.CreateIfNotExistsAsync();
            await _order.AddEntityAsync(order);

            // Queue the order
            var queueClient = new QueueClient(_storageconnection, "order-queue");
            await queueClient.CreateIfNotExistsAsync();
            string json = JsonSerializer.Serialize(order);
            await queueClient.SendMessageAsync(json);

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteStringAsync("Order added and queued successfully.");
            return response;
        }

        #endregion

        #region Azure File Uploads

        [Function("UploadToAzureFiles")]
        public async Task<HttpResponseData> UploadToAzureFiles([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "uploads")] HttpRequestData req)
        {
            try
            {
                var contentType = req.Headers.GetValues("Content-Type").FirstOrDefault();
                if (string.IsNullOrEmpty(contentType) || !contentType.Contains("multipart/form-data"))
                    return await CreateBadRequest(req, "Request must be multipart/form-data");

                var boundary = HeaderUtilities.RemoveQuotes(MediaTypeHeaderValue.Parse(contentType).Boundary).Value;
                var reader = new MultipartReader(boundary, req.Body);
                var section = await reader.ReadNextSectionAsync();

                if (section == null ||
                    !ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition) ||
                    string.IsNullOrEmpty(contentDisposition.FileName.Value))
                {
                    return await CreateBadRequest(req, "No file found in request.");
                }

                string fileName = contentDisposition.FileName.Value.Trim('"');
                using var memoryStream = new MemoryStream();
                await section.Body.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                string shareName = "uploads";
                var shareClient = new ShareClient(_storageconnection, shareName);
                await shareClient.CreateIfNotExistsAsync();

                var rootDir = shareClient.GetRootDirectoryClient();
                var fileClient = rootDir.GetFileClient(fileName);
                await fileClient.CreateAsync(memoryStream.Length);
                memoryStream.Position = 0;
                await fileClient.UploadRangeAsync(new HttpRange(0, memoryStream.Length), memoryStream);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync($"File '{fileName}' uploaded successfully!");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file to Azure File Share.");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync($"Upload failed: {ex.Message}");
                return error;
            }
        }

        [Function("GetUploadedFiles")]
        public async Task<HttpResponseData> GetUploadedFiles([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "uploads")] HttpRequestData req)
        {
            var files = new List<FileModel>();
            try
            {
                string shareName = "uploads";
                var shareClient = new ShareClient(_storageconnection, shareName);
                await shareClient.CreateIfNotExistsAsync();

                var rootDir = shareClient.GetRootDirectoryClient();
                await foreach (var item in rootDir.GetFilesAndDirectoriesAsync())
                {
                    if (!item.IsDirectory)
                    {
                        var fileClient = rootDir.GetFileClient(item.Name);
                        var props = await fileClient.GetPropertiesAsync();
                        files.Add(new FileModel
                        {
                            Name = item.Name,
                            Size = props.Value.ContentLength,
                            LastModified = props.Value.LastModified
                        });
                    }
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(files);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list files from Azure File Share.");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync($"Failed to list files: {ex.Message}");
                return error;
            }
        }

        #endregion

        private static async Task<HttpResponseData> CreateBadRequest(HttpRequestData req, string message)
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteStringAsync(message);
            return response;
        }

        [Function("DeleteCustomer")]
        public async Task<HttpResponseData> DeleteCustomer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "DeleteCustomer")] HttpRequestData req)
        {
            try
            {
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                string partitionKey = query["partitionKey"];
                string rowKey = query["rowKey"];

                if (string.IsNullOrEmpty(partitionKey) || string.IsNullOrEmpty(rowKey))
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("Missing partitionKey or rowKey.");
                    return bad;
                }

                await _customer.DeleteEntityAsync(partitionKey, rowKey);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync("Customer deleted successfully.");
                return response;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("Customer not found.");
                return notFound;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete customer.");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync($"Failed to delete customer: {ex.Message}");
                return error;
            }
        }

        [Function("DeleteProduct")]
        public async Task<HttpResponseData> DeleteProduct(
    [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "DeleteProduct")] HttpRequestData req)
        {
            try
            {
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                string partitionKey = query["partitionKey"];
                string rowKey = query["rowKey"];

                if (string.IsNullOrEmpty(partitionKey) || string.IsNullOrEmpty(rowKey))
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("Missing partitionKey or rowKey.");
                    return bad;
                }

                // Delete product from table
                await _product.DeleteEntityAsync(partitionKey, rowKey);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync("Product deleted successfully.");
                return response;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("Product not found.");
                return notFound;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete product.");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync($"Failed to delete product: {ex.Message}");
                return error;
            }
        }

       


    }

}
