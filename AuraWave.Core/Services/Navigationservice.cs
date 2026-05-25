using AuraWave.Core.Enums;
using AuraWave.Core.Interfaces;
using System;

namespace AuraWave.Core.Services
{
    public sealed class NavigationService : INavigationService
    {
        public NavigationPage CurrentPage { get; private set; } = NavigationPage.Dashboard;
        public event EventHandler<NavigationPage>? Navigated;

        public void NavigateTo(NavigationPage page)
        {
            if (CurrentPage == page) return;
            CurrentPage = page;
            Navigated?.Invoke(this, page);
        }
    }
}