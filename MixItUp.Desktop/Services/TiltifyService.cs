﻿using MixItUp.Base;
using MixItUp.Base.Commands;
using MixItUp.Base.Model.User;
using MixItUp.Base.Services;
using Newtonsoft.Json.Linq;
using StreamingClient.Base.Model.OAuth;
using StreamingClient.Base.Util;
using StreamingClient.Base.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Desktop.Services
{
    public class TiltifyService : OAuthServiceBase, ITiltifyService, IDisposable
    {
        private const string BaseAddress = "https://tiltify.com/api/v3/";

        public const string ClientID = "aa6b19e3f472808a632fe5a1b26b8ab37e852c123f60fb431c8a15c40df07f25";

        public const string ListeningURL = "https://localhost:8919/";
        public const string AuthorizationURL = "https://tiltify.com/oauth/authorize?client_id={0}&redirect_uri={1}&response_type=code";

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private string authorizationToken;

        private TiltifyUser user;

        public TiltifyService(string authorizationToken) : base(TiltifyService.BaseAddress) { this.authorizationToken = authorizationToken; }

        public TiltifyService(OAuthTokenModel token) : base(TiltifyService.BaseAddress, token) { }

        public async Task<bool> Connect()
        {
            if (this.token != null)
            {
                try
                {
                    if (await this.InitializeInternal())
                    {
                        return true;
                    }
                }
                catch (Exception ex) { Logger.Log(ex); }
            }

            if (!string.IsNullOrEmpty(this.authorizationToken))
            {
                try
                {
                    JObject payload = new JObject();
                    payload["grant_type"] = "authorization_code";
                    payload["client_id"] = TiltifyService.ClientID;
                    payload["client_secret"] = ChannelSession.SecretManager.GetSecret("TiltifySecret");
                    payload["code"] = this.authorizationToken;
                    payload["redirect_uri"] = TiltifyService.ListeningURL;

                    this.token = await this.PostAsync<OAuthTokenModel>("https://tiltify.com/oauth/token", AdvancedHttpClient.CreateContentFromObject(payload), autoRefreshToken: false);
                    if (this.token != null)
                    {
                        token.authorizationCode = this.authorizationToken;
                        token.AcquiredDateTime = DateTimeOffset.Now;
                        token.expiresIn = int.MaxValue;

                        return await this.InitializeInternal();
                    }
                }
                catch (Exception ex) { Logger.Log(ex); }
            }
            return false;
        }

        public Task Disconnect()
        {
            this.token = null;
            this.cancellationTokenSource.Cancel();
            return Task.FromResult(0);
        }

        public async Task<TiltifyUser> GetUser()
        {
            try
            {
                TiltifyResult result = await this.GetAsync<TiltifyResult>("user");
                return result.Data.ToObject<TiltifyUser>();
            }
            catch (Exception ex) { Logger.Log(ex); }
            return null;
        }

        public async Task<IEnumerable<TiltifyCampaign>> GetUserCampaigns(TiltifyUser user)
        {
            List<TiltifyCampaign> results = new List<TiltifyCampaign>();
            try
            {
                TiltifyResultArray result = await this.GetAsync<TiltifyResultArray>("users/" + user.ID.ToString() + "/campaigns");
                foreach (JToken token in result.Data)
                {
                    results.Add(token.ToObject<TiltifyCampaign>());
                }
            }
            catch (Exception ex) { Logger.Log(ex); }
            return results;
        }

        public async Task<IEnumerable<TiltifyTeam>> GetUserTeams(TiltifyUser user)
        {
            List<TiltifyTeam> results = new List<TiltifyTeam>();
            try
            {
                TiltifyResultArray result = await this.GetAsync<TiltifyResultArray>("users/" + user.ID.ToString() + "/teams");
                foreach (JToken token in result.Data)
                {
                    results.Add(token.ToObject<TiltifyTeam>());
                }
            }
            catch (Exception ex) { Logger.Log(ex); }
            return results;
        }

        public async Task<IEnumerable<TiltifyCampaign>> GetTeamCampaigns(TiltifyTeam team)
        {
            List<TiltifyCampaign> results = new List<TiltifyCampaign>();
            try
            {
                TiltifyResultArray result = await this.GetAsync<TiltifyResultArray>("teams/" + team.ID.ToString() + "/campaigns");
                foreach (JToken token in result.Data)
                {
                    results.Add(token.ToObject<TiltifyCampaign>());
                }
            }
            catch (Exception ex) { Logger.Log(ex); }
            return results;
        }

        public async Task<IEnumerable<TiltifyDonation>> GetCampaignDonations(TiltifyCampaign campaign)
        {
            List<TiltifyDonation> results = new List<TiltifyDonation>();
            try
            {
                TiltifyResultArray result = await this.GetAsync<TiltifyResultArray>("campaigns/" + campaign.ID.ToString() + "/donations");
                foreach (JToken token in result.Data)
                {
                    results.Add(token.ToObject<TiltifyDonation>());
                }
            }
            catch (Exception ex) { Logger.Log(ex); }
            return results;
        }

        protected override Task RefreshOAuthToken()
        {
            return Task.FromResult(0);
        }

        private async Task<bool> InitializeInternal()
        {
            this.cancellationTokenSource = new CancellationTokenSource();

            this.user = await this.GetUser();
            if (this.user != null)
            {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                Task.Run(this.BackgroundDonationCheck, this.cancellationTokenSource.Token);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

                return true;
            }
            return false;
        }

        private async Task BackgroundDonationCheck()
        {
            int currentCampaign = 0;
            TiltifyCampaign campaign = null;
            Dictionary<int, TiltifyDonation> donationsReceived = new Dictionary<int, TiltifyDonation>();

            while (!this.cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    if (ChannelSession.Settings.TiltifyCampaign != currentCampaign)
                    {
                        currentCampaign = ChannelSession.Settings.TiltifyCampaign;
                        donationsReceived.Clear();

                        IEnumerable<TiltifyCampaign> campaigns = await this.GetUserCampaigns(this.user);
                        campaign = campaigns.FirstOrDefault(c => c.ID.Equals(currentCampaign));
                        if (campaign != null)
                        {
                            foreach (TiltifyDonation donation in await this.GetCampaignDonations(campaign))
                            {
                                donationsReceived[donation.ID] = donation;
                            }
                        }
                    }

                    if (campaign != null)
                    {
                        foreach (TiltifyDonation tDonation in await this.GetCampaignDonations(campaign))
                        {
                            if (!donationsReceived.ContainsKey(tDonation.ID))
                            {
                                donationsReceived[tDonation.ID] = tDonation;
                                UserDonationModel donation = tDonation.ToGenericDonation();
                                await EventCommand.ProcessDonationEventCommand(donation, OtherEventTypeEnum.TiltifyDonation);
                            }
                        }
                    }
                }
                catch (Exception ex) { Logger.Log(ex); }

                await Task.Delay(10000);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // Dispose managed state (managed objects).
                    this.cancellationTokenSource.Dispose();
                }

                // Free unmanaged resources (unmanaged objects) and override a finalizer below.
                // Set large fields to null.

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
