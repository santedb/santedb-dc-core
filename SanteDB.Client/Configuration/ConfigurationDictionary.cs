/*
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
 * Date: 2023-6-21
 */
using System.Collections;
using System.Collections.Generic;

namespace SanteDB.Client.Configuration
{
    /// <summary>
    /// An implementation of an <see cref="IDictionary{TKey, TValue}"/> where getting by index returns null
    /// </summary>
    public class ConfigurationDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {

        // Wrapped dictionary
        private readonly IDictionary<TKey, TValue> m_wrapped = new Dictionary<TKey, TValue>();

        /// <inheritdoc/>
        public TValue this[TKey key]
        {
            get
            {
                if (this.m_wrapped.TryGetValue(key, out var value))
                {
                    return value;
                }
                else
                {
                    return default(TValue);
                }
            }
            set
            {
                if (this.m_wrapped.ContainsKey(key))
                {
                    this.m_wrapped[key] = value;
                }
                else
                {
                    this.m_wrapped.Add(key, value);
                }
            }
        }


        /// <inheritdoc/>
        public ICollection<TKey> Keys => this.m_wrapped.Keys;

        /// <inheritdoc/>
        public ICollection<TValue> Values => this.m_wrapped.Values;

        /// <inheritdoc/>
        public int Count => this.m_wrapped.Count;

        /// <inheritdoc/>
        public bool IsReadOnly => this.m_wrapped.IsReadOnly;

        /// <inheritdoc/>
        public void Add(TKey key, TValue value) => this.m_wrapped.Add(key, value);

        /// <inheritdoc/>
        public void Add(KeyValuePair<TKey, TValue> item) => this.m_wrapped.Add(item);

        /// <inheritdoc/>
        public void Clear() => this.m_wrapped.Clear();

        /// <inheritdoc/>
        public bool Contains(KeyValuePair<TKey, TValue> item) => this.m_wrapped.Contains(item);

        /// <inheritdoc/>
        public bool ContainsKey(TKey key) => this.m_wrapped.ContainsKey(key);

        /// <inheritdoc/>
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => this.m_wrapped.CopyTo(array, arrayIndex);

        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => this.m_wrapped.GetEnumerator();

        /// <inheritdoc/>
        public bool Remove(TKey key) => this.m_wrapped.Remove(key);

        /// <inheritdoc/>
        public bool Remove(KeyValuePair<TKey, TValue> item) => this.m_wrapped.Remove(item);

        /// <inheritdoc/>
        public bool TryGetValue(TKey key, out TValue value) => this.m_wrapped.TryGetValue(key, out value);

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => this.m_wrapped.GetEnumerator();
    }
}
