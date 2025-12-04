using System;
using System.Windows.Forms;

namespace TechnologyStoreAutomation
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            EnvFileLoader.LoadFromFile();

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
