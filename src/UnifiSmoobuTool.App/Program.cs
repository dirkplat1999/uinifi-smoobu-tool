using System;
using Velopack;

namespace UnifiSmoobuTool.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Must run first, before any other startup logic: handles Velopack's install/update/
        // uninstall hooks (creating shortcuts, running first-run tasks) when invoked by the
        // installer with special command-line flags, and exits immediately in that case.
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
