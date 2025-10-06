using CLDV_POE.Models;
using CLDV_POE.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CLDV_POE.Controllers
{
    public class FilesController : Controller
    {
        private readonly AzureFileShareService _fileShareService;
        private readonly HttpClient _httpClient;
        private readonly string _baseFunctionUrl = "https://functionapptasveerst10439435poe-bsdad4cbd9cjchej.southafricanorth-01.azurewebsites.net/api/";

        public FilesController(AzureFileShareService fileShareService, HttpClient httpClient)
        {
            _fileShareService = fileShareService;
            _httpClient = httpClient;
        }

        public async Task<IActionResult> Index()
        {
            List<FileModel> files;
            try
            {
                files = await _fileShareService.ListFilesAsync("uploads");
            }
            catch
            {
                files = new List<FileModel>();
            }

            return View(files);
        }

        [HttpPost]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return RedirectToAction("Index");

            using var stream = file.OpenReadStream();
            await _fileShareService.UpLoadFileAsync("uploads", file.FileName, stream);

            var fileEntity = new FileModel
            {
                Name = file.FileName,
                Size = file.Length,
                LastModified = DateTimeOffset.UtcNow
            };

            var json = JsonSerializer.Serialize(fileEntity);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            await _httpClient.PostAsync(_baseFunctionUrl + "StoreFileFunction", content);

            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> DownloadFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return BadRequest();

            var stream = await _fileShareService.DownLoadFileAsync("uploads", fileName);
            if (stream == null) return NotFound();

            return File(stream, "application/octet-stream", fileName);
        }
    }
}
