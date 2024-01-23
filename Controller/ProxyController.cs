

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography.X509Certificates;
using Microsoft.ApplicationInsights;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Primitives;
using System.Text.Unicode;
using Microsoft.ApplicationInsights.DataContracts;


namespace web_proxy.Controllers
{
    [ApiController]
    public class ProxyController : Controller
    {
        private readonly ILogger<ProxyController> _logger;
        private TelemetryClient _telemetry;
        protected readonly IHttpClientFactory _HttpClientFactory;

        public ProxyController(ILogger<ProxyController> logger, IHttpClientFactory httpClientFactory, TelemetryClient telemetry)
        {
            this._logger = logger;
            _telemetry = telemetry;
            _HttpClientFactory = httpClientFactory;
        }


        [Route("{**catchall}")]
        public async Task<ContentResult> CatchAllAsync(string catchAll = "")
        {
            try
            {
                if (!Request.Path.ToString().Contains("/oauth2/")){
                    return null;
                }

                string targetURL = $"https://wggdemo.ciamlogin.com{this.Request.Path}{this.Request.QueryString}";
                HttpClient client = _HttpClientFactory.CreateClient();

                // Copy the request headers
                foreach (var header in Request.Headers)
                {
                    if (header.Key != "Host" && header.Key != "Accept-Encoding")
                    {
                        client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                    }
                }

                // Send the HTTP GET request
                HttpResponseMessage response = await client.GetAsync(targetURL);

                // Read the response body
                string responseBody = string.Empty;
                if (response.IsSuccessStatusCode)
                {
                    responseBody = await response.Content.ReadAsStringAsync();
                }

                this.Response.Headers.Clear();

                // Copy the response headers
                foreach (var header in response.Headers)
                {
                    Response.Headers.Add(header.Key, header.Value.ToArray());
                }

                // Add application insights page telemetry
                PageViewTelemetry pageView = new PageViewTelemetry("Proxy");
                pageView.Properties.Add("Request_URL", $"{Request.Scheme}://{Request.Host}{Request.Path}{Request.QueryString}");
                pageView.Properties.Add("Target_URL", targetURL);
                pageView.Properties.Add("Request_Method", Request.Method);
                pageView.Properties.Add("Request_Headers", JsonSerializer.Serialize(Request.Headers));
                pageView.Properties.Add("Response_Headers", JsonSerializer.Serialize(response.Headers));
                //pageView.Properties.Add("Response_Body", responseBody);

                this._telemetry.TrackPageView(pageView);

                //Response the content
                return new ContentResult
                {
                    Content = responseBody,
                    ContentType = "text/html"
                };
            }
            catch (System.Exception ex)
            {
                //Commons.LogError(Request, _telemetry, settings, tenantId, EVENT + "Error", ex.Message, response);
                //return BadRequest(new { error = ex.Message });
                return null;
            }
        }
    }
}
