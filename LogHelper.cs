/*
	by: Rafael Tonello (tonello.rafinha@gmail.com)
	
	Version; 1.3.0.0

	History:
		1.0.0.1 -> 23/01/2018-> Fixed problem with file access conflict
		1.1.0.1 -> 03/04/2018-> Identation in line breaks
		1.2.0.0 -> 05/06/2018-> Compacting logs before archive this (if the app is running under Linux or if there is the 7z.exe in app folder)
		1.2.0.2 -> 05/06/2018-> Solved a fileLock problem with threadWrite
		1.3.0.0 -> 03/08/2018-> Now, logs function receive a variable number of arguments.

*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Shared
{


    class LogHelper
    {
        public enum ArchiveType { daily, hourly, weekly, mounthly, noArchive }

        public ArchiveType archiveType = ArchiveType.noArchive;


        string fileName;
        object logLock = new object();

        Semaphore fileLock = new Semaphore(1, int.MaxValue);

        public LogHelper(object sender, ArchiveType archiveType = ArchiveType.mounthly, string ext = "log", string path="")
        {
            this.Initialize(sender.GetType().FullName, archiveType, ext, path);
        }
        public LogHelper(string name, ArchiveType archiveType = ArchiveType.mounthly, string ext = "log", string path="")
        {
            this.Initialize(name, archiveType, ext, path);
        }
        private void Initialize(string name, ArchiveType archiveType = ArchiveType.mounthly, string ext = "log", string path="")
        {
            this.archiveType = archiveType;
			if (path == "")
				path = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/logs";
			
			path = path.Replace("\\", "/");
			
			
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            fileName = path + "/" + name + "." + ext;
			
			//grant the file name uses UNIX Like file names (windows is compatible with this, and also work on Linux/Mono)
			fileName = fileName.Replace("\\", "/");


            try
            {
                verifyArchive();
            }
            catch { }


        }

        Semaphore sm = new Semaphore(1, int.MaxValue);

        List<string> buffer = new List<string>();
        public void log(params object[] msg)
        {
            //prepare the list
            StringBuilder msgText = new StringBuilder();
            for (int cont = 0; cont <msg.Length; cont++)
            {
                msgText.Append(getObjectText(msg[cont]));
                if (cont < msg.Length - 1)
                    msgText.Append(" ");

            }

            string data = msgText.ToString();
            data = data.Replace("\r\n", "[[[[linebreak]]]]").Replace("\r", "[[[[linebreak]]]]").Replace("\n", "[[[[linebreak]]]]");
            data = data.Replace("[[[[linebreak]]]]", "\r\n                     ");

			lock(buffer)
			{
				buffer.Add(data);
			}
            threadWrite();
        }

        private string getObjectText(object obj)
        {
            if (obj is string)
                return (string)obj;
            else if (obj.GetType().Name.Contains("System.Collections.Generic.List"))
            {
                //get list length
                int listCount = (int)obj.GetType().GetMethod("get_Count").Invoke(obj, new object[] { });

                //test if is enumerable
                if (obj.GetType().GetMethod("GetEnumerator") == null) return "";

                //loop until serialize all items
                StringBuilder ret = new StringBuilder();
                for (int count = 0; count < listCount; count++)
                {
                    object ret3 = obj.GetType().GetMethod("get_Item").Invoke(obj, new object[] { count });
                    ret.Append(getObjectText(ret3));
                    if (count < listCount - 1)
                        ret .Append(", ");
                }
                return ret.ToString();
            }
            else if (obj is Array)
            {
                StringBuilder ret = new StringBuilder();
                int max = ((Array)obj).Length;
                int cont = 0;
                foreach (var c in (Array)obj)
                {
                    ret.Append(getObjectText(c));
                    if (cont < max - 1)
                        ret.Append(", ");

                    cont++;
                }
                return ret.ToString();
            }
            else return (obj.ToString());
        }


        Thread thWrite = null;
        public void threadWrite()
        {
            //checks if the thread already running
            if (thWrite != null)
                return;

            thWrite = new Thread(delegate ()
            {
                string msg;

                try
                {
                    verifyArchive();
                }
                catch { }
                fileLock.WaitOne();
                sm.WaitOne();

                while (buffer.Count > 0)
                {
					//the try bellow prevent any problem with buffer (this problemas occoured one time by unknown problem between c# and Windows)
                    try
                    {
						lock(buffer)
						{
							msg = "[" + DateTime.Now.ToString() + "]" + buffer[0];
							buffer.RemoveAt(0);
						}
                    }
                    catch (Exception e)
                    {
                        msg = "[" + DateTime.Now.ToString() + "] LOGHELPER_ERROR: " +e.Message ;
                        try { /*buffer.Clear();*/ } catch { }
						
                        /*buffer = new List<string>();*/
                    }
					
                    try
                    {
                        if (System.IO.File.Exists(fileName))
                            System.IO.File.AppendAllText(fileName, msg + "\r\n");
                        else
                            System.IO.File.WriteAllText(fileName, msg + "\r\n");

                        
                    }
                    catch
                    {
						Thread.Sleep(100);
                        try
                        {
                            if (System.IO.File.Exists(fileName))
                                System.IO.File.AppendAllText(fileName, msg + "\r\n");
                            else
                                System.IO.File.WriteAllText(fileName, msg + "\r\n");

                        }
                        catch 
						{ 
							Thread.Sleep(100);
						}
                    }
                }

                sm.Release();

                fileLock.Release();
                thWrite = null;
            });
            thWrite.Start();
        }


        Thread thVerifyArchive = null;
        public void verifyArchive()
        {
            if (thVerifyArchive != null)
                return;

            fileLock.WaitOne();

            thVerifyArchive = new Thread(delegate ()
            {

                bool archive = false;
                string destName = "";
                string appPath = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location).Replace("\\", "/");

                //the fileCreationTime need be searched in first line of file, because that windows memorize the file creation by name (even if you
                //move the file and create a new one, windows will create it with the date of the original file. :( )


                if (this.archiveType == ArchiveType.noArchive)
                {
                    fileLock.Release();
                    return;
                }

                DateTime fileCreationTime = DateTime.Now;

                string workingfile = fileName;

                try
                {

                    StreamReader freader = new StreamReader(workingfile);

                    while (true)
                    {
                        if (!freader.EndOfStream)
                        {
                            try
                            {
                                string l = freader.ReadLine();
                                fileCreationTime = DateTime.Parse(l.Substring(1, l.IndexOf(']') - 1));
                                freader.Close();
                                break;
                            }
                            catch { };

                        }
                        else
                        {
                            freader.Close();
                            fileLock.Release();
                            return;
                        }
                    }



                    if (this.archiveType == ArchiveType.hourly)
                    {
                        if (fileCreationTime.Hour != DateTime.Now.Hour)
                        {
                            archive = true;
                            destName = fileCreationTime.ToString("dd MM yyyy - HH mm ss");
                        }
                    }
                    else if (this.archiveType == ArchiveType.daily)
                    {
                        if (fileCreationTime.Day != DateTime.Now.Day)
                        {
                            archive = true;
                            destName = fileCreationTime.ToString("dd MM yyyy");
                        }
                    }
                    else if (this.archiveType == ArchiveType.weekly)
                    {
                        if (DateTime.Now.Subtract(fileCreationTime).TotalDays > 7)
                        {
                            archive = true;
                            destName = fileCreationTime.ToString("dd MM yyyy");
                        }
                    }
                    else if (this.archiveType == ArchiveType.mounthly)
                    {
                        var m1 = fileCreationTime.Month;
                        if (m1 != DateTime.Now.Month)
                        {
                            archive = true;
                            destName = fileCreationTime.ToString("MM yyyy");
                        }
                    }


                    if (archive)
                    {
                        //checks if the program is runninger over Unix enviroment (Linux)
                        if (Environment.OSVersion.ToString().ToUpper().Contains("UNIX"))
                        {
                            //locks by "zip" command and this version
                            Process zv = new Process();
                            zv.StartInfo.UseShellExecute = false;
                            zv.StartInfo.RedirectStandardOutput = true;
                            zv.StartInfo.FileName = "zip";
                            zv.StartInfo.Arguments = "-v ";
                            zv.Start();
                            string versionOut = zv.StandardOutput.ReadToEnd();
                            zv.WaitForExit();


                            if (versionOut.ToLower().Contains("this is zip"))
                            {
                                //compact the log using the Linux "zip" command
                                if (File.Exists(appPath + "/logs/" + Path.GetFileNameWithoutExtension(workingfile) + ".zip"))
                                    File.Delete(appPath + "/logs/" + Path.GetFileNameWithoutExtension(workingfile) + ".zip");
                                Process.Start("zip", "\"" + appPath + "/logs/" + Path.GetFileNameWithoutExtension(workingfile) + ".zip\" \"" + workingfile + "\"").WaitForExit();

                                //remove the original log file
                                int Timeout = 1000;
                                while (true)
                                {
                                    try
                                    {
                                        File.Delete(workingfile);
                                        break;
                                    }
                                    catch { }

                                    Thread.Sleep(100);
                                    Timeout -= 100;
                                    if (Timeout <= 0)
                                        break;
                                }

                                workingfile = appPath + "/logs/" + Path.GetFileNameWithoutExtension(workingfile) + ".zip";
                            }
                        }
                        else
                        {
                            //checks if the 7zip.exe there is in the installation folder
                            string _7zipApp = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\7z.exe";

                            if (File.Exists(_7zipApp))
                            {
                                //compact the log using the 7zip app
                                //compact the log using the Linux "zip" command
                                if (File.Exists(appPath + "/logs/" + Path.GetFileNameWithoutExtension(workingfile) + ".7z"))
                                    File.Delete(appPath + "/logs/" + Path.GetFileNameWithoutExtension(workingfile) + ".7z");
                                Process.Start(_7zipApp, " a -t7z \"" + appPath + "/logs/" + Path.GetFileNameWithoutExtension(workingfile) + ".7z\" \"" + workingfile + "\"").WaitForExit();
                                //remove the original log file
                                int Timeout = 10000;
                                while (true)
                                {
                                    try
                                    {
                                        File.Delete(workingfile);
                                        break;
                                    }
                                    catch { }

                                    Thread.Sleep(100);
                                    Timeout -= 100;
                                    if (Timeout <= 0)
                                        break;
                                }
                                workingfile = appPath + "/logs/" + Path.GetFileNameWithoutExtension(workingfile) + ".7z";
                            }
                            else
                            {

                            }
                        }


                        string path = appPath + "/logs/archive";

                        if (!Directory.Exists(path))
                            Directory.CreateDirectory(path);


                        path += "/" + Path.GetFileNameWithoutExtension(workingfile);
                        destName += Path.GetExtension(workingfile);
                        if (!Directory.Exists(path))
                            Directory.CreateDirectory(path);


                        string destFile = path + "/" + destName;
                        int tryCount = 0;
                        while (File.Exists(destFile))
                        {
                            tryCount++;
                            destFile = path + "/(" + tryCount + ")" + destName;
                        }

                        File.Move(workingfile, destFile);

                        //checks if the workingfile still exists(
                        int timeout = 10000;
                        while (timeout > 0)
                        {
                            try
                            {
                                if (File.Exists(destFile))
                                {
                                    if (File.Exists(workingfile))
                                    {
                                        //try to remove it with c#
                                        try
                                        {
                                            File.Delete(workingfile);
                                        }
                                        catch
                                        {
                                            //if under Linux, try to remvoe with  fm command
                                            if (Environment.OSVersion.ToString().ToUpper().Contains("UNIX"))
                                            {
                                                Process.Start("rm", " f \"" + workingfile + "\"");

                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    break;
                                }
                            }
                            catch { }
                            Thread.Sleep(100);
                            timeout -= 100;
                        }
                    }
                }

                catch { };

                fileLock.Release();
                this.thVerifyArchive = null;
            });

            thVerifyArchive.Start();
        }

        /// <summary>
        /// Android style
        /// </summary>
        private static Dictionary<string, LogHelper> catLoggers = new Dictionary<string, LogHelper>();
        public static void d(object sender, params object[] data)
        {
            d(sender.GetType().FullName, data);
        }

        public static void d(string logName, params object[] data)
        {
            LogHelper logger = null;
            try
            {
                if (LogHelper.catLoggers.ContainsKey(logName))
                    logger = LogHelper.catLoggers[logName];
                else
                {
                    logger = new LogHelper(logName);
                    LogHelper.catLoggers[logName] = logger;
                }
            }
            catch
            {
                logger = new LogHelper(logName);
                LogHelper.catLoggers[logName] = logger;
            }

            logger.log(data);
        }

        public static void cat(string logName, params object[] data)
        {
            d(logName, data);
        }

        public static void cat(object sender, params object[] data)
        {
            d(sender, data);
        }

    }

    class Log : LogHelper { public Log(string name, string ext = "log") : base(name, ArchiveType.mounthly, ext) { } }
}
