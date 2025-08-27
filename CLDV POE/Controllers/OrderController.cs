using Azure.Data.Tables;
using Azure;
using CLDV_POE.Models;
using CLDV_POE.Services;
using Microsoft.AspNetCore.Mvc;

namespace MVCAzureBird.Controllers
{
    public class OrderController : Controller
    {
        private readonly TableStorageService _tableStorageService;
        private readonly QueueService _queueService;

        public OrderController(TableStorageService tableStorageService, QueueService queueService)
        {
            _tableStorageService = tableStorageService;
            _queueService = queueService;
        }

        public async Task<IActionResult> Index()
        {
            var orders = await _tableStorageService.GetAllOrdersAsync();
            return View(orders);
        }

        public async Task<IActionResult> Register()
        {
            var customers = await _tableStorageService.GetAllCustomersAsync();
            var products = await _tableStorageService.GetAllProductsAsync();

            if (customers == null || !customers.Any() || products == null || !products.Any())
            {
                ModelState.AddModelError("", "No Customers or Products available. Please ensure they are added first.");
                return View();
            }

            ViewData["Customer"] = customers;
            ViewData["Product"] = products;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(Order order)
        {
            if (ModelState.IsValid)
            {
                order.OrderDate = DateTime.SpecifyKind(order.OrderDate, DateTimeKind.Utc);
                order.PartitionKey = "OrderPartition";
                order.RowKey = Guid.NewGuid().ToString();

                await _tableStorageService.AddOrderAsync(order);

                string message = $"New order by customer {order.CustomerId} " +
                                $"of the product {order.ProductId} on {order.OrderDate}";
                await _queueService.SendMessageAsync(message);

                return RedirectToAction("Index");
            }

            var customers = await _tableStorageService.GetAllCustomersAsync();
            var products = await _tableStorageService.GetAllProductsAsync();
            ViewData["Customer"] = customers;
            ViewData["Product"] = products;
            return View(order);
        }
    }
} 