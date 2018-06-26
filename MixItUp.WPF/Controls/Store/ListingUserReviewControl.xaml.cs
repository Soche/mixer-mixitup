﻿using MixItUp.Base.Model.Store;
using System.Windows.Controls;

namespace MixItUp.WPF.Controls.Store
{
    /// <summary>
    /// Interaction logic for ListingUserReviewControl.xaml
    /// </summary>
    public partial class ListingUserReviewControl : UserControl
    {
        private StoreListingReviewModel review;

        public ListingUserReviewControl(StoreListingReviewModel review)
        {
            this.DataContext = this.review = review;

            InitializeComponent();

            this.Loaded += ListingUserReviewControl_Loaded;
        }

        private void ListingUserReviewControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            this.UserAvatar.SetImageUrl(this.review.UserAvatar);
        }
    }
}