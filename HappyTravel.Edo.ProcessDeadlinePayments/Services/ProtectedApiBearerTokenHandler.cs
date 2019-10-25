using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HappyTravel.Edo.ProcessDeadlinePayments.Models;
using IdentityModel.Client;
using Microsoft.Extensions.Options;

namespace HappyTravel.Edo.ProcessDeadlinePayments.Services
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
            using var client = _clientFactory.CreateClient(HttpClientNames.Identity);
            // request the access token token
            var tokenResponse = await client.RequestClientCredentialsTokenAsync(_tokenRequest);
            if (tokenResponse.IsError)
                throw new HttpRequestException($"Something went wrong while requesting the access token. Error: {tokenResponse.Error}");

            return tokenResponse.AccessToken;
        }


        private readonly IHttpClientFactory _clientFactory;
        private readonly ClientCredentialsTokenRequest _tokenRequest;
    }
}
