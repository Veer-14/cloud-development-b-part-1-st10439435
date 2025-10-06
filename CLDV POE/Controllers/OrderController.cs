using CLDV_POE.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CLDV_POE.Controllers
{
    public class OrderController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseFunctionUrl = "https://functionapptasveerst10439435poe-bsdad4cbd9cjchej.southafricanorth-01.azurewebsites.net/api/";

        public OrderController(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IActionResult> Index()
        {
            var response = await _httpClient.GetAsync(_baseFunctionUrl + "orders");

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await response.Content.ReadAsStringAsync();
                ViewBag.Error = $"Failed to load orders. Server said: {errorMessage}";
                return View(new List<Order>()); // Return empty list so the view still loads
            }

            var content = await response.Content.ReadAsStringAsync();
            var orders = JsonConvert.DeserializeObject<List<Order>>(content);

            return View(orders);
        }

        public async Task<IActionResult> Register()
        {
            var customers = await _httpClient.GetFromJsonAsync<List<Customer>>(_baseFunctionUrl + "customers");
            var products = await _httpClient.GetFromJsonAsync<List<Product>>(_baseFunctionUrl + "products");

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
            if (!ModelState.IsValid)
            {
                ViewData["Customer"] = await _httpClient.GetFromJsonAsync<List<Customer>>(_baseFunctionUrl + "customers");
                ViewData["Product"] = await _httpClient.GetFromJsonAsync<List<Product>>(_baseFunctionUrl + "products");
                return View(order);
            }

            order.PartitionKey = "Order";
            order.RowKey = Guid.NewGuid().ToString();
            order.OrderDate = DateTime.SpecifyKind(order.OrderDate, DateTimeKind.Utc);

            var json = JsonConvert.SerializeObject(order);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            
            var response = await _httpClient.PostAsync(_baseFunctionUrl + "orders", content);

            if (response.IsSuccessStatusCode)
                return RedirectToAction("Index");

            ModelState.AddModelError("", "Error saving order via Azure Function.");
            ViewData["Customer"] = await _httpClient.GetFromJsonAsync<List<Customer>>(_baseFunctionUrl + "customers");
            ViewData["Product"] = await _httpClient.GetFromJsonAsync<List<Product>>(_baseFunctionUrl + "products");
            return View(order);
        }

    }
}
