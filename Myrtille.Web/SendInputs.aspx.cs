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
using System.Collections;
using System.Collections.Generic;
using System.Web.UI;

namespace Myrtille.Web
{
    public partial class SendInputs : Page
    {
        /// <summary>
        /// send user input(s) (mouse, keyboard) to the remote session
        /// if long-polling is disabled (xhr only), also returns image data within the response
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void Page_Load(
            object sender,
            EventArgs e)
        {
            // if cookies are enabled, the http session id is added to the http request headers; otherwise, it's added to the http request url
            // in both cases, the given http session is automatically bound to the current http context

            RemoteSession remoteSession = null;

            try
            {
                if (Session[HttpSessionStateVariables.RemoteSession.ToString()] == null)
                    throw new NullReferenceException();

                // retrieve the remote session for the current http session
                remoteSession = (RemoteSession)Session[HttpSessionStateVariables.RemoteSession.ToString()];

                try
                {
                    // retrieve params
                    var data = Request.QueryString["data"];
                    var imgIdx = int.Parse(Request.QueryString["imgIdx"]);
                    var imgReturn = int.Parse(Request.QueryString["imgReturn"]) == 1;

                    // process input(s)
                    if (!string.IsNullOrEmpty(data))
                    {
                        remoteSession.Manager.ProcessInputs(Session, data);
                    }

                    // xhr only
                    if (imgReturn)
                    {
                        // disconnected session
                        if (remoteSession.State == RemoteSessionState.Disconnected)
                        {
                            Response.Write("disconnected");
                            return;
                        }

                        // message queue
                        List<RemoteSessionMessage> messageQueue = null;
                        lock (remoteSession.Manager.MessageQueues.SyncRoot)
                        {
                            if (!remoteSession.Manager.MessageQueues.ContainsKey(Session.SessionID))
                            {
                                remoteSession.Manager.MessageQueues.Add(Session.SessionID, new List<RemoteSessionMessage>());
                            }
                            messageQueue = (List<RemoteSessionMessage>)remoteSession.Manager.MessageQueues[Session.SessionID];
                        }

                        // concatenate text for terminal output to avoid a slow rendering
                        // if another message type is in the queue, it will be given priority over the terminal
                        // the terminal is refreshed often, so it shouldn't be an issue...
                        var msgText = string.Empty;
                        var msgComplete = false;

                        while (messageQueue.Count > 0 && !msgComplete)
                        {
                            var message = messageQueue[0];

                            switch (message.Type)
                            {
                                case MessageType.PageReload:
                                    msgText = "reload";
                                    msgComplete = true;
                                    break;

                                case MessageType.RemoteClipboard:
                                    msgText = string.Format("clipboard|{0}", message.Text);
                                    msgComplete = true;
                                    break;

                                case MessageType.TerminalOutput:
                                    msgText += string.IsNullOrEmpty(msgText) ? string.Format("term|{0}", message.Text) : message.Text;
                                    break;

                                case MessageType.PrintJob:
                                    msgText = string.Format("printjob|{0}", message.Text);
                                    msgComplete = true;
                                    break;
                            }

                            lock (((ICollection)messageQueue).SyncRoot)
                            {
                                messageQueue.RemoveAt(0);
                            }
                        }

                        if (!string.IsNullOrEmpty(msgText))
                        {
                            Response.Write(msgText);
                        }
                        // next update
                        else
                        {
                            var image = remoteSession.Manager.GetNextUpdate(imgIdx);
                            if (image != null)
                            {
                                System.Diagnostics.Trace.TraceInformation("Returning image {0} ({1}), remote session {2}", image.Idx, (image.Fullscreen ? "screen" : "region"), remoteSession.Id);

                                var imgData =
                                    image.Idx + "," +
                                    image.PosX + "," +
                                    image.PosY + "," +
                                    image.Width + "," +
                                    image.Height + "," +
                                    image.Format.ToString().ToLower() + "," +
                                    image.Quality + "," +
                                    image.Fullscreen.ToString().ToLower() + "," +
                                    Convert.ToBase64String(image.Data);

                                // write the output
                                Response.Write(imgData);
                            }
                        }
                    }
                }
                catch (Exception exc)
                {
                    System.Diagnostics.Trace.TraceError("Failed to send user input(s), remote session {0} ({1})", remoteSession.Id, exc);
                }
            }
            catch (Exception exc)
            {
                System.Diagnostics.Trace.TraceError("Failed to retrieve the active remote session ({0})", exc);
            }
        }
    }
}