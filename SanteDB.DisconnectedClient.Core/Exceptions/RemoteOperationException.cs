﻿using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.DisconnectedClient.Exceptions
{
    /// <summary>
    /// Remote operation exception
    /// </summary>
    public class RemoteOperationException : Exception
    {

        /// <summary>
        /// Creates a new remote operation exception
        /// </summary>
        public RemoteOperationException()
        {

        }

        /// <summary>
        /// Creates a new remote operation exception
        /// </summary>
        public RemoteOperationException(String message) : base(message)
        {

        }

        /// <summary>
        /// Creates a new remote operation exception
        /// </summary>
        public RemoteOperationException(String message, Exception cause) : base (message, cause)
        {

        }
    }
}