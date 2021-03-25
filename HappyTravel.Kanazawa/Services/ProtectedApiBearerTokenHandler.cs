using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HappyTravel.Kanazawa.Models;
using IdentityModel.Client;
using Microsoft.Extensions.Options;

namespace HappyTravel.Kanazawa.Services
{
    public class ProtectedApiBearerTokenHandler : DelegatingHandler
    {
        public ProtectedApiBearerTokenHandler(IHttpClientFactory clientFactory, IOptions<ClientCredentialsTokenRequest> tokenRequest)
        {
            _clientFactory = clientFactory;
            _tokenRequest = tokenRequest.Value;
        }


        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var (token, _) = await GetToken();
            request.SetBearerToken(token);
            return await base.SendAsync(request, cancellationToken);
        }


        private async Task<(string Token, DateTime expiryDate)> GetToken()
        {
            await TokenSemaphore.WaitAsync();
            var now = DateTime.UtcNow;
            // We need to cache token because we will send several requests in short periods.
            if (_tokenInfo.Equals(default) || _tokenInfo.ExpiryDate < now)
            {
                var client = _clientFactory.CreateClient(HttpClientNames.Identity);
                // request the access token token
                var tokenResponse = await client.RequestClientCredentialsTokenAsync(_tokenRequest);
                if (tokenResponse.IsError)
                    throw new HttpRequestException($"Something went wrong while requesting the access token. Error: {tokenResponse.Error}");

                _tokenInfo = (tokenResponse.AccessToken, now.AddSeconds(tokenResponse.ExpiresIn));
            }

            TokenSemaphore.Release();
            return _tokenInfo;
        }


        private readonly IHttpClientFactory _clientFactory;
        private readonly ClientCredentialsTokenRequest _tokenRequest;
        private (string Token, DateTime ExpiryDate) _tokenInfo;
        private static readonly SemaphoreSlim TokenSemaphore = new SemaphoreSlim(1, 1);
    }
}