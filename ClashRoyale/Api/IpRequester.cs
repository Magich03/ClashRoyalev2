namespace ClashRoyale.Api
{
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using RestSharp;

    public static class IpRequester
    {
        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="IpRequester"/> has been initialized.
        /// </summary>
        public static bool Initialized { get; set; }

        private static RestClient Rest;
        private static RestRequest Request;

        /// <summary>
        /// Initializes this instance.
        /// </summary>
        public static void Initialize()
        {
            if (IpRequester.Initialized)
            {
                return;
            }

            // Updated Method.Get for modern RestSharp versions.
            IpRequester.Rest = new RestClient("http://ip-api.com");
            IpRequester.Request = new RestRequest("json/{ip}", Method.Get);

            Request.AddParameter("fields", "countryCode,region,city,status");

            IpRequester.Initialized = true;
        }

        /// <summary>
        /// Executes a GET request to the Rest API.
        /// </summary>
        public static async Task<IpResponse> GetIpInfo(string IpAddress)
        {
            Request.AddOrUpdateParameter("ip", IpAddress, ParameterType.UrlSegment);

            // Adjust for modern RestSharp async calls.
            var result = await Rest.ExecuteAsync(Request);
            var ipResponse = JsonConvert.DeserializeObject<IpResponse>(result.Content);

            return ipResponse;
        }
    }
}

