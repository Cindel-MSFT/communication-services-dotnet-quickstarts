﻿using Azure.Communication.Identity;

namespace CallAutomation_TryPstnBackendService
{
    public static class WebApplicationExtension
    {
        public async static Task<string> ProvisionAzureCommunicationServicesIdentity(this WebApplication app, string connectionString)
        {
            var client = new CommunicationIdentityClient(connectionString);
            var user = await client.CreateUserAsync().ConfigureAwait(false);
            return user.Value.Id;
        }
    }
}