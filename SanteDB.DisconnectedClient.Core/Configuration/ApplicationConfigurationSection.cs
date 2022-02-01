/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2021-8-27
 */
using Newtonsoft.Json;
using SanteDB.Core.Configuration;
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.Configuration
{

    /// <summary>
    /// Represents basic application configuration
    /// </summary>
    [XmlType(nameof(ApplicationConfigurationSection), Namespace = "http://santedb.org/mobile/configuration")]
    [JsonObject]
    public class ApplicationConfigurationSection : IConfigurationSection
    {
        ///// <summary>
        ///// Sets the services.
        ///// </summary>
        ///// <value>The services.</value>
        //[XmlIgnore, JsonIgnore]
        //public List<Object> Services {
        //	get {
        //		if (this.m_services == null) {
        //			this.m_services = new List<object> ();
        //			foreach (var itm in this.ServiceTypes) {
        //				Type t = Type.GetType (itm);
        //                      if (t == null)
        //                          throw new KeyNotFoundException(itm);
        //				this.m_services.Add (Activator.CreateInstance (t));
        //			}
        //		}
        //		return this.m_services;
        //	}
        //}

        /// <summary>
        /// The location of the directory where user preferences are stored
        /// </summary>
        /// <value>The user preference dir.</value>
        [XmlElement("userPrefDir")]
        [JsonProperty("userPrefDir")]
        public string UserPrefDir
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the style.
        /// </summary>
        /// <value>The style.</value>
        [XmlElement("style")]
        [JsonProperty("style")]
        public StyleSchemeType Style
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the cache configuration
        /// </summary>
        [XmlElement("caching")]
        public CacheConfiguration Cache { get; set; }





    }

    /// <summary>
    /// Cache configuration
    /// </summary>
    [XmlType(nameof(CacheConfiguration), Namespace = "http://santedb.org/mobile/configuration")]
    [JsonObject]
    public class CacheConfiguration
    {
        /// <summary>
        /// Max age
        /// </summary>
        [XmlAttribute("maxAge")]
        public long MaxAge { get; set; }

        /// <summary>
        /// Maximum time that can pass without cleaning
        /// </summary>
        [XmlAttribute("maxDirty")]
        public long MaxDirtyAge { get; set; }

        /// <summary>
        /// Maximum time that can pass withut reducing pressure
        /// </summary>
        [XmlAttribute("maxPressure")]
        public long MaxPressureAge { get; set; }

        /// <summary>
        /// Maximum size
        /// </summary>
        [XmlAttribute("maxSize")]
        public int MaxSize { get; set; }
    }


    /// <summary>
    /// Style scheme type
    /// </summary>
    [XmlType(nameof(StyleSchemeType), Namespace = "http://santedb.org/mobile/configuration")]
    public enum StyleSchemeType
    {
        Dark,
        Light
    }

}

