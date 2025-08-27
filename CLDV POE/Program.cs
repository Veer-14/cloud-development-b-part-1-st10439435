using CLDV_POE.Services;

namespace CLDV_POE
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var configuration = builder.Configuration;

            // Add services to the container.
            builder.Services.AddControllersWithViews();

            // Make sure your connection string name matches what's in appsettings.json
            builder.Services.AddSingleton(new TableStorageService(
                configuration.GetConnectionString("AzureStorage")
            ));

            builder.Services.AddSingleton(new BlobService(
                configuration.GetConnectionString("AzureStorage")
            ));
            builder.Services.AddSingleton<QueueService>(sp =>
            {
                var connectionString = configuration.GetConnectionString("AzureStorage");
                return new QueueService(connectionString, "orders");
            });

            builder.Services.AddSingleton<AzureFileShareService>(sp =>
            {
                var connectionString = configuration.GetConnectionString("AzureStorage");
                return new AzureFileShareService(connectionString, "fileshare");
            });


            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseAuthorization();
            app.UseStaticFiles(); // Added to serve static files

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}