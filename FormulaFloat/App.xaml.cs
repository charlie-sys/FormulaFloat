using System;
using System.Threading;
using System.Windows;

namespace FormulaFloat;

public partial class App : Application
{
    private Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instance = new Mutex(true, "Local\\FormulaFloat.Desktop", out bool first);
        if (!first) { MessageBox.Show("FormulaFloat 已在运行，请在系统托盘中打开。", "FormulaFloat"); Shutdown(); return; }
        var window = new MainWindow();
        MainWindow = window;
        window.InitializeDesktop();
    }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
}
