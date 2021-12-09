using Microsoft.TeamFoundation.DistributedTask.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Agent.Sdk.Util
{
    public class LoggedSecretMasker: ISecretMasker
    {
        private ISecretMasker _secretMasker;
        private ITraceWriter _trace;

        private void Trace(string msg)
        {
            if(this._trace != null)
            {
                this._trace.Info($"[DEBUG INFO]{msg}");
            }
        }

        private void TraceSpecialCharacters(string value)
        {
            var regex = new Regex("[^A-Za-z0-9]");
            this.Trace("Looking for special characters");
            foreach (Match match in regex.Matches(value))
            {
                this.Trace(String.Format("Found '{0}' at position {1}", match.Value, match.Index));
            }
        }

        public LoggedSecretMasker(ISecretMasker secretMasker)
        {
            this._secretMasker = secretMasker;
        }

        public void setTrace(ITraceWriter trace)
        {
            this._trace = trace;
        }

        public void AddRegex(string pattern)
        {
            this.AddRegex(pattern, "Unknown");
        }

        public void AddRegex(string pattern, string origin)
        {
            var regex = new Regex(pattern);
            var match = regex.Match("1");
            this.Trace($"Setting up regex for origin: {origin}.");
            this.Trace($"Length: {pattern.Length}.");
            this.TraceSpecialCharacters(pattern);

            if (match.Success)
            {
                this.Trace("Regex matches 1");
            }

            if (pattern == "1")
            {
                this.Trace("Pattern is equal to '1'");
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
            this.Trace($"Length: {value.Length}.");
            this.TraceSpecialCharacters(value);
            this._secretMasker.AddValue(value);
        }
        public void AddValueEncoder(ValueEncoder encoder, string origin)
        {
            this.Trace($"Setting up value for origin: {origin}");
            this.Trace($"Length: {encoder.ToString().Length}.");
            this.TraceSpecialCharacters(encoder.ToString());
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
