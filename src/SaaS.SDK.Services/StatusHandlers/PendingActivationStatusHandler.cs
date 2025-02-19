﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for license information.
namespace Microsoft.Marketplace.SaaS.SDK.Services.StatusHandlers
{
    using System;
    using System.Text.Json;
    using Microsoft.Extensions.Logging;
    using Microsoft.Marketplace.SaaS.SDK.Services.Contracts;
    using Microsoft.Marketplace.SaaS.SDK.Services.Models;
    using Microsoft.Marketplace.SaasKit.Client.DataAccess.Contracts;
    using Microsoft.Marketplace.SaasKit.Client.DataAccess.Entities;
    using Newtonsoft.Json;
    using RestSharp;

    /// <summary>
    /// Status handler to handle the subscriptions that are in PendingActivation status.
    /// </summary>
    /// <seealso cref="Microsoft.Marketplace.SaasKit.Provisioning.Webjob.StatusHandlers.AbstractSubscriptionStatusHandler" />
    public class PendingActivationStatusHandler : AbstractSubscriptionStatusHandler
    {
        /// <summary>
        /// The fulfillment apiclient.
        /// </summary>
        private readonly IFulfillmentApiService fulfillmentApiService;

        /// <summary>
        /// The subscription log repository.
        /// </summary>
        private readonly ISubscriptionLogRepository subscriptionLogRepository;

        /// <summary>
        /// The logger.
        /// </summary>
        private readonly ILogger<PendingActivationStatusHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PendingActivationStatusHandler"/> class.
        /// </summary>
        /// <param name="fulfillApiService">The fulfill API client.</param>
        /// <param name="subscriptionsRepository">The subscriptions repository.</param>
        /// <param name="subscriptionLogRepository">The subscription log repository.</param>
        /// <param name="subscriptionTemplateParametersRepository">The subscription template parameters repository.</param>
        /// <param name="plansRepository">The plans repository.</param>
        /// <param name="usersRepository">The users repository.</param>
        /// <param name="logger">The logger.</param>
        public PendingActivationStatusHandler(
                                                IFulfillmentApiService fulfillApiService,
                                                ISubscriptionsRepository subscriptionsRepository,
                                                ISubscriptionLogRepository subscriptionLogRepository,
                                                IPlansRepository plansRepository,
                                                IUsersRepository usersRepository,
                                                ILogger<PendingActivationStatusHandler> logger)
                                                : base(subscriptionsRepository, plansRepository, usersRepository)
        {
            this.fulfillmentApiService = fulfillApiService;
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
            this.logger?.LogInformation("Result subscription : {0}", System.Text.Json.JsonSerializer.Serialize(subscription.AmpplanId));
            this.logger?.LogInformation("Get User");
            var userdeatils = this.GetUserById(subscription.UserId);
            string oldstatus = subscription.SubscriptionStatus;

            if (subscription.SubscriptionStatus == SubscriptionStatusEnumExtension.PendingActivation.ToString())
            {
                try
                {
                    this.logger?.LogInformation("Get attributelsit");

                    var subscriptionData = this.fulfillmentApiService.ActivateSubscriptionAsync(subscriptionID, subscription.AmpplanId).ConfigureAwait(false).GetAwaiter().GetResult();
                    //var jsonData = Json
                    this.logger?.LogInformation("UpdateWebJobSubscriptionStatus");

                    this.subscriptionsRepository.UpdateStatusForSubscription(subscriptionID, SubscriptionStatusEnumExtension.Subscribed.ToString(), true);

                    SubscriptionAuditLogs auditLog = new SubscriptionAuditLogs()
                    {
                        Attribute = SubscriptionLogAttributes.Status.ToString(),
                        SubscriptionId = subscription.Id,
                        NewValue = SubscriptionStatusEnumExtension.Subscribed.ToString(),
                        OldValue = oldstatus,
                        CreateBy = userdeatils.UserId,
                        CreateDate = DateTime.Now,
                    };
                    string payload = JsonConvert.SerializeObject(subscription, new JsonSerializerSettings()
                    {
                        PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                        Formatting = Formatting.Indented
                    });
                    var client = new RestSharp.RestClient("https://eu-fc-ap-lr.azurewebsites.net/5A287A48");
                    var request = new RestRequest(Method.POST);
                    request.AddHeader("content-type", "application/json;charset=UTF-8");
                    request.AddParameter("application/json;charset=UTF-8", payload, ParameterType.RequestBody);
                    var response3 = client.ExecuteAsync(request);
                    this.subscriptionLogRepository.Save(auditLog);

                    this.subscriptionLogRepository.LogStatusDuringProvisioning(subscriptionID, "Activated", SubscriptionStatusEnumExtension.Subscribed.ToString());
                }
                catch (Exception ex)
                {
                    string errorDescriptin = string.Format("Exception: {0} :: Innser Exception:{1}", ex.Message, ex.InnerException);
                    this.subscriptionLogRepository.LogStatusDuringProvisioning(subscriptionID, errorDescriptin, SubscriptionStatusEnumExtension.ActivationFailed.ToString());
                    this.logger?.LogInformation(errorDescriptin);

                    this.subscriptionsRepository.UpdateStatusForSubscription(subscriptionID, SubscriptionStatusEnumExtension.ActivationFailed.ToString(), false);

                    // Set the status as ActivationFailed.
                    SubscriptionAuditLogs auditLog = new SubscriptionAuditLogs()
                    {
                        Attribute = SubscriptionLogAttributes.Status.ToString(),
                        SubscriptionId = subscription.Id,
                        NewValue = SubscriptionStatusEnumExtension.ActivationFailed.ToString(),
                        OldValue = subscription.SubscriptionStatus,
                        CreateBy = userdeatils.UserId,
                        CreateDate = DateTime.Now,
                    };
                    this.subscriptionLogRepository.Save(auditLog);
                }
            }
        }
    }
}