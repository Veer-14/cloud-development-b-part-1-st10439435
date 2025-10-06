using CLDV_POE.Models;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CLDV_POE.Controllers
{
    public class CustomerController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseFunctionUrl = "https://functionapptasveerst10439435poe-bsdad4cbd9cjchej.southafricanorth-01.azurewebsites.net/api/";

        public CustomerController(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IActionResult> Index()
        {
            var response = await _httpClient.GetAsync(_baseFunctionUrl + "customers");

            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = "Failed to load customers from Azure Function.";
                return View(new List<Customer>());
            }

            var content = await response.Content.ReadAsStringAsync();
            var customers = JsonSerializer.Deserialize<List<Customer>>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return View(customers);
        }

        [HttpGet]
        public IActionResult Add_Customer() => View(new Customer());

        [HttpPost]
        public async Task<IActionResult> AddCustomer(Customer customer)
        {
            if (!ModelState.IsValid) return View("Add_Customer", customer);

            var json = JsonSerializer.Serialize(customer);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_baseFunctionUrl + "customers", content);

            if (response.IsSuccessStatusCode)
                return RedirectToAction("Index");

            ModelState.AddModelError("", "Failed to add customer via Azure Function.");
            return View("Add_Customer", customer);  
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string partitionKey, string rowKey)
        {
            var deleteUrl = $"{_baseFunctionUrl}DeleteCustomer?partitionKey={partitionKey}&rowKey={rowKey}";
            var response = await _httpClient.DeleteAsync(deleteUrl);

            if (!response.IsSuccessStatusCode)
                TempData["Error"] = "Failed to delete customer via Azure Function.";

            return RedirectToAction("Index");
        }
    }
}
