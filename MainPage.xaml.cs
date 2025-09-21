using Microsoft.Maui.Controls;
using CryptoPulse.ViewModels;

namespace CryptoPulse
{
    public partial class MainPage : ContentPage
    {
        // Page is bound to the MainViewModel so all UI can directly
        // hook into properties and commands defined there.
        public MainPage(MainViewModel vm)
        {
            InitializeComponent();

            // Set the data context of this page to the ViewModel
            // so that XAML bindings work.
            BindingContext = vm;
        }
    }
}
