using Microsoft.AspNetCore.Mvc;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using System.Text;
using System.Security.Cryptography;
using System.Threading.Tasks.Dataflow;
using Elastic.Clients.Elasticsearch.Aggregations;


namespace ADDSMonitoringDemo.Controllers
{

    [ApiController]
    [Route("[controller]")]
    public class ADDSDemoController : ControllerBase
    {
        private readonly ProcessService _processService;

        public ADDSDemoController()
        {
            _processService = new ProcessService();
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartProcess(ProcessRequest request)
        {
            var id = await _processService.StartProcessAsync(request);
            return Ok(new { ProcessId = id });
        }

        [HttpPost("stop")]
        public async Task<IActionResult> StopProcess(ProcessRequest request)
        {
            await _processService.StopProcessAsync(request);
            return Ok();
        }
        [HttpPost("create_demo_data")]
        public async Task<IActionResult> CreateDemoData(int numSamples)
        {
            await _processService.CreateDemoData(numSamples);
            return Ok();
        }

    }

    public class ProcessService
    {
        private static ElasticOptions _elasticOptions = new ElasticOptions();
        private static ElasticsearchClient? _client;
        private static readonly char[] Vowels = { 'a', 'e', 'i', 'o', 'u' };
        private static readonly char[] Consonants = { 'b', 'c', 'd', 'f', 'g', 'h', 'j', 'k', 'l', 'm', 'n', 'p', 'q', 'r', 's', 't', 'v', 'w', 'x', 'y', 'z' };


        public ProcessService()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config.json", optional: false);
            IConfiguration config = builder.Build();
            _elasticOptions = config.GetSection("ElasticOptions").Get<ElasticOptions>() ?? new ElasticOptions();
            var settings = new ElasticsearchClientSettings(_elasticOptions.CloudId, new ApiKey(_elasticOptions.ApiKey));
            _client = new ElasticsearchClient(settings);
        }

        public async Task<string> StartProcessAsync(ProcessRequest request)
        {
            var id = GetHash(request);
            var document = new ProcessDocument
            {
                Id = id,
                Prop1 = request.Prop1,
                Prop2 = request.Prop2,
                Prop3 = request.Prop3,
                Prop4 = request.Prop4,
                Prop5 = request.Prop5,
                StartTimestamp = DateTime.UtcNow
            };

            var result = await _client.IndexAsync(document, idx => idx.Index("addsmonitoring"));

            return id;
        }
        public async Task StopProcessAsync(ProcessRequest request)
        {
            var id = GetHash(request);
            var startProcess = await _client.GetAsync<ProcessDocument>(id, idx => idx.Index("addsmonitoring"));
            var startTimestamp = startProcess.Source.StartTimestamp;
            var stopTimestamp = DateTime.UtcNow;
            var duration =  stopTimestamp - startTimestamp;

            var document = new ProcessDocument
            {
                Id = id,
                Prop1 = request.Prop1,
                Prop2 = request.Prop2,
                Prop3 = request.Prop3,
                Prop4 = request.Prop4,
                Prop5 = request.Prop5,
                StartTimestamp = startTimestamp,
                StopTimestamp = stopTimestamp,
                Duration = duration
            };

            await _client.IndexAsync(document, idx => idx.Index("addsmonitoring"));
        }

        public async Task CreateDemoData(int numSamples)
        {
            for (int i = 0; i < numSamples; i++)
            {
                var productName = GetRandomProductName();
                var jobDurationDays = GetRandomDuration(14);
                var durationMinutes = Convert.ToInt32(jobDurationDays.TotalMinutes);
                var stopTimestamp = DateTime.UtcNow-GetRandomDuration(14);
                var startTimestamp = stopTimestamp-jobDurationDays;
                var releasable = GetRandomBool();
                var editionNumber = new Random().Next(5);
                var updateNumber = new Random().Next(5);
                var statusName = GetRandomProductName();

                var id = GetHash(productName, editionNumber, updateNumber, releasable);

                var document = new DemoDoc
                {
                    Id = id,
                    Timestamp = startTimestamp,
                    _timestamp = startTimestamp,
                    ProductName = productName,    
                    StartTimestamp = startTimestamp,
                    StopTimestamp = stopTimestamp,
                    DurationMinutes = durationMinutes,
                    StatusName = statusName,
                    EditionNumber = editionNumber,
                    UpdateNumber = updateNumber,
                    Releasable = releasable
                };
                var result = await _client.IndexAsync(document, idx => idx.Index("addsmonitoring_mark"));
            }

        }

