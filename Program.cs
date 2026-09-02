using Velopack;

namespace Button_Config;

static class Program
{
    [STAThread]
    static void Main()
    {
        // This line handles the update process before the app even fully starts
        VelopackApp.Build().Run(); 

        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
    }    
}