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
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Web.Configuration;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using Myrtille.Helpers;
using Myrtille.Services.Contracts;

namespace Myrtille.Web
{
    public partial class Default : Page
    {
        private MFAAuthenticationClient _mfaAuthClient;
        private EnterpriseServiceClient _enterpriseClient;

        private bool _allowRemoteClipboard;
        private bool _allowFileTransfer;
        private bool _allowPrintDownload;
        private bool _allowSessionSharing;
        private bool _clientIPTracking;
        private bool _cookielessSession;

        private bool _authorizedRequest = true;

        private EnterpriseSession _enterpriseSession;
        protected RemoteSession RemoteSession;

        /// <summary>
        /// page init
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void Page_Init(
            object sender,
            EventArgs e)
        {
            _mfaAuthClient = new MFAAuthenticationClient();
            _enterpriseClient = new EnterpriseServiceClient();

            // remote clipboard
            if (!bool.TryParse(ConfigurationManager.AppSettings["AllowRemoteClipboard"], out _allowRemoteClipboard))
            {
                _allowRemoteClipboard = true;
            }

            // file transfer
            if (!bool.TryParse(ConfigurationManager.AppSettings["AllowFileTransfer"], out _allowFileTransfer))
            {
                _allowFileTransfer = true;
            }

            // print download
            if (!bool.TryParse(ConfigurationManager.AppSettings["AllowPrintDownload"], out _allowPrintDownload))
            {
                _allowPrintDownload = true;
            }

            // session sharing
            if (!bool.TryParse(ConfigurationManager.AppSettings["AllowSessionSharing"], out _allowSessionSharing))
            {
                _allowSessionSharing = true;
            }

            // client ip tracking
            if (!bool.TryParse(ConfigurationManager.AppSettings["ClientIPTracking"], out _clientIPTracking))
            {
                _clientIPTracking = false;
            }

            // cookieless session
            var sessionStateSection = (SessionStateSection)ConfigurationManager.GetSection("system.web/sessionState");
            _cookielessSession = sessionStateSection.Cookieless == HttpCookieMode.UseUri;
        }

