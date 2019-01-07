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
using Myrtille.Services.Contracts;

namespace Myrtille.Web
{
    public class RemoteSession
    {
        public RemoteSessionManager Manager { get; private set; }

        public Guid Id;
        public RemoteSessionState State;
        public string HostName;
        public HostTypeEnum HostType;                   // RDP or SSH
        public SecurityProtocolEnum SecurityProtocol;
        public string ServerAddress;                    // :port, if specified
        public string VMGuid;                           // RDP over VM bus (Hyper-V)
        public bool VMEnhancedMode;                     // RDP over VM bus (Hyper-V)
        public string UserDomain;
        public string UserName;
        public string UserPassword;
        public int ClientWidth;
        public int ClientHeight;
        public bool ScaleDisplay;
        public ImageEncoding? ImageEncoding;            // provided by the client
        public int? ImageQuality;                       // provided by the client
        public int? ImageQuantity;                      // provided by the client
        public bool StatMode;
        public bool DebugMode;
        public bool CompatibilityMode;
        public string StartProgram;
        public bool AllowRemoteClipboard;               // set in web config
        public bool AllowFileTransfer;                  // set in web config
        public bool AllowPrintDownload;                 // set in web config
        public bool AllowSessionSharing;                // set in web config
        public string OwnerSessionID;                   // the http session on which the remote session is bound to
        public int ExitCode;

        public RemoteSession(
            Guid id,
            RemoteSessionState state,
            string hostName,
            HostTypeEnum hostType,
            SecurityProtocolEnum securityProtocol,
            string serverAddress,
            string vmGuid,
            bool vmEnhancedMode,
            string userDomain,
            string userName,
            string userPassword,
            int clientWidth,
            int clientHeight,
            string startProgram,
            bool allowRemoteClipboard,
            bool allowFileTransfer,
            bool allowPrintDownload,
            bool allowSessionSharing,
            string ownerSessionID)
        {
            Id = id;
            State = state;
            HostName = hostName;
            HostType = hostType;
            SecurityProtocol = securityProtocol;
            ServerAddress = serverAddress;
            VMGuid = vmGuid;
            VMEnhancedMode = vmEnhancedMode;
            UserDomain = userDomain;
            UserName = userName;
            UserPassword = userPassword;
            ClientWidth = clientWidth;
            ClientHeight = clientHeight;
            StartProgram = startProgram;
            AllowRemoteClipboard = allowRemoteClipboard;
            AllowFileTransfer = allowFileTransfer;
            AllowPrintDownload = allowPrintDownload;
            AllowSessionSharing = allowSessionSharing;
            OwnerSessionID = ownerSessionID;

            Manager = new RemoteSessionManager(this);
        }
    }
}