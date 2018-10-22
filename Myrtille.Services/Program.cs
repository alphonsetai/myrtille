﻿/*
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
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.ServiceModel;
using System.ServiceProcess;
using log4net.Config;
using Myrtille.Services.Contracts;

namespace Myrtille.Services
{
    public class Program : ServiceBase
    {
        private static ServiceHost _remoteSessionCounter;
        private static ServiceHost _remoteSessionProcess;
        private static ServiceHost _localFileStorage;
        private static ServiceHost _printerService;
        private static ServiceHost _mfaAuthentication;
        private static ServiceHost _enterpriseServices;

        public static int _remoteSessionsCounter = 0;
        public static object _remoteSessionsCounterLock = new object();

        public static IMultifactorAuthenticationAdapter _multifactorAdapter = null;
        public static IEnterpriseAdapter _enterpriseAdapter = null;

        public static string _adminGroup;
        public static string _enterpriseDomain;

        private static ServiceHost OpenService(Type serviceType)
        {
            ServiceHost serviceHost = null;
            
            try
            {
                serviceHost = new ServiceHost(serviceType);
                serviceHost.Open();

                var description = serviceHost.Description;
                Trace.TraceInformation(string.Format("Service: {0}", description.ConfigurationName));
                foreach (var endpoint in description.Endpoints)
                {
                    Trace.TraceInformation(string.Format("Endpoint: {0}", endpoint.Name));
                    Trace.TraceInformation(string.Format("Address: {0}", endpoint.Address));
                    Trace.TraceInformation(string.Format("Binding: {0}", endpoint.Binding.Name));
                    Trace.TraceInformation(string.Format("Contract: {0}", endpoint.Contract.ConfigurationName));
                    Trace.TraceInformation("");
                }
            }
            catch (Exception exc)
            {
                Trace.TraceError("Failed to start service ({0})", exc);
                if (serviceHost != null)
                {
                    serviceHost.Abort();
                }
            }

            return serviceHost;
        }

        private static void CloseService(ref ServiceHost serviceHost)
        {
            if (serviceHost != null)
            {
                serviceHost.Close();
                serviceHost = null;
            }
        }

        private static void Main(string[] args)
        {
            // logger
            XmlConfigurator.Configure();

            // database (enterprise mode)
            ConfigureEnterpriseDatabase();

            if (!Environment.UserInteractive)
            {
                Run(new Program());
            }
            else
            {
                var consoleTraceListener = new ConsoleTraceListener();
                consoleTraceListener.Filter = new EventTypeFilter(SourceLevels.Information);
                Trace.Listeners.Add(consoleTraceListener);

                LoadMFAAdapter();
                LoadEnterpriseAdapter();

                _remoteSessionCounter = OpenService(typeof(RemoteSessionCounter));
                _remoteSessionProcess = OpenService(typeof(RemoteSessionProcess));
                _localFileStorage = OpenService(typeof(FileStorage));
                _printerService = OpenService(typeof(PrinterService));
                _mfaAuthentication = OpenService(typeof(MFAAuthentication));
                _enterpriseServices = OpenService(typeof(EnterpriseService));

                Console.WriteLine("press any key to exit...");
                Console.ReadKey();

                CloseService(ref _remoteSessionCounter);
                CloseService(ref _remoteSessionProcess);
                CloseService(ref _localFileStorage);
                CloseService(ref _printerService);
                CloseService(ref _mfaAuthentication);
                CloseService(ref _enterpriseServices);
            }
        }

        protected override void OnStart(string[] args)
		{
            LoadMFAAdapter();
            LoadEnterpriseAdapter();

            _remoteSessionCounter = OpenService(typeof(RemoteSessionCounter));
            _remoteSessionProcess = OpenService(typeof(RemoteSessionProcess));
            _localFileStorage = OpenService(typeof(FileStorage));
            _printerService = OpenService(typeof(PrinterService));
            _mfaAuthentication = OpenService(typeof(MFAAuthentication));
            _enterpriseServices = OpenService(typeof(EnterpriseService));
        }
 
		protected override void OnStop()
		{
            CloseService(ref _remoteSessionCounter);
            CloseService(ref _remoteSessionProcess);
            CloseService(ref _localFileStorage);
            CloseService(ref _printerService);
            CloseService(ref _mfaAuthentication);
            CloseService(ref _enterpriseServices);
        }

        // TODO? create config sections (into app.config) for the 2 adapters below

        private static void LoadMFAAdapter()
        {
            var configuration = ConfigurationManager.AppSettings["MFAAuthAdapter"];
            if (configuration == null)
                return;

            var assemblyDetails = configuration.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);
            if (assemblyDetails.Length != 2)
                throw new FormatException("MFAAuthAdapter configuration is invalid!");

            var assembly = Assembly.Load(assemblyDetails[1].Trim());
            _multifactorAdapter = (IMultifactorAuthenticationAdapter)assembly.CreateInstance(assemblyDetails[0]);
            if (_multifactorAdapter == null)
                throw new InvalidOperationException(string.Format("Unable to create instance of {0}", assemblyDetails[0]));
        }

        private static void LoadEnterpriseAdapter()
        {
            var configuration = ConfigurationManager.AppSettings["EnterpriseAdapter"];
            if (configuration == null)
                return;

            var assemblyDetails = configuration.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);
            if (assemblyDetails.Length != 2)
                throw new FormatException("EnterpriseAdapter configuration is invalid!");

            var assembly = Assembly.Load(assemblyDetails[1].Trim());
            _enterpriseAdapter = (IEnterpriseAdapter)assembly.CreateInstance(assemblyDetails[0]);
            if (_enterpriseAdapter == null)
                throw new InvalidOperationException(string.Format("Unable to create instance of {0}", assemblyDetails[0]));

            _adminGroup = ConfigurationManager.AppSettings["EnterpriseAdminGroup"];
            if (_adminGroup == null)
                throw new Exception("EnterpriseAdminGroup has not been configured!");

            _enterpriseDomain = ConfigurationManager.AppSettings["EnterpriseDomain"];
            if (_enterpriseDomain == null)
                throw new Exception("EnterpriseDomain has not been configured!");

            _enterpriseAdapter.Initialize();
        }

        private static void ConfigureEnterpriseDatabase()
        {
            // SQLCE DB folder
            var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\data");

            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }

            AppDomain.CurrentDomain.SetData("DataDirectory", dataDir);
        }
    }
}