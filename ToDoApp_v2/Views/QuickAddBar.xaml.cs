using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SlimeTodo.ViewModels;

namespace SlimeTodo.Views;

public partial class QuickAddBar : UserControl
{
    public QuickAddBar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.RequestQuickAddFocus += () => QuickAddTextBox.Focus();
        }
    }

    private void QuickAddTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.QuickAddText = string.Empty;
            }
            // Move focus away
            Keyboard.ClearFocus();
        }
    }

    public void FocusInput()
    {
        QuickAddTextBox.Focus();
        QuickAddTextBox.SelectAll();
    }
}
