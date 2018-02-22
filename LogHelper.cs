/*
	by: Rafael Tonello (tonello.rafinha@gmail.com)
	
	Version; 1.0.0.1

	History:
		1.0.0.1 -> 23/01/2018-> Fixed problem with file access conflict

*/
using SIDAI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public LogHelper(string name, ArchiveType archiveType = ArchiveType.mounthly, string ext = "log")
        {
            this.archiveType = archiveType;
            string path = System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath) + "\\logs";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            fileName = path + "\\" + name + "." + ext;


        }

        Semaphore sm = new Semaphore(1, int.MaxValue);

        List<string> buffer = new List<string>();
        bool writeRunner = false;
        public void log(string msg)
        {
            buffer.Add(msg);
            threadWrite();
        }

        public void threadWrite()
        {
            if (writeRunner)
                return;

            writeRunner = true;


            Thread th = new Thread(delegate ()
            {
                string msg;
                sm.WaitOne();
                while (buffer.Count > 0)
                {
                    msg = "[" + DateTime.Now.ToString() + "]" + buffer[0];
                    buffer.RemoveAt(0);
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
                }

                try
                {
                    verifyArchive();
                }
                catch { }

                sm.Release();

                writeRunner = false;
            });
            th.Start();
        }

        public void verifyArchive()
        {
            bool archive = false;
            string destName = "";

            //the fileCreationTime need be searched in first line of file, because that windows memorize the file creation by name (even if you
            //move the file and create a new one, windows will create it with the date of the original file. :( )


            if (this.archiveType == ArchiveType.noArchive)
                return;

            DateTime fileCreationTime = DateTime.Now;

            try
            {

                StreamReader freader = new StreamReader(fileName);

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
                        return;
                    }
                }



                if (this.archiveType == ArchiveType.hourly)
                {
                    if (fileCreationTime.Hour != DateTime.Now.Hour)
                    {
                        archive = true;
                        destName = fileCreationTime.ToString("dd MM yyyy - HH mm ss") + ".log";
                    }
                }
                else if (this.archiveType == ArchiveType.daily)
                {
                    if (fileCreationTime.Day != DateTime.Now.Day)
                    {
                        archive = true;
                        destName = fileCreationTime.ToString("dd MM yyyy") + ".log";
                    }
                }
                else if (this.archiveType == ArchiveType.weekly)
                {
                    if (DateTime.Now.Subtract(fileCreationTime).TotalDays > 7)
                    {
                        archive = true;
                        destName = fileCreationTime.ToString("dd MM yyyy") + ".log";
                    }
                }
                else if (this.archiveType == ArchiveType.mounthly)
                {
                    var m1 = fileCreationTime.Month;
                    if (m1 != DateTime.Now.Month)
                    {
                        archive = true;
                        destName = fileCreationTime.ToString("MM yyyy") + ".log";
                    }
                }

                if (archive)
                {
                    string path = System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath) + "\\logs\\archive";
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);
                    path += "\\" + Path.GetFileNameWithoutExtension(fileName);
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);


                    string destFile = path + "\\" + destName;
                    int tryCount = 0;
                    while (File.Exists(destFile))
                    {
                        tryCount++;
                        destFile = path + "\\(" + tryCount + ")" + destName;
                    }

                    File.Move(fileName, destFile);
                }
            }

            catch { };

        }

        /// <summary>
        /// Android style
        /// </summary>
        private static Dictionary<string, LogHelper> catLoggers = new Dictionary<string, LogHelper>();
        public static void d(string logName, string data)
        {
            LogHelper logger = null;
            try
            {
                logger = LogHelper.catLoggers[logName];
            }
            catch
            {
                logger = new LogHelper(logName);
                LogHelper.catLoggers[logName] = logger;
            }

            logger.log(data);
        }

        public static void cat(string logName, string data)
        {
            d(logName, data);
        }

    }

    class Log : LogHelper { public Log(string name, string ext = "log") : base(name, ArchiveType.mounthly, ext) { } }




}
