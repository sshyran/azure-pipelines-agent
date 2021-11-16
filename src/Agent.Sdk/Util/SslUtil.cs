using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Net.Http;
using Agent.Sdk;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.VisualStudio.Services.Agent.Util
{
    public sealed class SslUtil
    {
        private ITraceWriter Trace { get; set; }

        public SslUtil(ITraceWriter Trace)
        {
            this.Trace = Trace;
        }

        /// <summary>
        /// Implementation of the custom callback function that writes SSL-related data from the web request to the agent's logs
        /// </summary>
        /// <returns>Returns `true` if web request was successful, otherwise `false`</returns>
        public bool RequestStatusCustomValidation(HttpRequestMessage requestMessage, X509Certificate2 certificate, X509Chain chain, SslPolicyErrors sslErrors)
        {
            bool isRequestSuccessful = (sslErrors == SslPolicyErrors.None);

            if (!isRequestSuccessful)
            {
                LoggingRequestDiagnosticData(requestMessage, certificate, chain, sslErrors);
            }

            return isRequestSuccessful;
        }

        /// <summary>
        /// Writes SSL related data to agent logs
        /// </summary>
        private void LoggingRequestDiagnosticData(HttpRequestMessage requestMessage, X509Certificate2 certificate, X509Chain chain, SslPolicyErrors sslErrors)
        {
            string diagInfo = "Diagnostic data for request:\n";

            if (this.Trace != null)
            {
                diagInfo += SslDiagnosticDataProvider.ResolveSslPolicyErrorMessage(sslErrors);
                diagInfo += SslDiagnosticDataProvider.GetRequestMessageData(requestMessage);
                diagInfo += SslDiagnosticDataProvider.GetCertificateData(certificate);

                Trace?.Info(diagInfo);
            }
        }
    }

    public static class SslDiagnosticDataProvider
    {
        /// <summary>
        /// A predefined list of headers to be extracted from request for diagnostic data. 
        /// </summary>
        private static string[] requiredRequestHeaders = new[]
        {
            "X-TFS-Session",
            "X-VSS-E2EID",
            "User-Agent"
        };

        public static string GetRequestMessageData(HttpRequestMessage requestMessage)
        {
            // Getting general information about request
            string requestDiagInfoHeader = "HttpRequest";
            string diagInfo = string.Empty;

            if (requestMessage is null)
            {
                return $"{requestDiagInfoHeader} data is empty";
            }

            var requestDiagInfo = new List<KeyValuePair<string, string>>();

            var requestedUri = requestMessage?.RequestUri.ToString();
            var methodType = requestMessage?.Method.ToString();
            requestDiagInfo.Add(new KeyValuePair<string, string>("Requested URI", requestedUri));
            requestDiagInfo.Add(new KeyValuePair<string, string>("Request method", methodType));

            diagInfo = GetFormattedData(requestDiagInfoHeader, requestDiagInfo);

            // Getting informantion from headers
            var requestHeaders = requestMessage?.Headers;

            if (requestHeaders is null)
            {
                return diagInfo;
            }

            string headersDiagInfoHeader = "HttpRequestHeaders";

            var headersDiagInfo = new List<KeyValuePair<string, string>>();
            foreach (var headerKey in requiredRequestHeaders)
            {
                IEnumerable<string> headerValues;

                if (requestHeaders.TryGetValues(headerKey, out headerValues))
                {
                    var headerValue = string.Join(", ", headerValues.ToArray());
                    if (headerValue != null)
                    {
                        headersDiagInfo.Add(new KeyValuePair<string, string>(headerKey, headerValue.ToString()));
                    }
                }
            }

            diagInfo += GetFormattedData(headersDiagInfoHeader, headersDiagInfo);

            return diagInfo;
        }

        public static string GetCertificateData(X509Certificate2 certificate)
        {
            string diagInfoHeader = "Certificate";
            var diagInfo = new List<KeyValuePair<string, string>>();

            if (certificate is null)
            {
                return $"{diagInfoHeader} data is empty";
            }

            diagInfo.Add(new KeyValuePair<string, string>("Effective date", certificate?.GetEffectiveDateString()));
            diagInfo.Add(new KeyValuePair<string, string>("Expiration date", certificate?.GetExpirationDateString()));
            diagInfo.Add(new KeyValuePair<string, string>("Issuer", certificate?.Issuer));
            diagInfo.Add(new KeyValuePair<string, string>("Subject", certificate?.Subject));

            return GetFormattedData(diagInfoHeader, diagInfo);
        }

        public static string ResolveSslPolicyErrorMessage(SslPolicyErrors sslErrors)
        {
            string sslErrorsStatus = string.Empty;

            switch (sslErrors)
            {
                case SslPolicyErrors.None:
                    sslErrorsStatus = "No SSL policy errors";
                    break;

                case SslPolicyErrors.RemoteCertificateChainErrors:
                    sslErrorsStatus = "ChainStatus has returned a non empty array";
                    break;

                case SslPolicyErrors.RemoteCertificateNameMismatch:
                    sslErrorsStatus = "Certificate name mismatch";
                    break;

                case SslPolicyErrors.RemoteCertificateNotAvailable:
                    sslErrorsStatus = "Certificate not available";
                    break;

                default:
                    sslErrorsStatus = sslErrors.ToString();
                    break;
            }

            string diagInfo = $"SSL Policy Errors: {sslErrorsStatus}\n";

            return diagInfo;
        }


        /// <summary>
        /// Get diagnostic data as formatted text.
        /// </summary>
        private static string GetFormattedData(string diagInfoHeader, List<KeyValuePair<string, string>> diagInfo)
        {
            string formattedData = $"[{diagInfoHeader}]\n";

            foreach (var record in diagInfo)
            {
                formattedData += $"{record.Key}: {record.Value}\n";
            }

            return formattedData;
        }
    }
}
