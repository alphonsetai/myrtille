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

using System.Collections;

namespace Myrtille.Services.Contracts
{
    public enum RemoteSessionCommand
    {
        // connection
        SendServerAddress = 0,
        SendVMGuid = 1,
        SendUserDomain = 2,
        SendUserName = 3,
        SendUserPassword = 4,
        SendStartProgram = 5,
        ConnectClient = 6,

        // browser
        SendBrowserResize = 7,

        // keyboard
        SendKeyUnicode = 8,
        SendKeyScancode = 9,

        // mouse
        SendMouseMove = 10,
        SendMouseLeftButton = 11,
        SendMouseMiddleButton = 12,
        SendMouseRightButton = 13,
        SendMouseWheelUp = 14,
        SendMouseWheelDown = 15,

        // control
        SetStatMode = 16,
        SetDebugMode = 17,
        SetCompatibilityMode = 18,
        SetScaleDisplay = 19,
        SetImageEncoding = 20,
        SetImageQuality = 21,
        SetImageQuantity = 22,
        RequestFullscreenUpdate = 23,
        RequestRemoteClipboard = 24,
        CloseClient = 25
    }

    /*
    prefixes (3 chars) are used to serialize commands with strings instead of numbers
    they make it easier to read log traces to find out which commands are issued
    they must match the prefixes used client side
    */
    public static class RemoteSessionCommandMapping
    {
        public static Hashtable FromPrefix { get; private set; }
        public static Hashtable ToPrefix { get; private set; }

        static RemoteSessionCommandMapping()
        {
            FromPrefix = new Hashtable();
            FromPrefix["SRV"] = RemoteSessionCommand.SendServerAddress;
            FromPrefix["VMG"] = RemoteSessionCommand.SendVMGuid;
            FromPrefix["DOM"] = RemoteSessionCommand.SendUserDomain;
            FromPrefix["USR"] = RemoteSessionCommand.SendUserName;
            FromPrefix["PWD"] = RemoteSessionCommand.SendUserPassword;
            FromPrefix["PRG"] = RemoteSessionCommand.SendStartProgram;
            FromPrefix["CON"] = RemoteSessionCommand.ConnectClient;
            FromPrefix["RSZ"] = RemoteSessionCommand.SendBrowserResize;
            FromPrefix["KUC"] = RemoteSessionCommand.SendKeyUnicode;
            FromPrefix["KSC"] = RemoteSessionCommand.SendKeyScancode;
            FromPrefix["MMO"] = RemoteSessionCommand.SendMouseMove;
            FromPrefix["MLB"] = RemoteSessionCommand.SendMouseLeftButton;
            FromPrefix["MMB"] = RemoteSessionCommand.SendMouseMiddleButton;
            FromPrefix["MRB"] = RemoteSessionCommand.SendMouseRightButton;
            FromPrefix["MWU"] = RemoteSessionCommand.SendMouseWheelUp;
            FromPrefix["MWD"] = RemoteSessionCommand.SendMouseWheelDown;
            FromPrefix["STA"] = RemoteSessionCommand.SetStatMode;
            FromPrefix["DBG"] = RemoteSessionCommand.SetDebugMode;
            FromPrefix["CMP"] = RemoteSessionCommand.SetCompatibilityMode;
            FromPrefix["SCA"] = RemoteSessionCommand.SetScaleDisplay;
            FromPrefix["ECD"] = RemoteSessionCommand.SetImageEncoding;
            FromPrefix["QLT"] = RemoteSessionCommand.SetImageQuality;
            FromPrefix["QNT"] = RemoteSessionCommand.SetImageQuantity;
            FromPrefix["FSU"] = RemoteSessionCommand.RequestFullscreenUpdate;
            FromPrefix["CLP"] = RemoteSessionCommand.RequestRemoteClipboard;
            FromPrefix["CLO"] = RemoteSessionCommand.CloseClient;

            ToPrefix = new Hashtable();
            ToPrefix[RemoteSessionCommand.SendServerAddress] = "SRV";
            ToPrefix[RemoteSessionCommand.SendVMGuid] = "VMG";
            ToPrefix[RemoteSessionCommand.SendUserDomain] = "DOM";
            ToPrefix[RemoteSessionCommand.SendUserName] = "USR";
            ToPrefix[RemoteSessionCommand.SendUserPassword] = "PWD";
            ToPrefix[RemoteSessionCommand.SendStartProgram] = "PRG";
            ToPrefix[RemoteSessionCommand.ConnectClient] = "CON";
            ToPrefix[RemoteSessionCommand.SendBrowserResize] = "RSZ";
            ToPrefix[RemoteSessionCommand.SendKeyUnicode] = "KUC";
            ToPrefix[RemoteSessionCommand.SendKeyScancode] = "KSC";
            ToPrefix[RemoteSessionCommand.SendMouseMove] = "MMO";
            ToPrefix[RemoteSessionCommand.SendMouseLeftButton] = "MLB";
            ToPrefix[RemoteSessionCommand.SendMouseMiddleButton] = "MMB";
            ToPrefix[RemoteSessionCommand.SendMouseRightButton] = "MRB";
            ToPrefix[RemoteSessionCommand.SendMouseWheelUp] = "MWU";
            ToPrefix[RemoteSessionCommand.SendMouseWheelDown] = "MWD";
            ToPrefix[RemoteSessionCommand.SetStatMode] = "STA";
            ToPrefix[RemoteSessionCommand.SetDebugMode] = "DBG";
            ToPrefix[RemoteSessionCommand.SetCompatibilityMode] = "CMP";
            ToPrefix[RemoteSessionCommand.SetScaleDisplay] = "SCA";
            ToPrefix[RemoteSessionCommand.SetImageEncoding] = "ECD";
            ToPrefix[RemoteSessionCommand.SetImageQuality] = "QLT";
            ToPrefix[RemoteSessionCommand.SetImageQuantity] = "QNT";
            ToPrefix[RemoteSessionCommand.RequestFullscreenUpdate] = "FSU";
            ToPrefix[RemoteSessionCommand.RequestRemoteClipboard] = "CLP";
            ToPrefix[RemoteSessionCommand.CloseClient] = "CLO";
        }
    }
}