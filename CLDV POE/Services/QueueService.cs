using Azure.Storage.Queues;

namespace CLDV_POE.Services
{
    public class QueueService
    {

        private readonly QueueClient _queueClient;

        public QueueService(string connectionString, string queueName)
        {
            _queueClient = new QueueClient(connectionString, queueName);
        }

        public async Task SendMessageAsync(string message)
        {
            await _queueClient.SendMessageAsync(message);
        }

    }
}
