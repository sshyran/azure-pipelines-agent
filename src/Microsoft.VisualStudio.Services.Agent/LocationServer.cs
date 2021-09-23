// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.Location.Client;
using Microsoft.VisualStudio.Services.Location;
using Microsoft.VisualStudio.Services.Agent.Util;

namespace Microsoft.VisualStudio.Services.Agent
{
    [ServiceLocator(Default = typeof(LocationServer))]
    public interface ILocationServer : IAgentService
    {
        Task ConnectAsync(VssConnection jobConnection);

        Task<ConnectionData> GetConnectionDataAsync();
    }

    public sealed class LocationServer : AgentService, ILocationServer
    {
        private bool _hasConnection;
        private VssConnection _connection;
        private LocationHttpClient _locationClient;

        public async Task ConnectAsync(VssConnection jobConnection)
        {
            ArgUtil.NotNull(jobConnection, nameof(jobConnection));

            try
            {
                int attemptsNumber = 100;
                while (true)
                {
                    try
                    {
                        await jobConnection.ConnectAsync();
                        break;
                    }
                    catch (Exception)
                    {
                        attemptsNumber--;
                        Trace.Info($"Unable to connect to {jobConnection.Uri}, attempts left: {attemptsNumber}");
                        if (attemptsNumber == 0)
                        {
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.Info($"Unable to connect to {jobConnection.Uri}.");
                Trace.Error(ex);
                throw;
            }

            _locationClient = jobConnection.GetClient<LocationHttpClient>();
            _connection = jobConnection;
            _hasConnection = true;
        }

        private void CheckConnection()
        {
            if (!_hasConnection)
            {
                throw new InvalidOperationException("SetConnection");
            }
        }

        public async Task<ConnectionData> GetConnectionDataAsync()
        {
            CheckConnection();
            return await _locationClient.GetConnectionDataAsync(ConnectOptions.None, 0);
        }
    }
}