        private TimeSpan GetRandomDuration(int maxDays) {
            Random random = new Random();

            // Generate a random number of days, hours, and minutes
            int days = random.Next(maxDays + 1);
            int hours = random.Next(24);
            int minutes = random.Next(60);

            // Create a TimeSpan with the random values
            TimeSpan duration = new TimeSpan(days, hours, minutes, 0);

            return duration;
        }

        private string GetHash(ProcessRequest request)
        {
            var data = Encoding.UTF8.GetBytes($"{request.Prop1}{request.Prop2}{request.Prop3}{request.Prop4}{request.Prop5}");
            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(data);
                return BitConverter.ToString(hashBytes).Replace("-", "");
            }
        }

        private string GetHash(string productName, int editionNumber, int updateNumber, bool releasable)
        {
            var data = Encoding.UTF8.GetBytes($"{productName}{editionNumber}{updateNumber}{releasable}");
            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(data);
                return BitConverter.ToString(hashBytes).Replace("-", "");
            }
        }

        public static string GetRandomProductName(int maxLength = 10)
        {
            // Create a random number generator
            Random random = new Random();
            
            // Determine the length of the product name (at least 1 character, up to maxLength)
            int nameLength = random.Next(1, maxLength + 1);
            
            // Use StringBuilder for building the name
            StringBuilder nameBuilder = new StringBuilder();

            for (int i = 0; i < nameLength; i++)
            {
                // Alternate between consonants and vowels for readability
                char nextChar = (i % 2 == 0) ? Consonants[random.Next(Consonants.Length)] : Vowels[random.Next(Vowels.Length)];
                nameBuilder.Append(nextChar);
            }

            // Convert the first character to uppercase to simulate a proper name
            if (nameBuilder.Length > 0)
            {
                nameBuilder[0] = char.ToUpper(nameBuilder[0]);
            }

            return nameBuilder.ToString();
        }

        private bool GetRandomBool()
        {
            // Create a random number generator
            Random random = new Random();
            
            // Generate a random number (0 or 1)
            int randomNumber = random.Next(2);  // The argument 2 ensures values 0 or 1
            
            // Return true if randomNumber is 1, false if 0
            return randomNumber == 1;
        }
    }
    
    public class ProcessRequest
    {
        public string Prop1 { get; set; }
        public string Prop2 { get; set; }
        public string Prop3 { get; set; }
        public string Prop4 { get; set; }
        public string Prop5 { get; set; }
    }

    public class ProcessDocument
    {
        public string Id { get; set; }
        public string Prop1 { get; set; }
        public string Prop2 { get; set; }
        public string Prop3 { get; set; }
        public string Prop4 { get; set; }
        public string Prop5 { get; set; }
        public DateTime StartTimestamp { get; set; }
        public DateTime StopTimestamp { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsComplete { get; set; }
        public bool Timestamp { get; set; }
    }

    public class DemoDoc
    {
        public string Id { get; set; }
        public string ProductName { get; set; }
        public string StatusName { get; set; }
        public int UpdateNumber { get; set; }
        public int  EditionNumber { get; set; }
        public DateTime StartTimestamp { get; set; }
        public DateTime StopTimestamp { get; set; }
        public int DurationMinutes { get; set; }
        public bool IsComplete { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime _timestamp { get; set; }
        public bool Releasable { get; set; }
    }
 
    public class ElasticOptions
    {
        public string ApiKey { get; set; } = "";
        public string Url { get; set; } = "";
        public string Fingerprint { get; set; } = "";

        public string CloudId { get; set; } = "";

        public string IndexName { get; set; } = "ADDSDemoMonitoring";
    }
}