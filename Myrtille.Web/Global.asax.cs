/*
    Myrtille: A native HTML4/5 Remote Desktop Protocol client.

    Copyright(c) 2014-2018 Cedric Coste

    Licensed under the Apache License, Version 2.0 (the "License");
    you may not use this file except in compliance with the License.
    You may obtain a copy of the License at

        http://www.apache.org/licenses/LICENSE-2.0

    Unless required by applicable law or agreed to in writing, software
    distributed under the License is distributed on an "AS IS" BASIS,
    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
    See the License for the specific language governing permissions and
    limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Web;
using log4net.Config;

namespace Myrtille.Web
{
    public enum HttpApplicationStateVariables
    {
        Cache,
        SharedRemoteSessions
    }

    public enum HttpSessionStateVariables
    {
        ClientIP,
        ClientKey,
        EnterpriseSession,
        RemoteSession
    }

    public class Global : HttpApplication
    {
        protected void Application_Start(
            object sender,
            EventArgs e)
        {
            try
            {
                // logger
                XmlConfigurator.Configure();

                // application cache
                Application[HttpApplicationStateVariables.Cache.ToString()] = Context.Cache;

                // shared remote sessions
                Application[HttpApplicationStateVariables.SharedRemoteSessions.ToString()] = new Dictionary<string, RemoteSession>();
            }
            catch (Exception exc)
            {
                Trace.TraceError("Failed to start Myrtille.Web application ({0})", exc);
                throw;
            }
        }
    }
}