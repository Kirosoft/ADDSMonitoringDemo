using Microsoft.AspNetCore.Mvc;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using System.Text;
using System.Security.Cryptography;


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
    }

    public class ProcessService
    {
        private static ElasticOptions _elasticOptions = new ElasticOptions();
        private static ElasticsearchClient? _client;

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

        private string GetHash(ProcessRequest request)
        {
            var data = Encoding.UTF8.GetBytes($"{request.Prop1}{request.Prop2}{request.Prop3}{request.Prop4}{request.Prop5}");
            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(data);
                return BitConverter.ToString(hashBytes).Replace("-", "");
            }
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

 
    public class ElasticOptions
    {
        public string ApiKey { get; set; } = "";
        public string Url { get; set; } = "";
        public string Fingerprint { get; set; } = "";

        public string CloudId { get; set; } = "";

        public string IndexName { get; set; } = "ADDSDemoMonitoring";
    }
}