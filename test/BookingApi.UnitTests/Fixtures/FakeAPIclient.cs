
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BookingApi.Abstractions.Api;
using BookingApi.Abstractions.Api.Endpoints;
using BookingApi.Core.Api.Endpoints;
using BookingApi.Core.Models.ShipmentBooking;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using AutoFixture;
using BookingApi.Core.Api;
using Moq;
using Moq.Protected;
using System.Collections.Generic;
using BookingApi.Abstractions.Models.ShipmentBooking;
using BookingApi.Abstractions.Models.ShipmentDimension;
using BookingApi.Core.Models.ShipmentDimension;

namespace BookingApi.UnitTests.Fixtures
{
    public class FakeAPIClient : IApiClient
    {
        public static volatile IApiClient ApiInstance = new FakeAPIClient();

        public static volatile HttpClient _httpClient;


        private bool _isTestingMode = false;
        private string _secretKey = null;
        private string _privateKey = null;
        public FakeAPIClient()
        {

            _serializerSettings = new JsonSerializerSettings {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented,
                Converters = new JsonConverter[] {
                    new StringEnumConverter()

                }
            };
            _httpClient = SetupHttpClient();
        }

        private static HttpClient SetupHttpClient()
        {
            var httpMessageHandler = new Mock<HttpMessageHandler>();
            var fixture = new Fixture();


            httpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync((HttpRequestMessage request, CancellationToken token) => {
                    var response = new HttpResponseMessage();
                    response.StatusCode = System.Net.HttpStatusCode.OK;
                    if (request.RequestUri.Segments[request.RequestUri.Segments.Length - 1] == "dimensions") {
                        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(new ShipmentDimensionResponse() {
                            NorskBarcode = "509125319001",
                            Barcode = "1641620934",
                            Pieces = new List<IDimensions>() {
                                new Dimensions {
                                     Barcode ="1641620934",
                                     ImageUrl = "api/1641620934/Image",
                                      Depth = 30.5000000000m,
                                      Height = 16.0m,
                                      VolumeWeight = 1.0m,
                                      Width = 2.0m,
                                      Weight = 1m


                                }
                            }
                        }

                        ));
                    } else {
                        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(new BookShipmentResponse() {
                            NorskBarcode = "703451258001",
                            Barcode = "1641620934",
                            Label = new byte[1] { 23 },
                            ArchiveDocuments = new List<IShipmentArchiveDocument>() {
                                new ShipmentArchiveDocument(){Contents = new byte[1] { 23 } }
                            },
                            Items = new List<IShipmentBookingItem>()
                            {
                                new ShipmentBookingItem{ Barcode = "1641620934", Label = new byte[1]{ 23 }, NorskBarcode="703451258001", ScanBarcode="XXXXX", Weight=0.78M }
                            }
                        }
                   ));
                    }
                    response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    return response;
                });

            var httpClient = new HttpClient(httpMessageHandler.Object);
            httpClient.BaseAddress = fixture.Create<Uri>();
            return httpClient;
        }

        private readonly JsonSerializerSettings _serializerSettings;
        public string Endpoint => _isTestingMode ? "http://dev-api.norsk-global.com" : "http://api.norsk-global.com";

        public void Authentication(string secretKey, string privateKey)
        {
            _secretKey = secretKey;
            _privateKey = privateKey;
        }

        public async Task<IBookShipmentResponse> BookShipment(Action<IBookShipmentRequest> requestBuilder)
        {
           
            if (string.IsNullOrEmpty(_secretKey) || string.IsNullOrEmpty(_privateKey))
                throw new NotImplementedException();

            var request = new BookShipmentRequest(_httpClient.BaseAddress.ToString());
            requestBuilder(request);

            var rawJson = JsonConvert.SerializeObject(request, _serializerSettings);
            var httpRequest = new HttpRequest<BookShipmentRequest, BookShipmentResponse>(request);

            httpRequest.ConstructRequest(() => {
                var requestDateTime = DateTime.Now;

                var message = new HttpRequestMessage(request.Method, request.Endpoint) {
                    Content = new StringContent(rawJson)
                };

                message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

                var authentication = SignRequest(request.Method, rawJson, message.Content.Headers.ContentType.ToString(),
                    "/api/shipment/", requestDateTime);

                message.Headers.TryAddWithoutValidation("Authorization", $"{_privateKey}:{authentication}");
                message.Headers.Date = requestDateTime;
                return message;
            });
            return await SendRequest(httpRequest);
        }

        public async Task<IBookShipmentDimensionResponse> GetShipmentDimensions(Action<IBookShipmentDimensionRequest> requestBuilder)
        {
            
            if (string.IsNullOrEmpty(_secretKey) || string.IsNullOrEmpty(_privateKey))
                throw new NotImplementedException();

            var request = new ShipmentDimensionRequest(_httpClient.BaseAddress.ToString());
            requestBuilder(request);

            var rawJson = JsonConvert.SerializeObject(request, _serializerSettings);
            var httpRequest = new HttpRequest<ShipmentDimensionRequest, ShipmentDimensionResponse>(request);

            httpRequest.ConstructRequest(() => {
                var requestDateTime = DateTime.Now;

                var message = new HttpRequestMessage(request.Method, request.Endpoint) {
                    Content = new StringContent(rawJson)
                };

                message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

                var authentication = SignRequest(request.Method, rawJson, message.Content.Headers.ContentType.ToString(),
                    $"/api/shipment/{request.Barcode}/dimensions", requestDateTime);

                message.Headers.TryAddWithoutValidation("Authorization", $"{_privateKey}:{authentication}");
                message.Headers.Date = requestDateTime;
                return message;
            });

            return await SendRequest(httpRequest);
        }

        public void UseLiveApi() => _isTestingMode = false;

        public void UseStagingApi() => _isTestingMode = true;
        private string SignRequest(HttpMethod method, string rawJson, string contentType, string endpoint, DateTime dateTime)
        {
            var md5 = MD5.Create();

            var authBuilder = new StringBuilder();
            authBuilder.Append(method).Append("\n");
            authBuilder.Append(BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(rawJson)))
                .Replace("-", "").ToLowerInvariant()).Append("\n");
            authBuilder.Append(contentType).Append("\n");
            authBuilder.Append(dateTime.ToUniversalTime().ToString("r")).Append("\n");
            authBuilder.Append(endpoint);

            var hmacsha1 = new HMACSHA1 { Key = Encoding.UTF8.GetBytes(_secretKey) };
            var signBytes = Encoding.UTF8.GetBytes(authBuilder.ToString());
            var hashResult = hmacsha1.ComputeHash(signBytes);

            return Convert.ToBase64String(hashResult);
        }

        private static async Task<TResponse> SendRequest<TRequest, TResponse>(IHttpRequest<TRequest, ErrorResponse, TResponse> httpRequest)
        {
            await httpRequest.SendAsync(_httpClient).ConfigureAwait(false);

            if (httpRequest.Response != null)
                return httpRequest.Response;


            throw new NotImplementedException();
        }

       
    }
}