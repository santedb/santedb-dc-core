using SanteDB.Core.Diagnostics;
using SanteDB.Core.Event;
using SanteDB.Core.Http;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.Query;
using SanteDB.Core.PubSub;
using SanteDB.DisconnectedClient.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace SanteDB.DisconnectedClient.Services.Remote
{
    /// <summary>
    /// A pub-sub manager which interacts with the remote pub-sub services
    /// </summary>
    public class RemotePubSubManager : IPubSubManagerService
    {
        /// <summary>
        /// Gets the pub/sub name
        /// </summary>
        public string ServiceName => "Remote Pub-Sub Management Service";

        /// <summary>
        /// Fired when subscribing
        /// </summary>
        public event EventHandler<DataPersistingEventArgs<PubSubSubscriptionDefinition>> Subscribing;

        /// <summary>
        /// Fired after subscription
        /// </summary>
        public event EventHandler<DataPersistedEventArgs<PubSubSubscriptionDefinition>> Subscribed;

        /// <summary>
        /// Fired when un-subscribing
        /// </summary>
        public event EventHandler<DataPersistingEventArgs<PubSubSubscriptionDefinition>> UnSubscribing;

        /// <summary>
        /// Fired when subscribed
        /// </summary>
        public event EventHandler<DataPersistedEventArgs<PubSubSubscriptionDefinition>> UnSubscribed;

        /// <summary>
        /// Fired when activating
        /// </summary>
        public event EventHandler<DataPersistingEventArgs<PubSubSubscriptionDefinition>> Activating;

        /// <summary>
        /// Fired when de-activating
        /// </summary>
        public event EventHandler<DataPersistingEventArgs<PubSubSubscriptionDefinition>> DeActivating;

        /// <summary>
        /// Fired when activated
        /// </summary>
        public event EventHandler<DataPersistedEventArgs<PubSubSubscriptionDefinition>> Activated;

        /// <summary>
        /// Fired when de-activated
        /// </summary>
        public event EventHandler<DataPersistedEventArgs<PubSubSubscriptionDefinition>> DeActivated;

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(RemoteJobManager));

        /// <summary>
        /// Gets the rest client
        /// </summary>
        /// <returns></returns>
        private IRestClient GetRestClient()
        {
            var retVal = ApplicationContext.Current.GetRestClient("ami");
            return retVal;
        }

        /// <summary>
        /// Activate a subscription on the remote server
        /// </summary>
        public PubSubSubscriptionDefinition ActivateSubscription(Guid key, bool isActive)
        {
            try
            {
                var sub = this.GetSubscription(key);
                using (var client = this.GetRestClient())
                {
                    sub.IsActive = isActive;
                    return client.Put<PubSubSubscriptionDefinition, PubSubSubscriptionDefinition>($"PubSubSubscription/{key}", sub);
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error activating subscription {0} - {1}", key, e);
                throw new Exception($"Error activating {key}", e);
            }
        }

        /// <summary>
        /// Find channels
        /// </summary>
        public IEnumerable<PubSubChannelDefinition> FindChannel(Expression<Func<PubSubChannelDefinition, bool>> filter)
        {
            return this.FindChannel(filter, 0, 10, out int _);
        }

        /// <summary>
        /// Find channel
        /// </summary>
        public IEnumerable<PubSubChannelDefinition> FindChannel(Expression<Func<PubSubChannelDefinition, bool>> filter, int offset, int count, out int totalResults)
        {
            try
            {
                using (var client = this.GetRestClient())
                {
                    var qry = QueryExpressionBuilder.BuildQuery(filter);
                    var result = client.Get<AmiCollection>($"PubSubChannel", qry.ToArray());
                    totalResults = result.Size;
                    return result.CollectionItem.OfType<PubSubChannelDefinition>();
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error querying channels - {0}", e);
                throw new Exception($"Error querying channels", e);
            }
        }

        /// <summary>
        /// Find subscriptions
        /// </summary>
        public IEnumerable<PubSubSubscriptionDefinition> FindSubscription(Expression<Func<PubSubSubscriptionDefinition, bool>> filter)
        {
            return this.FindSubscription(filter, 0, 10, out int _);
        }

        /// <summary>
        /// Find subscriptions
        /// </summary>
        public IEnumerable<PubSubSubscriptionDefinition> FindSubscription(Expression<Func<PubSubSubscriptionDefinition, bool>> filter, int offset, int count, out int totalResults)
        {
            try
            {
                using (var client = this.GetRestClient())
                {
                    var qry = QueryExpressionBuilder.BuildQuery(filter);
                    var result = client.Get<AmiCollection>($"PubSubSubscription", qry.ToArray());
                    totalResults = result.Size;
                    return result.CollectionItem.OfType<PubSubSubscriptionDefinition>();
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error querying subscriptions - {0}", e);
                throw new Exception($"Error querying subscriptions", e);
            }
        }

        /// <summary>
        /// Get the channel
        /// </summary>
        public PubSubChannelDefinition GetChannel(Guid id)
        {
            try
            {
                using (var client = this.GetRestClient())
                {
                    return client.Get<PubSubChannelDefinition>($"PubSubChannel/{id}");
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error getting channel {0} - {1}", id, e);
                throw new Exception($"Error getting channel {id}", e);
            }
        }

        /// <summary>
        /// Get subscriptions
        /// </summary>
        public PubSubSubscriptionDefinition GetSubscription(Guid id)
        {
            try
            {
                using (var client = this.GetRestClient())
                {
                    return client.Get<PubSubSubscriptionDefinition>($"PubSubSubscription/{id}");
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error getting subscription {0} - {1}", id, e);
                throw new Exception($"Error getting subscription {id}", e);
            }
        }

        /// <summary>
        /// Get subscription by name
        /// </summary>
        public PubSubSubscriptionDefinition GetSubscriptionByName(string name)
        {
            return this.FindSubscription(o => o.Name == name, 0, 1, out int _).FirstOrDefault();
        }

        /// <summary>
        /// Regiser a channel
        /// </summary>
        public PubSubChannelDefinition RegisterChannel(string name, Type dispatcherFactoryType, Uri endpoint, IDictionary<string, string> settings)
        {
            try
            {
                using (var client = this.GetRestClient())
                {
                    return client.Post<PubSubChannelDefinition, PubSubChannelDefinition>($"PubSubChannel", new PubSubChannelDefinition()
                    {
                        DispatcherFactoryTypeXml = dispatcherFactoryType.AssemblyQualifiedName,
                        Endpoint = endpoint.ToString(),
                        Settings = settings.Select(o => new PubSubChannelSetting() { Name = o.Key, Value = o.Value }).ToList()
                    });
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error creating channel - {0}", e);
                throw new Exception($"Error creating channel", e);
            }
        }

        /// <summary>
        /// Register a channel with default handler
        /// </summary>
        public PubSubChannelDefinition RegisterChannel(string name, Uri endpoint, IDictionary<string, string> settings)
        {
            try
            {
                using (var client = this.GetRestClient())
                {
                    return client.Post<PubSubChannelDefinition, PubSubChannelDefinition>($"PubSubChannel", new PubSubChannelDefinition()
                    {
                        Endpoint = endpoint.ToString(),
                        Settings = settings.Select(o => new PubSubChannelSetting() { Name = o.Key, Value = o.Value }).ToList()
                    });
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error creating channel - {0}", e);
                throw new Exception($"Error creating channel", e);
            }
        }

        /// <summary>
        /// Register subscription
        /// </summary>
        public PubSubSubscriptionDefinition RegisterSubscription<TModel>(string name, string description, PubSubEventType events, Expression<Func<TModel, bool>> filter, Guid channelId, string supportAddress = null, DateTimeOffset? notBefore = null, DateTimeOffset? notAfter = null)
        {
            var filterHdsi = QueryExpressionBuilder.BuildQuery(filter);
            return this.RegisterSubscription(typeof(TModel), name, description, events, new NameValueCollection(filterHdsi.ToArray()).ToString(), channelId, supportAddress, notBefore, notAfter);
        }

        /// <summary>
        /// Register subscription
        /// </summary>
        public PubSubSubscriptionDefinition RegisterSubscription(Type modelType, string name, string description, PubSubEventType events, string hdsiFilter, Guid channelId, string supportAddress = null, DateTimeOffset? notBefore = null, DateTimeOffset? notAfter = null)
        {
            try
            {
                using (var client = this.GetRestClient())
                {
                    return client.Post<PubSubSubscriptionDefinition, PubSubSubscriptionDefinition>($"PubSubSubscription", new PubSubSubscriptionDefinition()
                    {
                        ChannelKey = channelId,
                        Description = description,
                        Event = events,
                        Filter = new List<string>() { hdsiFilter },
                        Name = name,
                        NotAfter = notAfter,
                        NotBefore = notBefore,
                        ResourceTypeXml = modelType.AssemblyQualifiedName,
                        SupportContact = supportAddress
                    });
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error creating subscription - {0}", e);
                throw new Exception($"Error creating subscription", e);
            }
        }

        /// <summary>
        /// Remove the specified subscription
        /// </summary>
        public PubSubChannelDefinition RemoveChannel(Guid id)
        {
            try
            {
                using (var client = this.GetRestClient())
                {
                    return client.Delete<PubSubChannelDefinition>($"PubSubChannel/{id}");
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error deleting subscription - {0}", e);
                throw new Exception($"Error deleting subscription", e);
            }
        }

        /// <summary>
        /// Remove subscription
        /// </summary>
        public PubSubSubscriptionDefinition RemoveSubscription(Guid id)
        {
            try
            {
                using (var client = this.GetRestClient())
                {
                    return client.Delete<PubSubSubscriptionDefinition>($"PubSubSubscription/{id}");
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error deleting subscription - {0}", e);
                throw new Exception($"Error deleting subscription", e);
            }
        }

        /// <summary>
        /// Update channel
        /// </summary>

        public PubSubChannelDefinition UpdateChannel(Guid key, string name, Uri endpoint, IDictionary<string, string> settings)
        {
            try
            {
                using (var client = this.GetRestClient())
                {
                    return client.Put<PubSubChannelDefinition, PubSubChannelDefinition>($"PubSubChannel/{key}", new PubSubChannelDefinition()
                    {
                        Endpoint = endpoint.ToString(),
                        Settings = settings.Select(o => new PubSubChannelSetting() { Name = o.Key, Value = o.Value }).ToList()
                    });
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error updating channel - {0}", e);
                throw new Exception($"Error updating channel", e);
            }
        }

        /// <summary>
        /// Update subscription
        /// </summary>
        public PubSubSubscriptionDefinition UpdateSubscription(Guid key, string name, string description, PubSubEventType events, string hdsiFilter, string supportAddress = null, DateTimeOffset? notBefore = null, DateTimeOffset? notAfter = null)
        {
            try
            {
                using (var client = this.GetRestClient())
                {
                    return client.Put<PubSubSubscriptionDefinition, PubSubSubscriptionDefinition>($"PubSubSubscription/{key}", new PubSubSubscriptionDefinition()
                    {
                        Description = description,
                        Event = events,
                        Filter = new List<string>() { hdsiFilter },
                        Name = name,
                        NotAfter = notAfter,
                        NotBefore = notBefore,
                        SupportContact = supportAddress
                    });
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error creating subscription - {0}", e);
                throw new Exception($"Error creating subscription", e);
            }
        }
    }
}