        /// <summary>
        /// page load (postback data is now available)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void Page_Load(
            object sender,
            EventArgs e)
        {
            // client ip protection
            if (_clientIPTracking)
            {
                var clientIP = ClientIPHelper.ClientIPFromRequest(new HttpContextWrapper(HttpContext.Current).Request, true, new string[] { });
                if (Session[HttpSessionStateVariables.ClientIP.ToString()] == null)
                {
                    Session[HttpSessionStateVariables.ClientIP.ToString()] = clientIP;
                }
                else if (!((string)Session[HttpSessionStateVariables.ClientIP.ToString()]).Equals(clientIP))
                {
                    System.Diagnostics.Trace.TraceWarning("Failed to validate the client ip");
                    _authorizedRequest = false;
                    UpdateControls();
                    return;
                }
            }

            // session spoofing protection
            if (_cookielessSession)
            {
                if (Request.Cookies["clientKey"] == null)
                {
                    if (Session[HttpSessionStateVariables.ClientKey.ToString()] == null)
                    {
                        var cookie = new HttpCookie("clientKey");
                        cookie.Value = Guid.NewGuid().ToString();
                        cookie.Path = "/";
                        Response.Cookies.Add(cookie);
                    }
                    else
                    {
                        System.Diagnostics.Trace.TraceWarning("Failed to validate the client key: missing key");
                        _authorizedRequest = false;
                        UpdateControls();
                        return;
                    }
                }
                else
                {
                    var clientKey = Request.Cookies["clientKey"].Value;
                    if (Session[HttpSessionStateVariables.ClientKey.ToString()] == null)
                    {
                        Session[HttpSessionStateVariables.ClientKey.ToString()] = clientKey;
                    }
                    else if (!((string)Session[HttpSessionStateVariables.ClientKey.ToString()]).Equals(clientKey))
                    {
                        System.Diagnostics.Trace.TraceWarning("Failed to validate the client key: key mismatch");
                        _authorizedRequest = false;
                        UpdateControls();
                        return;
                    }
                }
            }

            // retrieve the active enterprise session, if any
            if (Session[HttpSessionStateVariables.EnterpriseSession.ToString()] != null)
            {
                try
                {
                    _enterpriseSession = (EnterpriseSession)Session[HttpSessionStateVariables.EnterpriseSession.ToString()];
                }
                catch (Exception exc)
                {
                    System.Diagnostics.Trace.TraceError("Failed to retrieve the active enterprise session ({0})", exc);
                }
            }

            // retrieve the active remote session, if any
            if (Session[HttpSessionStateVariables.RemoteSession.ToString()] != null)
            {
                try
                {
                    RemoteSession = (RemoteSession)Session[HttpSessionStateVariables.RemoteSession.ToString()];

                    if (RemoteSession.State == RemoteSessionState.Disconnected)
                    {
                        // handle connection failure
                        ClientScript.RegisterClientScriptBlock(GetType(), Guid.NewGuid().ToString(), string.Format("handleRemoteSessionExit({0});", RemoteSession.ExitCode), true);

                        // cleanup
                        Session[HttpSessionStateVariables.RemoteSession.ToString()] = null;
                        RemoteSession = null;
                    }
                }
                catch (Exception exc)
                {
                    System.Diagnostics.Trace.TraceError("Failed to retrieve the active remote session ({0})", exc);
                }
            }
            // retrieve a shared remote session from url, if any
            else if (Request["SSE"] != null)
            {
                Session[HttpSessionStateVariables.RemoteSession.ToString()] = GetSharedRemoteSession(Request["SSE"]);

                try
                {
                    // remove the shared session guid from url
                    Response.Redirect("~/", true);
                }
                catch (ThreadAbortException)
                {
                    // occurs because the response is ended after redirect
                }
            }

            // postback events may redirect after execution; UI is updated from there
            if (!IsPostBack)
            {
                UpdateControls();
            }

            // disable the browser cache; in addition to a "noCache" dummy param, with current time, on long-polling and xhr requests
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
        }

        /// <summary>
        /// force remove the .net viewstate hidden fields from page (large bunch of unwanted data in url)
        /// </summary>
        /// <param name="writer"></param>
        protected override void Render(
            HtmlTextWriter writer)
        {
            var sb = new StringBuilder();
            var sw = new StringWriter(sb);
            var tw = new HtmlTextWriter(sw);
            base.Render(tw);
            var html = sb.ToString();
            html = Regex.Replace(html, "<input[^>]*id=\"(__VIEWSTATE)\"[^>]*>", string.Empty, RegexOptions.IgnoreCase);
            html = Regex.Replace(html, "<input[^>]*id=\"(__VIEWSTATEGENERATOR)\"[^>]*>", string.Empty, RegexOptions.IgnoreCase);
            writer.Write(html);
        }

