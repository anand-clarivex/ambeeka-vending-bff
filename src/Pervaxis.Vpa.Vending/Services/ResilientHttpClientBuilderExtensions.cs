using System.Net;
using Microsoft.Extensions.Options;
using Pervaxis.Vpa.Vending.Models.Configuration;

namespace Pervaxis.Vpa.Vending.Services;

public static class ResilientHttpClientBuilderExtensions
{
    public static IHttpClientBuilder AddForgeResilience(this IHttpClientBuilder builder)
    {
        builder.AddHttpMessageHandler(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ForgeResilienceOptions>>().Value;
            return new ForgeRetryDelegatingHandler(options);
        });

        return builder;
    }

    private sealed class ForgeRetryDelegatingHandler : DelegatingHandler
    {
        private readonly ForgeResilienceOptions _options;

        public ForgeRetryDelegatingHandler(ForgeResilienceOptions options)
        {
            _options = options;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!_options.Enabled)
            {
                return await base.SendAsync(request, cancellationToken);
            }

            var attempt = 0;
            var delay = TimeSpan.FromMilliseconds(Math.Max(1, _options.RetryDelayMilliseconds));
            HttpResponseMessage? response = null;

            while (attempt <= Math.Max(0, _options.RetryCount))
            {
                attempt++;
                response?.Dispose();
                response = await base.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode || !IsTransient(response.StatusCode) || attempt > _options.RetryCount)
                {
                    return response;
                }

                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
            }

            return response!;
        }

        private static bool IsTransient(HttpStatusCode statusCode) =>
            statusCode is HttpStatusCode.RequestTimeout
                or HttpStatusCode.TooManyRequests
                or HttpStatusCode.InternalServerError
                or HttpStatusCode.BadGateway
                or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.GatewayTimeout;
    }
}
