/*
 * Copyright 2015-2018 Mohawk College of Applied Arts and Technology
 *
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you 
 * may not use this file except in compliance with the License. You may 
 * obtain a copy of the License at 
 * 
 * http://www.apache.org/licenses/LICENSE-2.0 
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the 
 * License for the specific language governing permissions and limitations under 
 * the License.
 * 
 * User: justin
 * Date: 2018-6-28
 */
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Services;
using System;
using System.Linq;

namespace SanteDB.DisconnectedClient.Core.Configuration.Data.Migrations
{
    /// <summary>
    /// Subscribe to primary location update
    /// </summary>
    class SubscribePrimaryLocationUpdate : IDbMigration
    {
        /// <summary>
        /// Description
        /// </summary>
        public string Description
        {
            get
            {
                return "Updates subscriptions to include all data the location authored";
            }
        }

        /// <summary>
        /// Gets id of the update
        /// </summary>
        public string Id
        {
            get
            {
                return "update-subscription-primary-location";
            }
        }

        /// <summary>
        /// Install the migration
        /// </summary>
        public bool Install()
        {

            var syncSection = ApplicationContext.Current.Configuration.GetSection<SynchronizationConfigurationSection>();

            // Un-subscribe to SubstanceAdministration
            var actTypes = new Type[] { typeof(SubstanceAdministration), typeof(QuantityObservation), typeof(TextObservation), typeof(CodedObservation), typeof(Procedure) };
            syncSection.SynchronizationResources.RemoveAll(o => actTypes.Contains(o.ResourceType));
            syncSection.SynchronizationResources.RemoveAll(o => o.ResourceType == typeof(Patient));

            // Re-add substance administrations
            syncSection.SynchronizationResources.AddRange(actTypes.Select(t => new SynchronizationResource()
            {
                Always = false,
                Filters = syncSection.Facilities.SelectMany(o => new String[] {
                    $"participation[Location|EntryLocation].player=!{o}&participation[RecordTarget].player.relationship[DedicatedServiceDeliveryLocation|IncidentalServiceDeliveryLocation|ServiceDeliveryLocation].target={o}",
                    $"participation[Location|InformationRecipient|EntryLocation].player={o}"
                }).ToList(),
                ResourceType = t,
                Triggers = SynchronizationPullTriggerType.Always
            }));

            // Add patients that are mine and those that are involved in historical acts that are not mine
            syncSection.SynchronizationResources.Add(new SynchronizationResource()
            {
                Always = false,
                Filters = syncSection.Facilities.SelectMany(o => new String[] {
                    $"relationship[ServiceDeliveryLocation|DedicatedServiceDeliveryLocation|IncidentalServiceDeliveryLocation].target={o}",
                    $"participation[RecordTarget].source.participation[Location].player={o}&relationship[ServiceDeliveryLocation|DedicatedServiceDeliveryLocation|IncidentalServiceDeliveryLocation].target=!{o}"
                }).ToList(),
                ResourceType = typeof(Patient),
                Triggers = SynchronizationPullTriggerType.OnStart | SynchronizationPullTriggerType.OnNetworkChange
            });

            // Persons who are related to my patients
            syncSection.SynchronizationResources.Add(new SynchronizationResource()
            {
                Always = false,
                Filters = syncSection.Facilities.SelectMany(o => new String[] {
                    $"classConcept=9de2a846-ddf2-4ebc-902e-84508c5089ea&relationship.source.classConcept=bacd9c6f-3fa9-481e-9636-37457962804d&relationship.source.relationship[DedicatedServiceDeliveryLocation|IncidentalServiceDeliveryLocation|ServiceDeliveryLocation].target={o}"
                }).ToList(),
                ResourceType = typeof(Person),
                Triggers = SynchronizationPullTriggerType.Always
            });

            foreach (var ss in syncSection.SynchronizationResources.Where(o => o.Filters.Any(f => f.Contains("relationship[DedicatedServiceDeliveryLocation|IncidentalServiceDeliveryLocation]"))))
            {
                ss.Filters = ss.Filters.Select(o => o.Replace("relationship[DedicatedServiceDeliveryLocation|IncidentalServiceDeliveryLocation]", "relationship[DedicatedServiceDeliveryLocation|IncidentalServiceDeliveryLocation|ServiceDeliveryLocation]")).ToList();
            }

            return true;
        }
    }
}
