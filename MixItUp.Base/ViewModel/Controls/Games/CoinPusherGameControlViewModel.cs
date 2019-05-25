﻿using MixItUp.Base.Commands;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Requirement;
using MixItUp.Base.ViewModel.User;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.ViewModel.Controls.Games
{
    public class CoinPusherGameControlViewModel : GamesControlViewModelBase
    {
        public string MinimumAmountForPayoutString
        {
            get { return this.MinimumAmountForPayout.ToString(); }
            set
            {
                if (!string.IsNullOrEmpty(value) && int.TryParse(value, out int intValue) && intValue > 0)
                {
                    this.MinimumAmountForPayout = intValue;
                }
                else
                {
                    this.MinimumAmountForPayout = 0;
                }
                this.NotifyPropertyChanged();
            }
        }
        public int MinimumAmountForPayout { get; set; } = 2500;

        public string PayoutProbabilityString
        {
            get { return this.PayoutProbability.ToString(); }
            set
            {
                if (!string.IsNullOrEmpty(value) && int.TryParse(value, out int intValue) && intValue > 0)
                {
                    this.PayoutProbability = intValue;
                }
                else
                {
                    this.PayoutProbability = 0;
                }
                this.NotifyPropertyChanged();
            }
        }
        public int PayoutProbability { get; set; } = 10;

        public string PayoutPercentageMinimumString
        {
            get { return this.PayoutPercentageMinimum.ToString(); }
            set
            {
                if (!string.IsNullOrEmpty(value) && double.TryParse(value, out double intValue) && intValue > 0)
                {
                    this.PayoutPercentageMinimum = intValue;
                }
                else
                {
                    this.PayoutPercentageMinimum = 0;
                }
                this.NotifyPropertyChanged();
            }
        }
        public double PayoutPercentageMinimum { get; set; } = 30;

        public string PayoutPercentageMaximumString
        {
            get { return this.PayoutPercentageMaximum.ToString(); }
            set
            {
                if (!string.IsNullOrEmpty(value) && double.TryParse(value, out double intValue) && intValue > 0)
                {
                    this.PayoutPercentageMaximum = intValue;
                }
                else
                {
                    this.PayoutPercentageMaximum = 0;
                }
                this.NotifyPropertyChanged();
            }
        }
        public double PayoutPercentageMaximum { get; set; } = 70;

        public string StatusArgument { get; set; } = "status";
        public CustomCommand StatusCommand { get; set; }
        public CustomCommand NoPayoutCommand { get; set; }
        public CustomCommand PayoutCommand { get; set; }

        private CoinPusherGameCommand existingCommand;

        public CoinPusherGameControlViewModel(UserCurrencyViewModel currency)
        {
            this.StatusCommand = this.CreateBasicChatCommand("After spending a few minutes, you count $gametotalamount " + currency.Name + " inside the machine.");
            this.NoPayoutCommand = this.CreateBasicChatCommand("@$username drops their coins into the machine...and nothing happens. All $gametotalamount " + currency.Name + " stares back at you.");
            this.PayoutCommand = this.CreateBasicChatCommand("@$username drops their coins into the machine...and hits the jackpot, walking away with $gamepayout " + currency.Name + "!");
        }

        public CoinPusherGameControlViewModel(CoinPusherGameCommand command)
        {
            this.existingCommand = command;

            this.MinimumAmountForPayout = this.existingCommand.MinimumAmountForPayout;
            this.PayoutProbability = this.existingCommand.PayoutProbability;
            this.PayoutPercentageMinimum = this.existingCommand.PayoutPercentageMinimum;
            this.PayoutPercentageMaximum = this.existingCommand.PayoutPercentageMaximum;

            this.StatusArgument = this.existingCommand.StatusArgument;
            this.StatusCommand = this.existingCommand.StatusCommand;
            this.NoPayoutCommand = this.existingCommand.NoPayoutCommand;
            this.PayoutCommand = this.existingCommand.PayoutCommand;
        }

        public override void SaveGameCommand(string name, IEnumerable<string> triggers, RequirementViewModel requirements)
        {
            GameCommandBase newCommand = new CoinPusherGameCommand(name, triggers, requirements, this.StatusArgument.ToLower(), this.MinimumAmountForPayout,
                this.PayoutProbability, this.PayoutPercentageMinimum, this.PayoutPercentageMaximum, this.StatusCommand, this.NoPayoutCommand, this.PayoutCommand);
            if (this.existingCommand != null)
            {
                ChannelSession.Settings.GameCommands.Remove(this.existingCommand);
                newCommand.ID = this.existingCommand.ID;
            }
            ChannelSession.Settings.GameCommands.Add(newCommand);
        }

        public override async Task<bool> Validate()
        {
            if (this.MinimumAmountForPayout <= 0)
            {
                await DialogHelper.ShowMessage("The Min Amount for Payout is not a valid number greater than 0");
                return false;
            }

            if (this.PayoutProbability <= 0 || this.PayoutProbability > 100)
            {
                await DialogHelper.ShowMessage("The Payout Probability is not a valid number between 0 - 100");
                return false;
            }

            if (this.PayoutPercentageMinimum < 0 || PayoutPercentageMinimum > 100)
            {
                await DialogHelper.ShowMessage("The Min Payout % is not a valid number between 0 - 100");
                return false;
            }

            if (this.PayoutPercentageMaximum < 0 || this.PayoutPercentageMaximum > 100)
            {
                await DialogHelper.ShowMessage("The Max Payout % is not a valid number between 0 - 100");
                return false;
            }

            if (this.PayoutPercentageMaximum < this.PayoutPercentageMinimum)
            {
                await DialogHelper.ShowMessage("The Max Payout % can not be less than Min Payout %");
                return false;
            }

            if (string.IsNullOrEmpty(this.StatusArgument) && !this.StatusArgument.Any(c => char.IsLetterOrDigit(c)))
            {
                await DialogHelper.ShowMessage("The Status Argument must have a valid value");
                return false;
            }

            return true;
        }
    }
}