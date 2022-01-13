using Microsoft.TeamFoundation.DistributedTask.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Agent.Sdk.Util
{
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
