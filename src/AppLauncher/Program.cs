using System;
using System.Windows.Forms;

namespace AppLauncher
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ApplicationConfiguration.Initialize();

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                try
                {
                    MessageBox.Show(
                        $"Unhandled error:\n\n{args.ExceptionObject}",
                        "Clinix Launcher",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                catch
                {
                    // Last-resort; ignore UI failures.
                }
            };

            Application.ThreadException += (_, args) =>
            {
                MessageBox.Show(
                    $"Unexpected error:\n\n{args.Exception.Message}",
                    "Clinix Launcher",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            };

            Application.Run(new MainForm());
        }
    }
}
