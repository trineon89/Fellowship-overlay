using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace Fellowship_overlay;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    private void OnHyperlinkRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignored – if we cannot launch a browser we do not want to crash the window.
        }

        e.Handled = true;
    }
}