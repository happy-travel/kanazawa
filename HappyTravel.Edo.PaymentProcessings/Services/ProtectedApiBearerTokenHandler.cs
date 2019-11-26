using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HappyTravel.Edo.PaymentProcessings.Models;
using IdentityModel.Client;
using Microsoft.Extensions.Options;

namespace HappyTravel.Edo.PaymentProcessings.Services
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
            request.SetBearerToken(await GetToken());
            return await base.SendAsync(request, cancellationToken);
        }


        private async Task<string> GetToken()
        {
            // TODO: Check token lifetime.
            // We need to cache token because we will send several requests in short periods.
            if (!string.IsNullOrEmpty(_token))
                return _token;

            var client = _clientFactory.CreateClient(HttpClientNames.Identity);
            // request the access token token
            var tokenResponse = await client.RequestClientCredentialsTokenAsync(_tokenRequest);
            if (tokenResponse.IsError)
                throw new HttpRequestException($"Something went wrong while requesting the access token. Error: {tokenResponse.Error}");

            _token = tokenResponse.AccessToken;
            return _token;
        }


        private readonly IHttpClientFactory _clientFactory;
        private readonly ClientCredentialsTokenRequest _tokenRequest;
        private string _token;
    }
}