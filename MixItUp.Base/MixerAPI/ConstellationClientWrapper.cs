﻿using Mixer.Base.Clients;
using Mixer.Base.Model.Channel;
using Mixer.Base.Model.Chat;
using Mixer.Base.Model.Constellation;
using Mixer.Base.Model.Patronage;
using Mixer.Base.Model.Skills;
using Mixer.Base.Model.User;
using Mixer.Base.Util;
using MixItUp.Base.Commands;
using MixItUp.Base.Model.Skill;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.User;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.MixerAPI
{
    public class ConstellationClientWrapper : MixerWebSocketWrapper
    {
        public static ConstellationEventType ChannelUpdateEvent { get { return new ConstellationEventType(ConstellationEventTypeEnum.channel__id__update, ChannelSession.Channel.id); } }
        public static ConstellationEventType ChannelFollowEvent { get { return new ConstellationEventType(ConstellationEventTypeEnum.channel__id__followed, ChannelSession.Channel.id); } }
        public static ConstellationEventType ChannelHostedEvent { get { return new ConstellationEventType(ConstellationEventTypeEnum.channel__id__hosted, ChannelSession.Channel.id); } }
        public static ConstellationEventType ChannelSubscribedEvent { get { return new ConstellationEventType(ConstellationEventTypeEnum.channel__id__subscribed, ChannelSession.Channel.id); } }
        public static ConstellationEventType ChannelResubscribedEvent { get { return new ConstellationEventType(ConstellationEventTypeEnum.channel__id__resubscribed, ChannelSession.Channel.id); } }
        public static ConstellationEventType ChannelResubscribedSharedEvent { get { return new ConstellationEventType(ConstellationEventTypeEnum.channel__id__resubShared, ChannelSession.Channel.id); } }
        public static ConstellationEventType ChannelSkillEvent { get { return new ConstellationEventType(ConstellationEventTypeEnum.channel__id__skill, ChannelSession.Channel.id); } }
        public static ConstellationEventType ChannelPatronageUpdateEvent { get { return new ConstellationEventType(ConstellationEventTypeEnum.channel__id__patronageUpdate, ChannelSession.Channel.id); } }

        private static readonly List<ConstellationEventTypeEnum> subscribedEvents = new List<ConstellationEventTypeEnum>()
        {
            ConstellationEventTypeEnum.channel__id__followed, ConstellationEventTypeEnum.channel__id__hosted, ConstellationEventTypeEnum.channel__id__subscribed,
            ConstellationEventTypeEnum.channel__id__resubscribed, ConstellationEventTypeEnum.channel__id__resubShared, ConstellationEventTypeEnum.channel__id__update,
            ConstellationEventTypeEnum.channel__id__skill, ConstellationEventTypeEnum.channel__id__patronageUpdate
        };

        public event EventHandler<ConstellationLiveEventModel> OnEventOccurred;

        public event EventHandler<UserViewModel> OnFollowOccurred;
        public event EventHandler<UserViewModel> OnUnfollowOccurred;
        public event EventHandler<Tuple<UserViewModel, int>> OnHostedOccurred;
        public event EventHandler<UserViewModel> OnSubscribedOccurred;
        public event EventHandler<Tuple<UserViewModel, int>> OnResubscribedOccurred;
        public event EventHandler<Tuple<UserViewModel, SkillInstanceModel>> OnSkillOccurred;
        public event EventHandler<PatronageStatusModel> OnPatronageUpdateOccurred;

        public ConstellationClient Client { get; private set; }

        private Dictionary<string, HashSet<uint>> userEventTracking = new Dictionary<string, HashSet<uint>>();

        private List<PatronageMilestoneModel> allPatronageMilestones = new List<PatronageMilestoneModel>();
        private List<PatronageMilestoneModel> remainingPatronageMilestones = new List<PatronageMilestoneModel>();
        private SemaphoreSlim patronageMilestonesSemaphore = new SemaphoreSlim(1);

        private SkillCatalogModel skillCatalog;
        private Dictionary<Guid, SkillModel> availableSkills = new Dictionary<Guid, SkillModel>();

        public ConstellationClientWrapper() { }

        public async Task<bool> Connect()
        {
            PatronageStatusModel patronageStatus = await ChannelSession.Connection.GetPatronageStatus(ChannelSession.Channel);
            if (patronageStatus != null)
            {
                PatronagePeriodModel patronagePeriod = await ChannelSession.Connection.GetPatronagePeriod(patronageStatus);
                if (patronagePeriod != null)
                {
                    this.allPatronageMilestones = new List<PatronageMilestoneModel>(patronagePeriod.milestoneGroups.SelectMany(mg => mg.milestones));
                    this.remainingPatronageMilestones = new List<PatronageMilestoneModel>(this.allPatronageMilestones.Where(m => m.target > patronageStatus.patronageEarned));
                }
            }

            this.skillCatalog = await ChannelSession.Connection.GetSkillCatalog(ChannelSession.Channel);

            // Hacky workaround until auth issue is fixed for Skill Catalog
            this.skillCatalog = await SerializerHelper.DeserializeFromFile<SkillCatalogModel>("SkillsCatalogData.txt");

            if (this.skillCatalog != null)
            {
                this.availableSkills = new Dictionary<Guid, SkillModel>(this.skillCatalog.skills.ToDictionary(s => s.id, s => s));
            }

            return await this.AttemptConnect();
        }

        public async Task SubscribeToEvents(IEnumerable<ConstellationEventType> events) { await this.RunAsync(this.Client.SubscribeToEvents(events)); }

        public async Task UnsubscribeToEvents(IEnumerable<ConstellationEventType> events) { await this.RunAsync(this.Client.UnsubscribeToEvents(events)); }

        public async Task Disconnect()
        {
            if (this.Client != null)
            {
                this.Client.OnDisconnectOccurred -= ConstellationClient_OnDisconnectOccurred;
                if (ChannelSession.Settings.DiagnosticLogging)
                {
                    this.Client.OnPacketSentOccurred -= WebSocketClient_OnPacketSentOccurred;
                    this.Client.OnMethodOccurred -= WebSocketClient_OnMethodOccurred;
                    this.Client.OnReplyOccurred -= WebSocketClient_OnReplyOccurred;
                    this.Client.OnEventOccurred -= WebSocketClient_OnEventOccurred;
                }
                this.Client.OnSubscribedEventOccurred -= ConstellationClient_OnSubscribedEventOccurred;

                await this.RunAsync(this.Client.Disconnect());

                this.backgroundThreadCancellationTokenSource.Cancel();
            }
            this.Client = null;
        }

        public bool CanUserRunEvent(UserViewModel user, string eventName)
        {
            return (!this.userEventTracking.ContainsKey(eventName) || !this.userEventTracking[eventName].Contains(user.ID));
        }

        public void LogUserRunEvent(UserViewModel user, string eventName)
        {
            if (!this.userEventTracking.ContainsKey(eventName))
            {
                this.userEventTracking[eventName] = new HashSet<uint>();
            }

            if (user != null)
            {
                this.userEventTracking[eventName].Add(user.ID);
            }
        }

        public EventCommand FindMatchingEventCommand(string eventDetails)
        {
            foreach (EventCommand command in ChannelSession.Settings.EventCommands)
            {
                if (command.MatchesEvent(eventDetails))
                {
                    return command;
                }
            }
            return null;
        }

        public async Task RunEventCommand(EventCommand command, UserViewModel user, Dictionary<string, string> extraSpecialIdentifiers = null)
        {
            if (command != null)
            {
                if (user != null)
                {
                    await command.Perform(user, arguments: null, extraSpecialIdentifiers: extraSpecialIdentifiers);
                }
                else
                {
                    await command.Perform(await ChannelSession.GetCurrentUser(), arguments: null, extraSpecialIdentifiers: extraSpecialIdentifiers);
                }
            }
        }

        protected override async Task<bool> ConnectInternal()
        {
            this.Client = await this.RunAsync(ConstellationClient.Create(ChannelSession.Connection.Connection));
            if (this.Client != null)
            {
                this.backgroundThreadCancellationTokenSource = new CancellationTokenSource();

                if (await this.RunAsync(this.Client.Connect()))
                {
                    this.Client.OnDisconnectOccurred += ConstellationClient_OnDisconnectOccurred;
                    if (ChannelSession.Settings.DiagnosticLogging)
                    {
                        this.Client.OnPacketSentOccurred += WebSocketClient_OnPacketSentOccurred;
                        this.Client.OnMethodOccurred += WebSocketClient_OnMethodOccurred;
                        this.Client.OnReplyOccurred += WebSocketClient_OnReplyOccurred;
                        this.Client.OnEventOccurred += WebSocketClient_OnEventOccurred;
                    }
                    this.Client.OnSubscribedEventOccurred += ConstellationClient_OnSubscribedEventOccurred;

                    await this.SubscribeToEvents(ConstellationClientWrapper.subscribedEvents.Select(e => new ConstellationEventType(e, ChannelSession.Channel.id)));

                    return true;
                }
            }
            return false;
        }

        private async void ConstellationClient_OnSubscribedEventOccurred(object sender, ConstellationLiveEventModel e)
        {
            try
            {
                UserViewModel user = null;
                bool? followed = null;
                ChannelModel channel = null;

                JToken payloadToken;
                if (e.payload.TryGetValue("user", out payloadToken))
                {
                    user = new UserViewModel(payloadToken.ToObject<UserModel>());

                    JToken subscribeStartToken;
                    if (e.payload.TryGetValue("since", out subscribeStartToken))
                    {
                        user.SubscribeDate = subscribeStartToken.ToObject<DateTimeOffset>();
                    }

                    if (e.payload.TryGetValue("following", out JToken followedToken))
                    {
                        followed = (bool)followedToken;
                    }
                }
                else if (e.payload.TryGetValue("hoster", out payloadToken))
                {
                    channel = payloadToken.ToObject<ChannelModel>();
                    user = new UserViewModel(channel.userId, channel.token);
                }

                if (e.channel.Equals(ConstellationClientWrapper.ChannelUpdateEvent.ToString()))
                {
                    if (e.payload["online"] != null)
                    {
                        bool online = e.payload["online"].ToObject<bool>();
                        user = await ChannelSession.GetCurrentUser();
                        if (online)
                        {
                            if (this.CanUserRunEvent(user, EnumHelper.GetEnumName(OtherEventTypeEnum.MixerChannelStreamStart)))
                            {
                                this.LogUserRunEvent(user, EnumHelper.GetEnumName(OtherEventTypeEnum.MixerChannelStreamStart));
                                await this.RunEventCommand(this.FindMatchingEventCommand(EnumHelper.GetEnumName(OtherEventTypeEnum.MixerChannelStreamStart)), user);
                            }
                        }
                        else
                        {
                            if (this.CanUserRunEvent(user, EnumHelper.GetEnumName(OtherEventTypeEnum.MixerChannelStreamStop)))
                            {
                                this.LogUserRunEvent(user, EnumHelper.GetEnumName(OtherEventTypeEnum.MixerChannelStreamStop));
                                await this.RunEventCommand(this.FindMatchingEventCommand(EnumHelper.GetEnumName(OtherEventTypeEnum.MixerChannelStreamStop)), user);
                            }
                        }
                    }
                }
                else if (e.channel.Equals(ConstellationClientWrapper.ChannelFollowEvent.ToString()))
                {
                    if (followed.GetValueOrDefault())
                    {
                        if (this.CanUserRunEvent(user, ConstellationClientWrapper.ChannelFollowEvent.ToString()))
                        {
                            this.LogUserRunEvent(user, ConstellationClientWrapper.ChannelFollowEvent.ToString());

                            foreach (UserCurrencyViewModel currency in ChannelSession.Settings.Currencies.Values)
                            {
                                user.Data.AddCurrencyAmount(currency, currency.OnFollowBonus);
                            }

                            if (this.OnFollowOccurred != null)
                            {
                                this.OnFollowOccurred(this, user);
                            }

                            await this.RunEventCommand(this.FindMatchingEventCommand(e.channel), user);
                        }
                    }
                    else
                    {
                        if (this.CanUserRunEvent(user, EnumHelper.GetEnumName(OtherEventTypeEnum.MixerUserUnfollow)))
                        {
                            this.LogUserRunEvent(user, EnumHelper.GetEnumName(OtherEventTypeEnum.MixerUserUnfollow));
                            await this.RunEventCommand(this.FindMatchingEventCommand(EnumHelper.GetEnumName(OtherEventTypeEnum.MixerUserUnfollow)), user);
                        }

                        if (this.OnUnfollowOccurred != null)
                        {
                            this.OnUnfollowOccurred(this, user);
                        }
                    }
                }
                else if (e.channel.Equals(ConstellationClientWrapper.ChannelHostedEvent.ToString()))
                {
                    if (this.CanUserRunEvent(user, ConstellationClientWrapper.ChannelHostedEvent.ToString()))
                    {
                        this.LogUserRunEvent(user, ConstellationClientWrapper.ChannelHostedEvent.ToString());

                        int viewerCount = 0;
                        if (channel != null)
                        {
                            viewerCount = (int)channel.viewersCurrent;
                        }

                        foreach (UserCurrencyViewModel currency in ChannelSession.Settings.Currencies.Values)
                        {
                            user.Data.AddCurrencyAmount(currency, currency.OnHostBonus);
                        }

                        if (this.OnHostedOccurred != null)
                        {
                            this.OnHostedOccurred(this, new Tuple<UserViewModel, int>(user, viewerCount));
                        }

                        EventCommand command = this.FindMatchingEventCommand(e.channel);
                        if (command != null)
                        {
                            Dictionary<string, string> specialIdentifiers = new Dictionary<string, string>() { { "hostviewercount", viewerCount.ToString() } };
                            await this.RunEventCommand(command, user, specialIdentifiers);
                        }
                    }
                }
                else if (e.channel.Equals(ConstellationClientWrapper.ChannelSubscribedEvent.ToString()))
                {
                    if (this.CanUserRunEvent(user, ConstellationClientWrapper.ChannelSubscribedEvent.ToString()))
                    {
                        this.LogUserRunEvent(user, ConstellationClientWrapper.ChannelSubscribedEvent.ToString());

                        user.SubscribeDate = DateTimeOffset.Now;
                        foreach (UserCurrencyViewModel currency in ChannelSession.Settings.Currencies.Values)
                        {
                            user.Data.AddCurrencyAmount(currency, currency.OnSubscribeBonus);
                        }

                        if (this.OnSubscribedOccurred != null)
                        {
                            this.OnSubscribedOccurred(this, user);
                        }

                        await this.RunEventCommand(this.FindMatchingEventCommand(e.channel), user);
                    }
                }
                else if (e.channel.Equals(ConstellationClientWrapper.ChannelResubscribedEvent.ToString()) || e.channel.Equals(ConstellationClientWrapper.ChannelResubscribedSharedEvent.ToString()))
                {
                    if (this.CanUserRunEvent(user, ConstellationClientWrapper.ChannelResubscribedEvent.ToString()))
                    {
                        this.LogUserRunEvent(user, ConstellationClientWrapper.ChannelResubscribedEvent.ToString());

                        foreach (UserCurrencyViewModel currency in ChannelSession.Settings.Currencies.Values)
                        {
                            user.Data.AddCurrencyAmount(currency, currency.OnSubscribeBonus);
                        }

                        int resubMonths = 0;
                        if (e.payload.TryGetValue("totalMonths", out JToken resubMonthsToken))
                        {
                            resubMonths = (int)resubMonthsToken;
                        }

                        if (this.OnResubscribedOccurred != null)
                        {
                            this.OnResubscribedOccurred(this, new Tuple<UserViewModel, int>(user, resubMonths));
                        }

                        Dictionary<string, string> specialIdentifiers = new Dictionary<string, string>() { { "usersubmonths", resubMonths.ToString() } };
                        await this.RunEventCommand(this.FindMatchingEventCommand(ConstellationClientWrapper.ChannelResubscribedEvent.ToString()), user, specialIdentifiers);
                    }
                }
                else if (e.channel.Equals(ConstellationClientWrapper.ChannelSkillEvent.ToString()))
                {
                    if (e.payload["triggeringUserId"] != null && e.payload["skillId"] != null)
                    {
                        uint userID = e.payload["triggeringUserId"].ToObject<uint>();
                        user = await ChannelSession.ActiveUsers.GetUserByID(userID);
                        if (user != null)
                        {
                            user = new UserViewModel(await ChannelSession.Connection.GetUser(userID));
                        }

                        Guid skillID = e.payload["skillId"].ToObject<Guid>();
                        SkillModel skill = null;
                        if (this.availableSkills.ContainsKey(skillID))
                        {
                            skill = this.availableSkills[skillID];
                        }

                        if (user != null && skill != null)
                        {
                            JObject manifest = (JObject)e.payload["manifest"];
                            JObject parameters = (JObject)e.payload["parameters"];

                            SkillInstanceModel skillInstance = new SkillInstanceModel(skill, manifest, parameters);

                            if (this.OnSkillOccurred != null)
                            {
                                this.OnSkillOccurred(this, new Tuple<UserViewModel, SkillInstanceModel>(user, skillInstance));
                            }

                            GlobalEvents.SkillOccurred(new Tuple<UserViewModel, SkillInstanceModel>(user, skillInstance));
                        }
                    }
                }
                else if (e.channel.Equals(ConstellationClientWrapper.ChannelPatronageUpdateEvent.ToString()))
                {
                    PatronageStatusModel patronageStatus = e.payload.ToObject<PatronageStatusModel>();
                    if (patronageStatus != null)
                    {
                        if (this.OnPatronageUpdateOccurred != null)
                        {
                            this.OnPatronageUpdateOccurred(this, patronageStatus);
                        }

                        bool milestoneUpdateOccurred = await this.patronageMilestonesSemaphore.WaitAndRelease(() =>
                        {
                            return Task.FromResult(this.remainingPatronageMilestones.RemoveAll(m => m.target <= patronageStatus.patronageEarned) > 0);
                        });

                        if (milestoneUpdateOccurred)
                        {
                            PatronageMilestoneModel milestoneReached = this.allPatronageMilestones.OrderByDescending(m => m.target).FirstOrDefault(m => m.target <= patronageStatus.patronageEarned);
                            if (milestoneReached != null)
                            {
                                double milestoneReward = Math.Round(((double)milestoneReached.reward) / 100.0, 2);
                                Dictionary<string, string> specialIdentifiers = new Dictionary<string, string>()
                                {
                                    { SpecialIdentifierStringBuilder.MilestoneSpecialIdentifierHeader + "amount", milestoneReached.target.ToString() },
                                    { SpecialIdentifierStringBuilder.MilestoneSpecialIdentifierHeader + "reward", string.Format("{0:C}", milestoneReward) },
                                };
                                await this.RunEventCommand(this.FindMatchingEventCommand(EnumHelper.GetEnumName(OtherEventTypeEnum.MixerMilestoneReached)), await ChannelSession.GetCurrentUser(), specialIdentifiers);
                            }
                        }
                    }
                }

                if (this.OnEventOccurred != null)
                {
                    this.OnEventOccurred(this, e);
                }
            }
            catch (Exception ex) { Util.Logger.Log(ex); }
        }

        private async void ConstellationClient_OnDisconnectOccurred(object sender, WebSocketCloseStatus e)
        {
            ChannelSession.DisconnectionOccurred("Constellation");

            do
            {
                await Task.Delay(2500);
            }
            while (!await this.Connect());

            ChannelSession.ReconnectionOccurred("Constellation");
        }
    }
}
