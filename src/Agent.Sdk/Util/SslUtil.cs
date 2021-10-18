using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Net.Http;
using Agent.Sdk;

namespace Microsoft.VisualStudio.Services.Agent.Util
{
    public sealed class SslUtil
    {
        private ITraceWriter Trace { get; set; }
        
        public SslUtil(ITraceWriter Trace)
        {
            this.Trace = Trace;
        }

        public bool ServerCertificateCustomValidation(HttpRequestMessage requestMessage, X509Certificate2 certificate, X509Chain chain, SslPolicyErrors sslErrors)
        {
            LoggingRequestDiagnosticData(requestMessage, certificate, chain, sslErrors);
            return sslErrors == SslPolicyErrors.None;
        }

        private void LoggingRequestDiagnosticData(HttpRequestMessage requestMessage, X509Certificate2 certificate, X509Chain chain, SslPolicyErrors sslErrors)
        {
            if (this.Trace != null)
            {
                Trace.Info($"Requested URI: {requestMessage.RequestUri}");
                Trace.Info($"Requested Headers: {requestMessage.Headers}");
                Trace.Info($"Effective date: {certificate.GetEffectiveDateString()}");
                Trace.Info($"Expiration date: {certificate.GetExpirationDateString()}");
                Trace.Info($"Issuer: {certificate.Issuer}");
                Trace.Info($"Subject: {certificate.Subject}");
                Trace.Info($"Errors: {sslErrors}");
            }
        }
    }
}