        /// <summary>
        /// update the UI
        /// </summary>
        private void UpdateControls()
        {
            // remote session toolbar
            if (RemoteSession != null && (RemoteSession.State == RemoteSessionState.Connecting || RemoteSession.State == RemoteSessionState.Connected))
            {
                toolbarToggle.Style["visibility"] = "visible";
                toolbarToggle.Style["display"] = "block";
                serverInfo.Value = !string.IsNullOrEmpty(RemoteSession.VMGuid) ? RemoteSession.VMGuid : (!string.IsNullOrEmpty(RemoteSession.HostName) ? RemoteSession.HostName : RemoteSession.ServerAddress);
                userInfo.Value = !string.IsNullOrEmpty(RemoteSession.VMGuid) || RemoteSession.SecurityProtocol == SecurityProtocolEnum.rdp ? string.Empty : RemoteSession.UserName;
                userInfo.Visible = !string.IsNullOrEmpty(userInfo.Value);
                stat.Value = RemoteSession.StatMode ? "Hide Stat" : "Show Stat";
                stat.Disabled = !Session.SessionID.Equals(RemoteSession.OwnerSessionID);
                debug.Value = RemoteSession.DebugMode ? "Hide Debug" : "Show Debug";
                debug.Disabled = !Session.SessionID.Equals(RemoteSession.OwnerSessionID);
                browser.Value = RemoteSession.CompatibilityMode ? "HTML5" : "HTML4";
                browser.Disabled = !Session.SessionID.Equals(RemoteSession.OwnerSessionID);
                scale.Value = RemoteSession.ScaleDisplay ? "Unscale" : "Scale";
                scale.Disabled = !Session.SessionID.Equals(RemoteSession.OwnerSessionID) || RemoteSession.HostType == HostTypeEnum.SSH;
                keyboard.Disabled = !Session.SessionID.Equals(RemoteSession.OwnerSessionID);
                clipboard.Disabled = !Session.SessionID.Equals(RemoteSession.OwnerSessionID) || RemoteSession.HostType == HostTypeEnum.SSH || !RemoteSession.AllowRemoteClipboard || (!string.IsNullOrEmpty(RemoteSession.VMGuid) && !RemoteSession.VMEnhancedMode);
                files.Disabled = !Session.SessionID.Equals(RemoteSession.OwnerSessionID) || RemoteSession.HostType == HostTypeEnum.SSH || !RemoteSession.AllowFileTransfer || (RemoteSession.ServerAddress.ToLower() != "localhost" && RemoteSession.ServerAddress != "127.0.0.1" && RemoteSession.ServerAddress != "[::1]" && RemoteSession.ServerAddress != Request.Url.Host && string.IsNullOrEmpty(RemoteSession.UserDomain)) || !string.IsNullOrEmpty(RemoteSession.VMGuid);
                cad.Disabled = !Session.SessionID.Equals(RemoteSession.OwnerSessionID) || RemoteSession.HostType == HostTypeEnum.SSH;
                mrc.Disabled = !Session.SessionID.Equals(RemoteSession.OwnerSessionID) || RemoteSession.HostType == HostTypeEnum.SSH;
                vswipe.Disabled = !Session.SessionID.Equals(RemoteSession.OwnerSessionID) || RemoteSession.HostType == HostTypeEnum.SSH;
                share.Disabled = !Session.SessionID.Equals(RemoteSession.OwnerSessionID) || !RemoteSession.AllowSessionSharing;
                disconnect.Disabled = !Session.SessionID.Equals(RemoteSession.OwnerSessionID);
            }
            // hosts list
            else if (_enterpriseSession != null && _enterpriseSession.AuthenticationErrorCode == EnterpriseAuthenticationErrorCode.NONE)
            {
                hosts.Visible = true;
                enterpriseUserInfo.Value = _enterpriseSession.UserName;
                enterpriseUserInfo.Visible = !string.IsNullOrEmpty(enterpriseUserInfo.Value);
                newRDPHost.Visible = _enterpriseSession.IsAdmin;
                newSSHHost.Visible = _enterpriseSession.IsAdmin;
                hostsList.DataSource = _enterpriseClient.GetSessionHosts(_enterpriseSession.SessionID);
                hostsList.DataBind();
            }
            // login screen
            else
            {
                login.Visible = true;

                // MFA
                if (_mfaAuthClient.GetState())
                {
                    mfaDiv.Visible = true;
                    mfaProvider.InnerText = _mfaAuthClient.GetPromptLabel();
                    mfaProvider.HRef = _mfaAuthClient.GetProviderURL();
                }

                // enterprise mode
                if (_enterpriseClient.GetState())
                {
                    hostConnectDiv.Visible = false;
                }
                // standard mode
                else
                {
                    connect.Attributes["onclick"] = "initDisplay();";
                }
            }
        }

