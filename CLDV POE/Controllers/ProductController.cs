using CLDV_POE.Models;
using CLDV_POE.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CLDV_POE.Controllers
{
    public class ProductController : Controller
    {
        private readonly BlobService _blobService;
        private readonly HttpClient _httpClient;
        private readonly string _baseFunctionUrl = "https://functionapptasveerst10439435poe-bsdad4cbd9cjchej.southafricanorth-01.azurewebsites.net/api/";

        public ProductController(BlobService blobService, HttpClient httpClient)
        {
            _blobService = blobService;
            _httpClient = httpClient;
        }

        public async Task<IActionResult> Index()
        {
            var response = await _httpClient.GetAsync(_baseFunctionUrl + "products");
            var content = await response.Content.ReadAsStringAsync();
            var products = JsonSerializer.Deserialize<List<Product>>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return View(products);
        }

        [HttpGet]
        public IActionResult AddProduct() => View();

        [HttpPost]
        public async Task<IActionResult> AddProduct(Product product, IFormFile file)
        {
            if (file != null)
            {
                using var stream = file.OpenReadStream();
                product.ImageUrl = await _blobService.UploadAsync(stream, file.FileName);
            }

            var json = JsonSerializer.Serialize(product);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_baseFunctionUrl + "products", content);

            if (response.IsSuccessStatusCode)
                return RedirectToAction("Index");

            ModelState.AddModelError("", "Failed to add product via Azure Function.");
            return View(product);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProduct(string partitionKey, string rowKey, string imageUrl)
        {
            if (!string.IsNullOrEmpty(imageUrl))
                await _blobService.DeleteBlobAsync(imageUrl);

            var deleteUrl = $"{_baseFunctionUrl}DeleteProduct?partitionKey={partitionKey}&rowKey={rowKey}";
            await _httpClient.DeleteAsync(deleteUrl);

            return RedirectToAction("Index");
        }
    }
}
