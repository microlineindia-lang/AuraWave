using System.Windows;
using System.Windows.Controls;

namespace AuraWave.App.Controls;

public partial class ConsolePanel : UserControl
{
    public ConsolePanel()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ConsolePanel),
            new PropertyMetadata("Console"));

    public static readonly DependencyProperty RightTextProperty =
        DependencyProperty.Register(nameof(RightText), typeof(string), typeof(ConsolePanel),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty LogTextProperty =
        DependencyProperty.Register(nameof(LogText), typeof(string), typeof(ConsolePanel),
            new PropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string RightText
    {
        get => (string)GetValue(RightTextProperty);
        set => SetValue(RightTextProperty, value);
    }

    public string LogText
    {
        get => (string)GetValue(LogTextProperty);
        set => SetValue(LogTextProperty, value);
    }
}
