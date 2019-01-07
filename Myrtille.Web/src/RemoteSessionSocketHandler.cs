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
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web.SessionState;
using Microsoft.Web.WebSockets;
using Myrtille.Network;
using Myrtille.Services.Contracts;

namespace Myrtille.Web
{
    public class RemoteSessionSocketHandler : WebSocketHandler
    {
        private readonly HttpSessionState _session;
        private readonly RemoteSession _remoteSession;

        public bool BinaryMode { get; private set; }

        public DataBuffer<int> Buffer { get; private set; }
        private const int _bufferSize = 100;
        private const int _bufferDelay = 10;

        public RemoteSessionSocketHandler(HttpSessionState session, bool binaryMode)
            : base()
        {
            _session = session;
            BinaryMode = binaryMode;

            try
            {
                if (session[HttpSessionStateVariables.RemoteSession.ToString()] == null)
                    throw new NullReferenceException();

                // retrieve the remote session for the given http session
                _remoteSession = (RemoteSession)session[HttpSessionStateVariables.RemoteSession.ToString()];

                bool websocketBuffering;
                if (!bool.TryParse(ConfigurationManager.AppSettings["WebsocketBuffering"], out websocketBuffering))
                {
                    websocketBuffering = true;
                }

                // RDP: display updates are buffered to fit the client latency; buffer data is also invalidated past the image cache duration (default 1 sec) to avoid lag
                // SSH: terminal messages are unbuffered
                if (websocketBuffering && _remoteSession.HostType == HostTypeEnum.RDP)
                {
                    Buffer = new DataBuffer<int>(_bufferSize, _bufferDelay);
                    Buffer.SendBufferData = SendBufferData;
                }
            }
            catch (Exception exc)
            {
                Trace.TraceError("Failed to retrieve the remote session for the http session {0}, ({1})", session.SessionID, exc);
            }
        }

        public override void OnOpen()
        {
            Trace.TraceInformation("Opening websocket, remote session {0}", _remoteSession.Id);
            _remoteSession.Manager.WebSockets.Add(this);

            if (Buffer != null)
            {
                Buffer.Start();
            }

            base.OnOpen();

            // send a disconnect notification
            if (_remoteSession.State == RemoteSessionState.Disconnected)
            {
                Trace.TraceInformation("Sending disconnected state on websocket, remote session {0}", _remoteSession.Id);
                Send("disconnected");
            }
        }

        public override void OnClose()
        {
            Trace.TraceInformation("Closing websocket, remote session {0}", _remoteSession.Id);
            _remoteSession.Manager.WebSockets.Remove(this);

            if (Buffer != null)
            {
                Buffer.Stop();
            }

            base.OnClose();
        }

        public override void OnError()
        {
            Trace.TraceError("Websocket error, remote session {0} ({1})", _remoteSession.Id, Error);
            base.OnError();
        }

        public override void OnMessage(string message)
        {
            ProcessMessage(message);
        }

        public override void OnMessage(byte[] message)
        {
            ProcessMessage(Encoding.UTF8.GetString(message));
        }

        private void ProcessMessage(string message)
        {
            try
            {
                var msgParams = message.Split(new[] { "&" }, StringSplitOptions.None);

                var data = Uri.UnescapeDataString(msgParams[0]);
                var imgIdx = int.Parse(msgParams[1]);   // if needed
                var latency = int.Parse(msgParams[2]);  // if needed

                // fit buffer delay according to latency
                if (Buffer != null)
                {
                    Buffer.Delay = latency;
                }

                var timestamp = long.Parse(msgParams[3]);

                // process input(s)
                if (!string.IsNullOrEmpty(data))
                {
                    _remoteSession.Manager.ProcessInputs(_session, data);
                }

                // acknowledge the message processing with the given timestamp; it will be used by the client to compute the roundtrip time
                Send("ack," + timestamp);
            }
            catch (Exception exc)
            {
                Trace.TraceError("Failed to process websocket message, remote session {0} ({1})", _remoteSession.Id, exc);
            }
        }

        public void ProcessImage(RemoteSessionImage image)
        {
            if (Buffer != null)
            {
                Buffer.AddItem(image.Idx);
            }
            else
            {
                if (!BinaryMode)
                {
                    Send(GetImageText(image) + ";");
                }
                else
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        var bytes = GetImageBytes(image);
                        memoryStream.Write(BitConverter.GetBytes(bytes.Length), 0, 4);
                        memoryStream.Write(bytes, 0, bytes.Length);
                        Send(memoryStream.ToArray());
                    }
                }
            }
        }

        public new void Send(string message)
        {
            if (!BinaryMode)
            {
                base.Send(message);
            }
            else
            {
                using (var memoryStream = new MemoryStream())
                {
                    var bytes = Encoding.UTF8.GetBytes(message);
                    memoryStream.Write(BitConverter.GetBytes(bytes.Length), 0, 4);
                    memoryStream.Write(bytes, 0, bytes.Length);
                    Send(memoryStream.ToArray());
                }
            }
        }

        public void SendBufferData(List<int> data)
        {
            if (!BinaryMode)
            {
                SendBufferText(data);
            }
            else
            {
                SendBufferBytes(data);
            }
        }

        private void SendBufferText(List<int> data)
        {
            var text = string.Empty;
            foreach (var imageIdx in data)
            {
                var image = _remoteSession.Manager.GetCachedUpdate(imageIdx);
                if (image != null)
                {
                    text += GetImageText(image) + ";";
                }
            }
            Send(text);
        }

        private void SendBufferBytes(List<int> data)
        {
            using (var memoryStream = new MemoryStream())
            {
                foreach (var imageIdx in data)
                {
                    var image = _remoteSession.Manager.GetCachedUpdate(imageIdx);
                    if (image != null)
                    {
                        var bytes = GetImageBytes(image);
                        memoryStream.Write(BitConverter.GetBytes(bytes.Length), 0, 4);
                        memoryStream.Write(bytes, 0, bytes.Length);
                    }
                }

                Send(memoryStream.ToArray());
            }
        }

        private string GetImageText(RemoteSessionImage image)
        {
            return
                image.Idx + "," +
                image.PosX + "," +
                image.PosY + "," +
                image.Width + "," +
                image.Height + "," +
                image.Format.ToString().ToLower() + "," +
                image.Quality + "," +
                image.Fullscreen.ToString().ToLower() + "," +
                Convert.ToBase64String(image.Data);
        }

        private byte[] GetImageBytes(RemoteSessionImage image)
        {
            using (var memoryStream = new MemoryStream())
            {
                // tag (4 bytes)
                memoryStream.Write(BitConverter.GetBytes(0), 0, 4);

                // info (32 bytes)
                memoryStream.Write(BitConverter.GetBytes(image.Idx), 0, 4);
                memoryStream.Write(BitConverter.GetBytes(image.PosX), 0, 4);
                memoryStream.Write(BitConverter.GetBytes(image.PosY), 0, 4);
                memoryStream.Write(BitConverter.GetBytes(image.Width), 0, 4);
                memoryStream.Write(BitConverter.GetBytes(image.Height), 0, 4);
                memoryStream.Write(BitConverter.GetBytes((int)image.Format), 0, 4);
                memoryStream.Write(BitConverter.GetBytes(image.Quality), 0, 4);
                memoryStream.Write(BitConverter.GetBytes(image.Fullscreen ? 1 : 0), 0, 4);

                // data
                memoryStream.Write(image.Data, 0, image.Data.Length);

                return memoryStream.ToArray();
            }
        }
    }
}