using Microsoft.TeamFoundation.DistributedTask.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Agent.Sdk.Util
{
    /// <summary>
    /// Extended secret masker service, that allows to log origins of secrets
    /// </summary>
    public class LoggedSecretMasker : ISecretMasker
    {
        private ISecretMasker _secretMasker;
        private ITraceWriter _trace;

        private void Trace(string msg)
        {
            this._trace?.Info($"[DEBUG INFO]{msg}");
        }

        public LoggedSecretMasker(ISecretMasker secretMasker)
        {

            this._secretMasker = secretMasker;
        }

        public void setTrace(ITraceWriter trace)
        {
            this._trace = trace;
        }

        public void AddValue(string value)
        {
            this.AddValue(value, "Unknown");
        }

        /// <summary>
        /// Overloading of AddValue method with additional logic for logging origin of provided secret
        /// </summary>
        /// <param name="value">Secret to be added</param>
        /// <param name="origin">Origin of the secret</param>
        public void AddValue(string value, string origin)
        {
            this.Trace($"Setting up value for origin: {origin}");
            if (value == null)
            {
                this.Trace($"Value is empty.");
                return;
            }
            this._secretMasker.AddValue(value);
        }

        public void AddRegex(string pattern)
        {
            this._secretMasker.AddRegex(pattern);
        }

        public void AddValueEncoder(ValueEncoder encoder)
        {
            this._secretMasker.AddValueEncoder(encoder);
        }

        public ISecretMasker Clone()
        {
            return this._secretMasker.Clone();
        }

        public string MaskSecrets(string input)
        {
            return this._secretMasker.MaskSecrets(input);
        }
    }
}
