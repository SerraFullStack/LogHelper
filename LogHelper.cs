using IntegracaoGapApp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    

    class LogHelper
    {

        string fileName;
        string name = "";
        object logLock = new object();
        public LogHelper(string name, string ext = "log")
        {
            this.name = name;
            string path = System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath) + "\\logs";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            fileName = path +"\\"+ name + "." + ext;

            
        }
        public void log(string msg)
        {
            Program.log("[" + DateTime.Now.ToString() + "] " + this.name + ": "+ msg);
            lock (logLock)
            {
                msg = "["+DateTime.Now.ToString()+"]" + msg;
                try
                {
                    if (System.IO.File.Exists(fileName))
                        System.IO.File.AppendAllText(fileName, msg + "\r\n");
                    else
                        System.IO.File.WriteAllText(fileName, msg + "\r\n");
                }
                catch
                {
                    try
                    {
                        if (System.IO.File.Exists(fileName))
                            System.IO.File.AppendAllText(fileName, msg + "\r\n");
                        else
                            System.IO.File.WriteAllText(fileName, msg + "\r\n");
                    }
                    catch { }
                }
            };
        }

        /// <summary>
        /// Android style
        /// </summary>
        private static Dictionary<string, LogHelper> catLoggers = new Dictionary<string, LogHelper>();
        public static void cat(string logName, string data)
        {
            LogHelper logger = null;
            try { 
                logger = LogHelper.catLoggers[logName]; 
            }
            catch 
            {
                logger = new LogHelper(logName);
                LogHelper.catLoggers[logName] = logger;
            }

            logger.log(data);
        }
            
    }

    class Log : LogHelper { public Log(string name, string ext = "log") : base(name, ext) {} }



    
}
