﻿using Mixer.Base.Util;
using MixItUp.Base.ViewModel.Requirement;
using MixItUp.Base.ViewModel.User;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Commands
{
    [DataContract]
    public abstract class PermissionsCommandBase : CommandBase
    {
        [DataMember]
        public RequirementViewModel Requirements { get; set; }

        [DataMember]
        [Obsolete]
        internal UserRole Permissions { get; set; }

        [Obsolete]
        [DataMember]
        internal int Cooldown { get; set; }

        [DataMember]
        [Obsolete]
        internal CurrencyRequirementViewModel CurrencyRequirement { get; set; }

        [DataMember]
        [Obsolete]
        internal CurrencyRequirementViewModel RankRequirement { get; set; }

        public PermissionsCommandBase()
        {
            this.Requirements = new RequirementViewModel();
        }

        public PermissionsCommandBase(string name, CommandTypeEnum type, string command, RequirementViewModel requirements)
            : this(name, type, new List<string>() { command }, requirements)
        { }

        public PermissionsCommandBase(string name, CommandTypeEnum type, IEnumerable<string> commands, RequirementViewModel requirements)
            : base(name, type, commands)
        {
            this.Requirements = requirements;
        }

        [JsonIgnore]
        public string UserRoleRequirementString { get { return EnumHelper.GetEnumName(this.Requirements.UserRole); } }

        public override async Task PerformInternal(UserViewModel user, IEnumerable<string> arguments = null)
        {
            if (!await this.CheckCooldownRequirement(user) || !await this.CheckUserRoleRequirement(user) || !await this.CheckRankRequirement(user) || !await this.CheckCurrencyRequirement(user))
            {
                return;
            }
            await base.PerformInternal(user, arguments);
        }

        public async Task<bool> CheckCooldownRequirement(UserViewModel user)
        {
            if (!this.Requirements.DoesMeetCooldownRequirement(user))
            {
                await this.Requirements.Cooldown.SendCooldownNotMetWhisper(user);
                return false;
            }
            this.Requirements.UpdateCooldown(user);
            return true;
        }

        public async Task<bool> CheckUserRoleRequirement(UserViewModel user)
        {
            if (!await this.Requirements.DoesMeetUserRoleRequirement(user))
            {
                await this.Requirements.SendUserRoleNotMetWhisper(user);
                return false;
            }
            return true;
        }

        public async Task<bool> CheckRankRequirement(UserViewModel user)
        {
            if (this.Requirements.Rank != null && this.Requirements.Rank.GetCurrency() != null)
            {
                if (!this.Requirements.Rank.DoesMeetRankRequirement(user.Data))
                {
                    await this.Requirements.Rank.SendRankNotMetWhisper(user);
                    return false;
                }
            }
            return true;
        }

        public async Task<bool> CheckCurrencyRequirement(UserViewModel user)
        {
            if (this.Requirements.Currency != null && this.Requirements.Currency.GetCurrency() != null)
            {
                if (!this.Requirements.Currency.TrySubtractAmount(user.Data, this.Requirements.Currency.RequiredAmount))
                {
                    await this.Requirements.Currency.SendCurrencyNotMetWhisper(user);
                    return false;
                }
            }
            return true;
        }
    }
}
