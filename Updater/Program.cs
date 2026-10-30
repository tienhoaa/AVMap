using System;
using System.Windows.Forms;

namespace AVMapUpdater
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string targetDir = AppDomain.CurrentDomain.BaseDirectory;
            string mode = "acad";
            string cadExe = "";
            string dwgPath = "";
            int cadPid = 0;
            string versionUrl = "https://raw.githubusercontent.com/tienhoaa/AVMap/main/version.json";

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.Equals("-targetDir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    targetDir = args[++i];
                }
                else if (arg.Equals("-mode", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    mode = args[++i];
                }
                else if (arg.Equals("-cadExe", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    cadExe = args[++i];
                }
                else if (arg.Equals("-dwg", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    dwgPath = args[++i];
                }
                else if (arg.Equals("-cadPid", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    int.TryParse(args[++i], out cadPid);
                }
                else if (arg.Equals("-versionUrl", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    versionUrl = args[++i];
                }
            }

            Application.Run(new UpdaterForm(targetDir, mode, cadExe, dwgPath, cadPid, versionUrl));
        }
    }
}
