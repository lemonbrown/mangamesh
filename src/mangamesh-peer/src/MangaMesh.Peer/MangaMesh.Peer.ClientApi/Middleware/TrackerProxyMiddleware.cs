using System.Text;

namespace MangaMesh.Server.Middleware
{
    public class TrackerProxyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _trackerUrl;

        public TrackerProxyMiddleware(RequestDelegate next, IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _next = next;
            _httpClientFactory = httpClientFactory;
            _trackerUrl = config["TrackerUrl"] ?? "https://localhost:7030";
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? "";

            // Proxy /api/auth -> /api (Rewrite)
            if (path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase))
            {
                var targetPath = path.Replace("/api/auth", "/api", StringComparison.OrdinalIgnoreCase);
                await ProxyRequest(context, targetPath);
                return;
            }

            // Proxy /api/mangametadata -> /api/mangametadata (Forward)
            if (path.StartsWith("/api/mangametadata", StringComparison.OrdinalIgnoreCase))
            {
                await ProxyRequest(context, path);
                return;
            }

            // Proxy /api/Series (generic) -> /api/Series (Forward)
            // But verify we don't block local routes? 
            // Local routes: /api/Series/{seriesId}/chapter/{chapterId}/manifest/{manifestHash}/read
            // Tracker routes: /api/Series (POST), /api/Series/search, etc.
            // Simple heuristic: If it contains "/read", let it pass to local.
            if (path.StartsWith("/api/Series", StringComparison.OrdinalIgnoreCase) && !path.Contains("/read", StringComparison.OrdinalIgnoreCase))
            {
                await ProxyRequest(context, path);
                return; 
            }

            await _next(context);
        }

        private async Task ProxyRequest(HttpContext context, string targetPath)
        {
            var client = _httpClientFactory.CreateClient("TrackerProxy");
            
            // Ensure target path has leading slash if needed, but Path.Value usually has it.
            // _trackerUrl might not have trailing slash.
            var baseUri = new Uri(_trackerUrl);
            var targetUri = new Uri(baseUri, targetPath + context.Request.QueryString);

            var requestMessage = new HttpRequestMessage();
            requestMessage.RequestUri = targetUri;
            requestMessage.Method = new HttpMethod(context.Request.Method);

            // Copy content
            if (context.Request.ContentLength > 0 || (context.Request.ContentType != null))
            {
                var streamContent = new StreamContent(context.Request.Body);
                requestMessage.Content = streamContent;
                if (!string.IsNullOrEmpty(context.Request.ContentType))
                {
                    requestMessage.Content.Headers.Add("Content-Type", context.Request.ContentType);
                }
            }

            // Copy headers
            foreach (var header in context.Request.Headers)
            {
                if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase)) continue;
                // Avoid content-type duplication if handled above, but TryAddWithoutValidation handles it?
                // StreamContent headers are separate. 
                
                requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }

            try
            {
                var responseMessage = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
                
                context.Response.StatusCode = (int)responseMessage.StatusCode;

                foreach (var header in responseMessage.Headers)
                {
                    context.Response.Headers[header.Key] = header.Value.ToArray();
                }
                foreach (var header in responseMessage.Content.Headers)
                {
                    context.Response.Headers[header.Key] = header.Value.ToArray();
                }

                context.Response.Headers.Remove("transfer-encoding");

                await responseMessage.Content.CopyToAsync(context.Response.Body);
            }
            catch (Exception)
            {
                context.Response.StatusCode = 502; // Bad Gateway
            }
        }
    }
}
