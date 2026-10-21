#if NET8_0
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.WebServices;

namespace NotaCarioca.Testes
{
    [TestClass]
    public class NfseWsIntegrationTests
    {
        private WebApplicationFactory<Program> _factory;
        private HttpClient _client;

        [TestInitialize]
        public void Inicializar()
        {
            _factory = new WebApplicationFactory<Program>();
            _client = _factory.CreateClient();
        }

        [TestCleanup]
        public void Limpar()
        {
            _client.Dispose();
            _factory.Dispose();
        }

        [TestMethod]
        public async Task GetServico_RetornaPaginaInicial()
        {
            HttpResponseMessage response = await _client.GetAsync("/NfseWS.asmx");
            string content = await response.Content.ReadAsStringAsync();

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            StringAssert.Contains(content, "NfseWS");
            StringAssert.Contains(content, "NfseWS.asmx?wsdl");
        }

        [TestMethod]
        public async Task GetWsdl_RetornaDocumentoWsdl()
        {
            HttpResponseMessage response = await _client.GetAsync("/NfseWS.asmx?wsdl");
            string content = await response.Content.ReadAsStringAsync();

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            StringAssert.Contains(response.Content.Headers.ContentType.MediaType, "text/xml");
            StringAssert.Contains(content, "<wsdl:definitions");
            StringAssert.Contains(content, "RecepcionarLoteRps");
        }

        [TestMethod]
        public async Task PostSoap_OperacaoDesconhecida_RetornaFalhaSoap()
        {
            const string envelope = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
                "<soap:Body><inputXML /></soap:Body></soap:Envelope>";

            using (var request = new HttpRequestMessage(HttpMethod.Post, "/NfseWS.asmx"))
            {
                request.Headers.Add("SOAPAction", "\"http://notacarioca.rio.gov.br/OperacaoInexistente\"");
                request.Content = new StringContent(envelope, System.Text.Encoding.UTF8, "text/xml");

                HttpResponseMessage response = await _client.SendAsync(request);
                string content = await response.Content.ReadAsStringAsync();

                Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
                StringAssert.Contains(content, "soap:Fault");
                StringAssert.Contains(content, "Operação desconhecida: OperacaoInexistente");
            }
        }
    }
}
#endif
