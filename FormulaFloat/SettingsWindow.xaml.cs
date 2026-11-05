using System;
using System.Windows;

namespace FormulaFloat;
public partial class SettingsWindow : Window
{
    public ModelSettings? Result { get; private set; }
    public SettingsWindow(ModelSettings settings)
    {
        InitializeComponent();
        EndpointBox.Text = settings.Endpoint; ModelBox.Text = settings.Model; KeyBox.Password = settings.ApiKey; TimeoutBox.Text = settings.TimeoutSeconds.ToString();
    }
    private void SaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!int.TryParse(TimeoutBox.Text, out int seconds)) throw new InvalidOperationException("请输入整数秒数。");
            var result = new ModelSettings { Endpoint = EndpointBox.Text.Trim(), Model = ModelBox.Text.Trim(), ApiKey = KeyBox.Password.Trim(), TimeoutSeconds = seconds };
            SettingsStore.Save(result); Result = result; DialogResult = true;
        }
        catch (Exception ex) { ErrorText.Text = ex.Message; }
    }
}
