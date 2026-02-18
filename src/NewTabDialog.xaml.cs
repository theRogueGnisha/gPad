using System.Windows;
using System.Windows.Input;

namespace gPad;

public partial class NewTabDialog : Window
{
    public string NoteName => NameBox.Text;

    public NewTabDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => NameBox.Focus();
        NameBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                DialogResult = true;
                Close();
            }
        };
    }

    public void SetInitialName(string name)
    {
        NameBox.Text = name ?? "";
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
