

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
using web_proxy.Helpers;


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
        [HttpGet]
        [HttpPost]
        public async Task<IActionResult> CatchAllAsync(string catchAll = "")
        {
            try
            {
                if (!(Request.Path.ToString().Contains("/818fbfd7-0338-45d3-8cc8-8d521cc578b2/") || Request.Path.ToString().Contains("/common/")))
                {
                    return Ok();
                }

                string targetURL = $"https://wggdemo.ciamlogin.com{this.Request.Path}{this.Request.QueryString}";

                // Create HTTP client 
                HttpClient client = _HttpClientFactory.CreateClient("ConfiguredHttpClientHandler");

                // Copy the request headers
                foreach (var header in Request.Headers)
                {
                    if (header.Key != "Host" && header.Key != "Accept-Encoding")
                    {
                        client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                    }
                }

                // Add the X-Forwarded-For
                client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", Request.Host.Value);

                HttpResponseMessage response = null;
                string contentType = "text/html";

                // Copy the request HTTP body (if any)
                if (HttpMethods.IsPost(Request.Method))
                {
                    HttpContent content = null;

                    if (Request.ContentType.Contains("application/json"))
                    {
                        // Set the content type to JSON
                        contentType = "application/json";

                        // Read the request body 
                        string body = await new System.IO.StreamReader(this.Request.Body).ReadToEndAsync();

                        // Add the JOSN payload
                        content = new StringContent(body, Encoding.UTF8, contentType);
                    }
                    else
                    {
                        // IMPORTANT: this code supports only application/x-www-form-urlencoded
                        var dict = new Dictionary<string, string>();
                        foreach (var item in Request.Form)
                        {
                            dict.Add(item.Key, item.Value);
                        }

                        content = new FormUrlEncodedContent(dict);
                    }


                    // Send an HTTP POST request
                    response = await client.PostAsync(targetURL, content);
                }
                else
                {
                    // Send an HTTP GET request
                    response = await client.GetAsync(targetURL);
                }

                // Read the response body
                string responseBody = string.Empty;
                if (response.IsSuccessStatusCode)
                {
                    responseBody = await response.Content.ReadAsStringAsync();

                    string domain = Request.Host.Value;
                    
                    // Check for the original host header X-Forwarded-Host (if exists)
                    if (Request.Headers.ContainsKey("X-Forwarded-Host") && Request.Headers["X-Forwarded-Host"].Count > 0)
                    {
                        domain = Request.Headers["X-Forwarded-Host"][0]!;
                    }

                    response.Content = new StringContent(
                        responseBody.Replace("wggdemo.ciamlogin.com", domain),
                        Encoding.UTF8,
                        contentType);
                }

                // Add application insights page telemetry
                PageViewTelemetry pageView = new PageViewTelemetry("Proxy");
                pageView.Properties.Add("Request_URL", $"{Request.Scheme}://{Request.Host}{Request.Path}{Request.QueryString}");
                pageView.Properties.Add("Target_URL", targetURL);
                pageView.Properties.Add("Request_Method", Request.Method);
                pageView.Properties.Add("Request_Headers", JsonSerializer.Serialize(Request.Headers));
                pageView.Properties.Add("Response_Headers", JsonSerializer.Serialize(response.Headers));

                if (Request.Query.ContainsKey("test") && Request.Query["test"].Count > 0)
                {
                    pageView.Properties.Add("TestID", Request.Query["test"][0]);
                }

                //pageView.Properties.Add("Response_Body", responseBody);

                this._telemetry.TrackPageView(pageView);

                // Here we ask the framework to dispose the response object a the end of the user request
                this.HttpContext.Response.RegisterForDispose(response);

                // Return the respons that return from the call to the web server
                return new HttpResponseMessageResult(response);

            }
            catch (System.Exception ex)
            {
                //Commons.LogError(Request, _telemetry, settings, tenantId, EVENT + "Error", ex.Message, response);
                //return BadRequest(new { error = ex.Message });
                return Ok(ex.Message);
            }
        }
    }
}
