﻿using Mixer.Base.Util;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Window;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.Controls.MainControls
{
    public class GiveawayMainControlViewModel : MainControlViewModelBase
    {
        public string Item { get; set; }

        public IEnumerable<string> EntryTypes { get { return EnumHelper.GetEnumNames<GiveawayEntryTypeEnum>(); } }
        public string EntryTypeString
        {
            get { return EnumHelper.GetEnumName(this.EntryType); }
            set
            {
                this.EntryType = EnumHelper.GetEnumValueFromString<GiveawayEntryTypeEnum>(value);
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged("IsCommandBased");
                this.NotifyPropertyChanged("IsDonationTipBased");
            }
        }
        public GiveawayEntryTypeEnum EntryType { get; private set; } = GiveawayEntryTypeEnum.Command;

        public bool IsCommandBased { get { return this.EntryType == GiveawayEntryTypeEnum.Command; } }

        public bool IsDonationTipBased { get { return this.EntryType == GiveawayEntryTypeEnum.DonationTips; } }

        public string TotalTime
        {
            get { return ChannelSession.Settings.GiveawayTimer.ToString(); }
            set
            {
                if (!string.IsNullOrEmpty(value) && int.TryParse(value, out int time) && time > 0)
                {
                    ChannelSession.Settings.GiveawayTimer = time;
                }
                else
                {
                    ChannelSession.Settings.GiveawayTimer = 0;
                }
                this.NotifyPropertyChanged();
            }
        }

        public string ReminderTime
        {
            get { return ChannelSession.Settings.GiveawayReminderInterval.ToString(); }
            set
            {
                if (!string.IsNullOrEmpty(value) && int.TryParse(value, out int time) && time > 0)
                {
                    ChannelSession.Settings.GiveawayReminderInterval = time;
                }
                else
                {
                    ChannelSession.Settings.GiveawayReminderInterval = 0;
                }
                this.NotifyPropertyChanged();
            }
        }

        public string MaxEntries
        {
            get { return ChannelSession.Settings.GiveawayMaximumEntries.ToString(); }
            set
            {
                if (!string.IsNullOrEmpty(value) && int.TryParse(value, out int maxEntries) && maxEntries > 0)
                {
                    ChannelSession.Settings.GiveawayMaximumEntries = maxEntries;
                }
                else
                {
                    ChannelSession.Settings.GiveawayMaximumEntries = 0;
                }
                this.NotifyPropertyChanged();
            }
        }

        public bool RequireClaim
        {
            get { return ChannelSession.Settings.GiveawayRequireClaim; }
            set
            {
                ChannelSession.Settings.GiveawayRequireClaim = value;
                this.NotifyPropertyChanged();
            }
        }

        public bool AllowPastWinners
        {
            get { return ChannelSession.Settings.GiveawayAllowPastWinners; }
            set
            {
                ChannelSession.Settings.GiveawayAllowPastWinners = value;
                this.NotifyPropertyChanged();
            }
        }

        public string Command
        {
            get { return ChannelSession.Settings.GiveawayCommand; }
            set
            {
                ChannelSession.Settings.GiveawayCommand = value;
                this.NotifyPropertyChanged();
            }
        }

        public IEnumerable<string> EntryQualificationTypes { get { return EnumHelper.GetEnumNames<GiveawayDonationEntryQualificationTypeEnum>(); } }
        public string EntryQualificationTypeString
        {
            get { return EnumHelper.GetEnumName(this.EntryQualificationType); }
            set
            {
                this.EntryQualificationType = EnumHelper.GetEnumValueFromString<GiveawayDonationEntryQualificationTypeEnum>(value);
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged("EntryAmountRequired");
            }
        }
        public GiveawayDonationEntryQualificationTypeEnum EntryQualificationType { get; private set; } = GiveawayDonationEntryQualificationTypeEnum.OneEntryPerUser;

        public bool EntryAmountRequired
        {
            get { return this.EntryQualificationType == GiveawayDonationEntryQualificationTypeEnum.MinimumAmountRequired || this.EntryQualificationType == GiveawayDonationEntryQualificationTypeEnum.OneEntryPerAmount; }
        }

        public string DonationAmountEntryQualifier
        {
            get { return ChannelSession.Settings.GiveawayDonationAmount.ToString(); }
            set
            {
                if (!string.IsNullOrEmpty(value) && double.TryParse(value, out double amount) && amount > 0)
                {
                    amount = Math.Round(amount, 2);
                    ChannelSession.Settings.GiveawayDonationAmount = amount;
                }
                else
                {
                    ChannelSession.Settings.GiveawayDonationAmount = 0;
                }
                this.NotifyPropertyChanged();
            }
        }

        public bool IsRunning { get { return ChannelSession.Services.GiveawayService.IsRunning; } }
        public bool IsNotRunning { get { return !this.IsRunning; } }

        public string TimeLeft
        {
            get
            {
                string result = (ChannelSession.Services.GiveawayService.TimeLeft % 60).ToString() + " Seconds";
                if (ChannelSession.Services.GiveawayService.TimeLeft > 60)
                {
                    result = (ChannelSession.Services.GiveawayService.TimeLeft / 60).ToString() + " Minutes " + result;
                }
                return result;
            }
        }

        public string WinnerUsername { get { return (ChannelSession.Services.GiveawayService.Winner != null) ? ChannelSession.Services.GiveawayService.Winner.UserName : string.Empty; } }

        public ObservableCollection<GiveawayUser> EnteredUsers = new ObservableCollection<GiveawayUser>();

        public ICommand StartGiveawayCommand { get; set; }
        public ICommand EndGiveawayCommand { get; set; }

        public GiveawayMainControlViewModel(MainWindowViewModel windowViewModel)
            : base(windowViewModel)
        {
            GlobalEvents.OnGiveawaysChangedOccurred += GlobalEvents_OnGiveawaysChangedOccurred;

            this.StartGiveawayCommand = this.CreateCommand(async (x) =>
            {
                string result = await ChannelSession.Services.GiveawayService.Start(this.Item, this.EntryType, this.EntryQualificationType);
                if (!string.IsNullOrEmpty(result))
                {
                    await DialogHelper.ShowMessage(result);
                }
                this.NotifyPropertyChanges();
            });

            this.EndGiveawayCommand = this.CreateCommand(async (x) =>
            {
                await ChannelSession.Services.GiveawayService.End();
                this.NotifyPropertyChanges();
            });
        }

        private async void GlobalEvents_OnGiveawaysChangedOccurred(object sender, System.EventArgs e)
        {
            await DispatcherHelper.InvokeDispatcher(() =>
            {
                this.EnteredUsers.Clear();
                foreach (GiveawayUser user in ChannelSession.Services.GiveawayService.Users)
                {
                    this.EnteredUsers.Add(user);
                }
                return Task.FromResult(0);
            });
            this.NotifyPropertyChanges();
        }

        private void NotifyPropertyChanges()
        {
            this.NotifyPropertyChanged("EnteredUsers");
            this.NotifyPropertyChanged("EntryAmountRequired");
            this.NotifyPropertyChanged("IsRunning");
            this.NotifyPropertyChanged("IsNotRunning");
            this.NotifyPropertyChanged("TimeLeft");
            this.NotifyPropertyChanged("WinnerUsername");
        }
    }
}
