// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.Services.Agent;

namespace Agent.Sdk.Util
{
    public class ExceptionsUtil
    {
        public static void HandleAggregateException(AggregateException e)
        {
            Trace.Error("One or several exceptions have been occurred.");

            int i = 0;
            foreach (var ex in ((AggregateException)e).Flatten().InnerExceptions)
            {
                i++;
                Trace.Error($"InnerException #{i}");
                Trace.Error(ex);
            }
        }
    }
}
