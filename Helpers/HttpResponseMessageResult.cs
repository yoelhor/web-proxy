using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace web_proxy.Helpers
{
    public class HttpResponseMessageResult : IActionResult
    {
        private readonly HttpResponseMessage _responseMessage;

        public HttpResponseMessageResult(HttpResponseMessage responseMessage)
        {
            _responseMessage = responseMessage; // could add throw if null
        }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            context.HttpContext.Response.StatusCode = (int)_responseMessage.StatusCode;

            // Ignore the Transfer-Encoding header if it is just "chunked".
            // We let the host decide about whether the response should be chunked or not.
            if (_responseMessage.Headers.TransferEncodingChunked == true &&
                _responseMessage.Headers.TransferEncoding.Count == 1)
            {
                _responseMessage.Headers.TransferEncoding.Clear();
            }

            foreach (var header in _responseMessage.Headers)
            {
                context.HttpContext.Response.Headers.TryAdd(header.Key, new StringValues(header.Value.ToArray()));
            }

            if (_responseMessage.Content != null)
            {
                var contentHeaders = _responseMessage.Content.Headers;

                // Copy the response content headers only after ensuring they are complete.
                // We ask for Content-Length first because HttpContent lazily computes this
                // and only afterwards writes the value into the content headers.
                var unused = contentHeaders.ContentLength;

                foreach (var header in contentHeaders)
                {
                    context.HttpContext.Response.Headers.Append(header.Key, header.Value.ToArray());
                }

                await _responseMessage.Content.CopyToAsync(context.HttpContext.Response.Body);
            }

        }
    }

}