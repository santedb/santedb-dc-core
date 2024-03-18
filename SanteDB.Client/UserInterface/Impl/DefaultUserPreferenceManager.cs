﻿/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
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
 * User: fyfej
 * Date: 2024-2-16
 */
using Newtonsoft.Json.Linq;
using SanteDB.Core.Configuration;
using SanteDB.Core.Extensions;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;

namespace SanteDB.Client.UserInterface.Impl
{
    /// <summary>
    /// A user preference manager which stores user preferences in the <see cref="UserEntity"/> profile object
    /// </summary>
    public class DefaultUserPreferenceManager : IUserPreferencesManager
    {
        private readonly IRepositoryService<EntityExtension> m_entityExtensionRepository;
        private readonly IIdentityProviderService m_identityProvider;
        private readonly IRepositoryService<UserEntity> m_userEntityRepository;

        /// <summary>
        /// DI constructor
        /// </summary>
        public DefaultUserPreferenceManager(IRepositoryService<EntityExtension> entityExtensionRepository, IRepositoryService<UserEntity> userEntityRepository, IIdentityProviderService identityProvider)
        {
            this.m_entityExtensionRepository = entityExtensionRepository;
            this.m_identityProvider = identityProvider;
            this.m_userEntityRepository = userEntityRepository;
        }

        /// <inheritdoc/>
        public IEnumerable<AppSettingKeyValuePair> GetUserSettings(string forUser)
        {
            var extension = this.m_entityExtensionRepository.Find(o => (o.SourceEntity as UserEntity).SecurityUser.UserName.ToLowerInvariant() == forUser.ToLowerInvariant() && o.ExtensionTypeKey == ExtensionTypeKeys.UserPreferenceExtension).FirstOrDefault();
            if (extension == null)
            {
                yield break;
            }
            else if (extension.ExtensionValue is JObject dict)
            {
                foreach (var itm in dict)
                {
                    yield return new AppSettingKeyValuePair(itm.Key, itm.Value.Value<String>());
                }
            }
        }

        /// <inheritdoc/>
        public void SetUserSettings(string forUser, IEnumerable<AppSettingKeyValuePair> settings)
        {
            var extension = this.m_entityExtensionRepository.Find(o => (o.SourceEntity as UserEntity).SecurityUser.UserName.ToLowerInvariant() == forUser.ToLowerInvariant() && o.ExtensionTypeKey == ExtensionTypeKeys.UserPreferenceExtension).FirstOrDefault();
            if (extension == null) // No profile so create one
            {
                var ue = this.m_userEntityRepository.Find(o => o.SecurityUser.UserName.ToLowerInvariant() == forUser.ToLowerInvariant()).FirstOrDefault() ??
                    this.m_userEntityRepository.Insert(new UserEntity()
                    {
                        SecurityUserKey = this.m_identityProvider.GetSid(forUser)
                    });

                extension = new EntityExtension(ExtensionTypeKeys.UserPreferenceExtension, typeof(DictionaryExtensionHandler), null)
                {
                    SourceEntityKey = ue.Key
                };
            }

            var settingsDictionary = extension?.ExtensionValue as JObject ?? new JObject();

            // Add settings to dictionary
            foreach (var itm in settings)
            {
                if (settingsDictionary.ContainsKey(itm.Key))
                {
                    settingsDictionary[itm.Key] = itm.Value;
                }
                else
                {
                    settingsDictionary.Add(itm.Key, itm.Value);
                }
            }
            extension.ExtensionValue = settingsDictionary;
            this.m_entityExtensionRepository.Save(extension);
        }
    }
}
