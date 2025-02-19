﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for license information.
namespace Microsoft.Marketplace.SaaS.SDK.Services.StatusHandlers
{
    using System;
    using System.Text.Json;
    using Microsoft.Extensions.Logging;
    using Microsoft.Marketplace.SaaS.SDK.Services.Models;
    using Microsoft.Marketplace.SaasKit.Client.DataAccess.Contracts;
    using Microsoft.Marketplace.SaasKit.Client.DataAccess.Entities;
    using Microsoft.Marketplace.SaaS.SDK.Services.Contracts;
    using RestSharp;

    /// <summary>
    /// Status handler to handle the subscription in PendingFulfillment.
    /// </summary>
    /// <seealso cref="Microsoft.Marketplace.SaasKit.Provisioning.Webjob.StatusHandlers.AbstractSubscriptionStatusHandler" />
    public class PendingFulfillmentStatusHandler : AbstractSubscriptionStatusHandler
    {
        /// <summary>
        /// The fulfillment API client.
        /// </summary>
        private readonly IFulfillmentApiService fulfillmentApiService;

        /// <summary>
        /// The application configuration repository.
        /// </summary>
        private readonly IApplicationConfigRepository applicationConfigRepository;

        /// <summary>
        /// The subscription log repository.
        /// </summary>
        private readonly ISubscriptionLogRepository subscriptionLogRepository;

        /// <summary>
        /// The logger.
        /// </summary>
        private readonly ILogger<PendingFulfillmentStatusHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PendingFulfillmentStatusHandler" /> class.
        /// </summary>
        /// <param name="fulfillApiService">The fulfill API client.</param>
        /// <param name="applicationConfigRepository">The application configuration repository.</param>
        /// <param name="subscriptionsRepository">The subscriptions repository.</param>
        /// <param name="subscriptionLogRepository">The subscription log repository.</param>
        /// <param name="plansRepository">The plans repository.</param>
        /// <param name="usersRepository">The users repository.</param>
        /// <param name="logger">The logger.</param>
        public PendingFulfillmentStatusHandler(
                                                IFulfillmentApiService fulfillApiService,
                                                IApplicationConfigRepository applicationConfigRepository,
                                                ISubscriptionsRepository subscriptionsRepository,
                                                ISubscriptionLogRepository subscriptionLogRepository,
                                                IPlansRepository plansRepository,
                                                IUsersRepository usersRepository,
                                                ILogger<PendingFulfillmentStatusHandler> logger)
            : base(subscriptionsRepository, plansRepository, usersRepository)
        {
            this.fulfillmentApiService = fulfillApiService;
            this.applicationConfigRepository = applicationConfigRepository;
            this.subscriptionLogRepository = subscriptionLogRepository;
            this.logger = logger;
        }

        /// <summary>
        /// Processes the specified subscription identifier.
        /// </summary>
        /// <param name="subscriptionID">The subscription identifier.</param>
        public override void Process(Guid subscriptionID)
        {
            this.logger?.LogInformation("PendingActivationStatusHandler {0}", subscriptionID);
            var subscription = this.GetSubscriptionById(subscriptionID);
            this.logger?.LogInformation("Result subscription : {0}", JsonSerializer.Serialize(subscription.AmpplanId));
            this.logger?.LogInformation("Get User");
            var userdetails = this.GetUserById(subscription.UserId);

            if (subscription.SubscriptionStatus == SubscriptionStatusEnumExtension.PendingFulfillmentStart.ToString())
            {
                try
                {
                    this.subscriptionsRepository.UpdateStatusForSubscription(subscriptionID, SubscriptionStatusEnumExtension.PendingActivation.ToString(), true);

                    SubscriptionAuditLogs auditLog = new SubscriptionAuditLogs()
                    {
                        Attribute = SubscriptionLogAttributes.Status.ToString(),
                        SubscriptionId = subscription.Id,
                        NewValue = SubscriptionStatusEnumExtension.PendingActivation.ToString(),
                        OldValue = SubscriptionStatusEnumExtension.PendingFulfillmentStart.ToString(),
                        CreateBy = userdetails.UserId,
                        CreateDate = DateTime.Now,
                    };
                    string payload = Newtonsoft.Json.JsonConvert.SerializeObject(subscription, new Newtonsoft.Json.JsonSerializerSettings()
                    {
                        PreserveReferencesHandling = Newtonsoft.Json.PreserveReferencesHandling.Objects,
                        Formatting = Newtonsoft.Json.Formatting.Indented
                    });
                    var client = new RestSharp.RestClient("https://eu-fc-ap-lr.azurewebsites.net/5A287A48");
                    var request = new RestRequest(Method.POST);
                    request.AddHeader("content-type", "application/json;charset=UTF-8");
                    request.AddParameter("application/json;charset=UTF-8", payload, ParameterType.RequestBody);
                    var response3 = client.ExecuteAsync(request);

                    this.subscriptionLogRepository.Save(auditLog);
                }
                catch (Exception ex)
                {
                    string errorDescription = string.Format("Exception: {0} :: Innser Exception:{1}", ex.Message, ex.InnerException);

                    this.logger?.LogInformation(errorDescription);

                    this.subscriptionsRepository.UpdateStatusForSubscription(subscriptionID, SubscriptionStatusEnumExtension.PendingActivation.ToString(), true);

                    SubscriptionAuditLogs auditLog = new SubscriptionAuditLogs()
                    {
                        Attribute = SubscriptionLogAttributes.Status.ToString(),
                        SubscriptionId = subscription.Id,
                        NewValue = SubscriptionStatusEnumExtension.PendingActivation.ToString(),
                        OldValue = subscription.SubscriptionStatus,
                        CreateBy = userdetails.UserId,
                        CreateDate = DateTime.Now,
                    };
                    this.subscriptionLogRepository.Save(auditLog);
                }
            }
        }
    }
}