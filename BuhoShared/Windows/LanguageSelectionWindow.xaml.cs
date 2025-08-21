using System.Windows;
using BuhoShared.Services;

namespace BuhoShared.Windows;

public partial class LanguageSelectionWindow : Window
{
    public string SelectedLanguage { get; private set; } = "en";

    public LanguageSelectionWindow()
    {
        InitializeComponent();
        
        // Set current language selection
        var currentCulture = LocalizationService.Instance.CurrentCulture;
        if (currentCulture.TwoLetterISOLanguageName == "es")
        {
            SpanishRadioButton.IsChecked = true;
        }
        else
        {
            EnglishRadioButton.IsChecked = true;
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (EnglishRadioButton.IsChecked == true)
        {
            SelectedLanguage = "en";
        }
        else if (SpanishRadioButton.IsChecked == true)
        {
            SelectedLanguage = "es";
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