        /// <summary>
        /// enterprise mode from url: load the enterprise session (from querystring param) and proceed to connection; the user is non admin and the url is only usable once
        /// enterprise mode from login: authenticate the user against the enterprise active directory and list the servers available to the user; the user is admin if member of the "EnterpriseAdminGroup" defined into myrtille services config
        /// standard mode: connect the specified server; authentication is delegated to the remote server or connection broker (if applicable)
        /// if MFA is enabled and not already processed, authenticate the user against the configured MFA provider (OTP preferred)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void ConnectButtonClick(
            object sender,
            EventArgs e)
        {
            if (!_authorizedRequest)
                return;

            // one time usage enterprise session url
            if (_enterpriseSession == null && Request["SI"] != null && Request["SD"] != null && Request["SK"] != null)
            {
                CreateEnterpriseSessionFromUrl();
            }

            // MFA (OTP passcode)
            if (_enterpriseSession == null && _mfaAuthClient.GetState())
            {
                var clientIP = ClientIPHelper.ClientIPFromRequest(new HttpContextWrapper(HttpContext.Current).Request, true, new string[] { });
                if (!_mfaAuthClient.Authenticate(user.Value, mfaPassword.Value, clientIP))
                {
                    connectError.InnerText = "MFA Authentication failed!";
                    UpdateControls();
                    return;
                }
            }

            // enterprise mode from login
            if (_enterpriseSession == null && _enterpriseClient.GetState())
            {
                CreateEnterpriseSessionFromLogin();
            }
            // connection from:
            // > standard mode
            // > enterprise mode: hosts list
            // > enterprise mode: one time session url
            else
            {
                // the display size is required to start a remote session
                // if missing, the client will provide it automatically
                if (string.IsNullOrEmpty(width.Value) || string.IsNullOrEmpty(height.Value))
                {
                    return;
                }

                // connect
                if (ConnectRemoteServer())
                {
                    // in enterprise mode from login, a new http session id was already generated (no need to do it each time an host is connected!)
                    // in standard mode or enterprise mode from url, a new http session id must be generated
                    if (_enterpriseSession == null || Request["SI"] != null)
                    {
                        // session fixation protection
                        if (_cookielessSession)
                        {
                            // generate a new http session id
                            RemoteSession.OwnerSessionID = HttpSessionHelper.RegenerateSessionId();
                        }
                    }
                    try
                    {
                        // standard mode: switch to http get (standard login) or remove the connection params from url (auto-connect / start program from url)
                        // enterprise mode: remove the host id from url
                        Response.Redirect("~/", true);
                    }
                    catch (ThreadAbortException)
                    {
                        // occurs because the response is ended after redirect
                    }
                }
                // connection failed from the hosts list or from a one time session url
                else if (_enterpriseSession != null && Request["SD"] != null)
                {
                    try
                    {
                        // remove the host id from url
                        Response.Redirect("~/", true);
                    }
                    catch (ThreadAbortException)
                    {
                        // occurs because the response is ended after redirect
                    }
                }
            }
        }

        /// <summary>
        /// connect the remote server
        /// </summary>
        /// <remarks>
        /// authentication is delegated to the remote server or connection broker (if applicable)
        /// </remarks>
        private bool ConnectRemoteServer()
        {
            // connection parameters
            string loginHostName = null;
            var loginHostType = (HostTypeEnum)Convert.ToInt32(hostType.Value);
            var loginProtocol = (SecurityProtocolEnum)securityProtocol.SelectedIndex;
            var loginServer = string.IsNullOrEmpty(server.Value) ? "localhost" : server.Value;
            var loginVMGuid = vmGuid.Value;
            var loginVMEnhancedMode = vmEnhancedMode.Checked;
            var loginDomain = domain.Value;
            var loginUser = user.Value;
            var loginPassword = string.IsNullOrEmpty(passwordHash.Value) ? password.Value : RDPCryptoHelper.DecryptPassword(passwordHash.Value);
            var startProgram = program.Value;

            // connect an host from the hosts list or from a one time session url
            if (_enterpriseSession != null && Request["SD"] != null)
            {
                long hostId;
                if (!long.TryParse(Request["SD"], out hostId))
                {
                    hostId = 0;
                }

                try
                {
                    // retrieve the host connection details
                    var connection = _enterpriseClient.GetSessionConnectionDetails(_enterpriseSession.SessionID, hostId, _enterpriseSession.SessionKey);
                    if (connection == null)
                    {
                        System.Diagnostics.Trace.TraceInformation("Unable to retrieve host {0} connection details (invalid host or one time session url already used?)", hostId);
                        return false;
                    }
                    loginHostName = connection.HostName;
                    loginHostType = connection.HostType;
                    loginProtocol = connection.Protocol;
                    loginServer = !string.IsNullOrEmpty(connection.HostAddress) ? connection.HostAddress : connection.HostName;
                    loginVMGuid = connection.VMGuid;
                    loginVMEnhancedMode = connection.VMEnhancedMode;
                    loginDomain = connection.Domain;
                    loginUser = connection.Username;
                    loginPassword = RDPCryptoHelper.DecryptPassword(connection.Password);
                    startProgram = connection.StartRemoteProgram;
                }
                catch (Exception exc)
                {
                    System.Diagnostics.Trace.TraceError("Failed to retrieve host {0} connection details ({1})", hostId, exc);
                    return false;
                }
            }

            // remove any active remote session (disconnected?)
            if (RemoteSession != null)
            {
                // unset the remote session for the current http session
                Session[HttpSessionStateVariables.RemoteSession.ToString()] = null;
                RemoteSession = null;
            }

            // create a new remote session
            try
            {
                // create the remote session
                RemoteSession = new RemoteSession(
                    Guid.NewGuid(),
                    RemoteSessionState.NotConnected,
                    loginHostName,
                    loginHostType,
                    loginProtocol,
                    loginServer,
                    loginVMGuid,
                    loginVMEnhancedMode,
                    loginDomain,
                    loginUser,
                    loginPassword,
                    int.Parse(width.Value),
                    int.Parse(height.Value),
                    startProgram,
                    _allowRemoteClipboard,
                    _allowFileTransfer,
                    _allowPrintDownload,
                    _allowSessionSharing,
                    Session.SessionID
                );

                // bind the remote session to the current http session
                Session[HttpSessionStateVariables.RemoteSession.ToString()] = RemoteSession;
            }
            catch (Exception exc)
            {
                System.Diagnostics.Trace.TraceError("Failed to create remote session ({0})", exc);
                RemoteSession = null;
            }

            // connect it
            if (RemoteSession != null)
            {
                try
                {
                    // update the remote session state
                    RemoteSession.State = RemoteSessionState.Connecting;

                    // create pipes for the web gateway and the host client to talk
                    RemoteSession.Manager.Pipes.CreatePipes();

                    // the host client does connect the pipes when it starts; when it stops (either because it was closed, crashed or because the remote session had ended), pipes are released
                    // as the process command line can be displayed into the task manager / process explorer, the connection settings (including user credentials) are now passed to the host client through the inputs pipe
                    // use http://technet.microsoft.com/en-us/sysinternals/dd581625 to track the existing pipes
                    RemoteSession.Manager.HostClient.StartProcess(
                        RemoteSession.Id,
                        RemoteSession.HostType,
                        RemoteSession.SecurityProtocol,
                        RemoteSession.ServerAddress,
                        RemoteSession.VMGuid,
                        RemoteSession.UserDomain,
                        RemoteSession.UserName,
                        RemoteSession.StartProgram,
                        RemoteSession.ClientWidth,
                        RemoteSession.ClientHeight,
                        RemoteSession.AllowRemoteClipboard,
                        RemoteSession.AllowPrintDownload);
                }
                catch (Exception exc)
                {
                    System.Diagnostics.Trace.TraceError("Failed to connect the remote session {0} ({1})", RemoteSession.Id, exc);
                    connectError.InnerText = "Failed to connect! ensure myrtille services are running";
                    return false;
                }
            }
            else
            {
                connectError.InnerText = "Failed to create remote session!";
                return false;
            }

            return true;
        }

        /// <summary>
        /// disconnect the remote server
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void DisconnectButtonClick(
            object sender,
            EventArgs e)
        {
            if (!_authorizedRequest)
                return;

            // disconnect the active remote session, if any and connecting/connected
            if (RemoteSession != null && (RemoteSession.State == RemoteSessionState.Connecting || RemoteSession.State == RemoteSessionState.Connected))
            {
                try
                {
                    // prevent the remote session from being disconnected by a guest
                    if (Session.SessionID.Equals(RemoteSession.OwnerSessionID))
                    {
                        RemoteSession.State = RemoteSessionState.Disconnecting;
                        RemoteSession.Manager.SendCommand(RemoteSessionCommand.CloseClient);
                    }
                    else
                    {
                        Session[HttpSessionStateVariables.RemoteSession.ToString()] = null;
                        RemoteSession = null;
                    }

                    // logout the enterprise session if single use only
                    if (_enterpriseSession != null && _enterpriseSession.SingleUseConnection)
                    {
                        LogoutButtonClick(this, EventArgs.Empty);
                    }

                    // enterprise mode: redirect to the hosts list
                    // standard mode: redirect to the login screen
                    Response.Redirect("~/", true);
                }
                catch (ThreadAbortException)
                {
                    // occurs because the response is ended after redirect
                }
                catch (Exception exc)
                {
                    System.Diagnostics.Trace.TraceError("Failed to disconnect the remote session {0} ({1})", RemoteSession.Id, exc);
                }
            }
        }

        #region enterprise mode

        /// <summary>
        /// create an enterprise session from a one time url
        /// </summary>
        private void CreateEnterpriseSessionFromUrl()
        {
            try
            {
                // create enterprise session from querystring params
                _enterpriseSession = new EnterpriseSession
                {
                    IsAdmin = false,    // simple host connection only (no hosts management)
                    SessionID = Request["SI"],
                    SessionKey = Request["SK"],
                    SingleUseConnection = true
                };

                // bind the enterprise session to the current http session
                Session[HttpSessionStateVariables.EnterpriseSession.ToString()] = _enterpriseSession;

                // session fixation protection
                if (_cookielessSession)
                {
                    // generate a new http session id
                    HttpSessionHelper.RegenerateSessionId();
                }
            }
            catch (Exception exc)
            {
                System.Diagnostics.Trace.TraceError("Failed to create enterprise session from url ({0})", exc);
            }
        }

        /// <summary>
        /// authenticate the user against the enterprise active directory and list the servers available to the user
        /// </summary>
        private void CreateEnterpriseSessionFromLogin()
        {
            try
            {
                // authenticate the user against the enterprise active directory
                _enterpriseSession = _enterpriseClient.Authenticate(user.Value, password.Value);

                if (_enterpriseSession.AuthenticationErrorCode != EnterpriseAuthenticationErrorCode.NONE)
                {
                    if (_enterpriseSession.AuthenticationErrorCode == EnterpriseAuthenticationErrorCode.PASSWORD_EXPIRED)
                    {
                        ClientScript.RegisterClientScriptBlock(GetType(), Guid.NewGuid().ToString(), string.Format("openPopup('changePasswordPopup', 'EnterpriseChangePassword.aspx?userName={0}');", user.Value), true);
                    }
                    else
                    {
                        connectError.InnerText = EnterpriseAuthenticationErrorHelper.GetErrorDescription(_enterpriseSession.AuthenticationErrorCode);
                    }
                    UpdateControls();
                    return;
                }

                // bind the enterprise session to the current http session
                Session[HttpSessionStateVariables.EnterpriseSession.ToString()] = _enterpriseSession;

                // session fixation protection
                if (_cookielessSession)
                {
                    // generate a new http session id
                    HttpSessionHelper.RegenerateSessionId();
                }

                // redirect to the hosts list
                Response.Redirect("~/", true);
            }
            catch (ThreadAbortException)
            {
                // occurs because the response is ended after redirect
            }
            catch (Exception exc)
            {
                System.Diagnostics.Trace.TraceError("Failed to create enterprise session from login ({0})", exc);
            }
        }

        /// <summary>
        /// populate the enterprise session hosts list
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void hostsList_ItemDataBound(
            object sender,
            RepeaterItemEventArgs e)
        {
            try
            {
                var host = e.Item.DataItem as EnterpriseHost;

                if (host.PromptForCredentials)
                {
                    var hostLink = e.Item.FindControl("hostLink") as HtmlAnchor;
                    hostLink.HRef = null;
                    hostLink.Attributes["onclick"] = string.Format("openPopup('editCredentialPopup', 'CredentialsPrompt.aspx?hostId={0}');", host.HostID);
                    hostLink.Attributes["class"] = "hostLink";
                }
                else
                {
                    var hostLink = e.Item.FindControl("hostLink") as HtmlAnchor;
                    hostLink.HRef = string.Format("?SD={0}&__EVENTTARGET=&__EVENTARGUMENT=&connect=Connect%21", host.HostID);
                    hostLink.Attributes["class"] = "hostLink";
                }

                var hostName = e.Item.FindControl("hostName") as HtmlGenericControl;
                hostName.InnerText = (_enterpriseSession.IsAdmin ? "Edit " : string.Empty) + host.HostName;
                if (_enterpriseSession.IsAdmin)
                {
                    hostName.Attributes["class"] = "hostName";
                    hostName.Attributes["title"] = "edit";
                    hostName.Attributes["onclick"] = string.Format("openPopup('editHostPopup', 'EditHost.aspx?hostId={0}');", host.HostID);
                }
            }
            catch (Exception exc)
            {
                System.Diagnostics.Trace.TraceError("Failed to populate hosts for the enterprise session {0} ({1})", _enterpriseSession.SessionID, exc);
            }
        }

        /// <summary>
        /// logout the enterprise session
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void LogoutButtonClick(
            object sender,
            EventArgs e)
        {
            if (!_authorizedRequest)
                return;

            if (_enterpriseSession == null)
                return;

            try
            {
                // logout the enterprise session
                _enterpriseClient.Logout(_enterpriseSession.SessionID);
                Session[HttpSessionStateVariables.EnterpriseSession.ToString()] = null;
                _enterpriseSession = null;

                // redirect to the login screen
                Response.Redirect("~/", true);
            }
            catch (ThreadAbortException)
            {
                // occurs because the response is ended after redirect
            }
            catch (Exception exc)
            {
                System.Diagnostics.Trace.TraceError("Failed to logout the enterprise session {0} ({1})", _enterpriseSession.SessionID, exc);
            }
        }

        #endregion

        #region session sharing

        /// <summary>
        /// retrieve a shared remote session
        /// </summary>
        /// <param name="guestGuid">guest guid</param>
        /// <returns></returns>
        private RemoteSession GetSharedRemoteSession(
            string guestGuid)
        {
            RemoteSession remoteSession = null;

            try
            {
                Application.Lock();

                var sharedSessions = (IDictionary<string, RemoteSession>)Application[HttpApplicationStateVariables.SharedRemoteSessions.ToString()];
                if (sharedSessions.ContainsKey(guestGuid))
                {
                    remoteSession = sharedSessions[guestGuid];
                    sharedSessions.Remove(guestGuid);
                }
            }
            catch (Exception exc)
            {
                System.Diagnostics.Trace.TraceError("Failed to retrieve the shared remote session ({0})", exc);
            }
            finally
            {
                Application.UnLock();
            }

            return remoteSession;
        }

        #endregion
    }
}