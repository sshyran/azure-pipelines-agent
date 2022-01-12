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
            this._trace?.Info($"========================[DEBUG INFO]========================");
        }

        public void AddRegex(string pattern)
        {
            this.AddRegex(pattern, "Unknown");
        }

        public void AddRegex(string pattern, string origin)
        {
            this.Trace($"Setting up regex for origin: {origin}.");
            if (pattern == null)
            {
                this.Trace($"Pattern is empty.");
                return;
            }

            this._secretMasker.AddRegex(pattern);
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

        public void AddValueEncoder(ValueEncoder encoder, string origin)
        {
            this.Trace($"Setting up value for origin: {origin}");
            if (encoder == null)
            {
                this.Trace($"Encoder is empty.");
                return;
            }
            this._secretMasker.AddValueEncoder(encoder);
        }

        public void AddValueEncoder(ValueEncoder encoder)
        {
            this.AddValueEncoder(encoder, "Unknown");
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
