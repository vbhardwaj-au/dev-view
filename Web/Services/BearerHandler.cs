using System.Net.Http.Headers;

namespace Web.Services
{
    public class BearerHandler : DelegatingHandler
    {
        public static string? Token { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(Token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            }
            return base.SendAsync(request, cancellationToken);
        }
    }
}


