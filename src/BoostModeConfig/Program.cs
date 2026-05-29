using System.Text;

namespace BoostModeConfig;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
