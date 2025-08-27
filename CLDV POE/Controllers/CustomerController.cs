using CLDV_POE.Models;
using CLDV_POE.Services;
using Microsoft.AspNetCore.Mvc;

namespace CLDV_POE.Controllers
{
    public class CustomerController : Controller
    {
        private readonly TableStorageService _tableStorageService;
        public CustomerController(TableStorageService tableStorageService)
        { 
          _tableStorageService = tableStorageService;
        }

        public async Task<IActionResult> Index()
        {
            var customers = await _tableStorageService.GetAllCustomersAsync();
            return View(customers);
        }

        public async Task<IActionResult> Delete(string partitionKey, string rowKey)
        {
            await _tableStorageService.DeleteCustomerAsync(partitionKey, rowKey);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> AddCustomer(Customer customer)
        {
            var customers = await _tableStorageService.GetAllCustomersAsync();
            int nextId = customers.Any() ? customers.Max(c => c.CustomerId) + 1 : 1;

            customer.CustomerId = nextId;
            customer.PartitionKey = "CustomersPartition";
            customer.RowKey = Guid.NewGuid().ToString();

            await _tableStorageService.AddCustomerAsync(customer);
            return RedirectToAction("Index");
        }
        [HttpGet] 
        public IActionResult Add_Customer() 
        { return View(new Customer()); }
    }
}
