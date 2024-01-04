/*
 * Created by SharpDevelop.
 * Date: 31.08.2017
 * Time: 20:32
 *
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Elatec.Net.Helpers.Log4CSharp
{
    /// <summary>
    /// Description of LogWriter.
    /// </summary>
    public static class LogWriter
    {
        private static StreamWriter textStream;

        private static readonly string _logFileName = "log.txt";

        /// <summary>
        ///
        /// </summary>
        /// <param name="entry"></param>
        public static void CreateLogEntry(string entry, string appDataSubPath)
        {
            string _logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appDataSubPath, "log");

            if (!Directory.Exists(_logFilePath))
            {
                Directory.CreateDirectory(_logFilePath);
            }

            try
            {
                if (!File.Exists(Path.Combine(_logFilePath, _logFileName)))
                {
                    textStream = File.CreateText(Path.Combine(_logFilePath, _logFileName));
                }
                else
                {
                    textStream = File.AppendText(Path.Combine(_logFilePath, _logFileName));
                }

                textStream.WriteAsync(string.Format("{0}" + Environment.NewLine, entry.Replace("\r\n", "; ")));
                textStream.Close();
                textStream.Dispose();
            }
            catch
            {
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="entry"></param>
        public static void CreateLogEntry(Exception e)
        {
            CreateLogEntry(e, Assembly.GetExecutingAssembly().FullName);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="entry"></param>
        public static void CreateLogEntry(Exception e, string appDataSubPath)
        {
            string _logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appDataSubPath, "log");

            if (!Directory.Exists(_logFilePath))
            {
                Directory.CreateDirectory(_logFilePath);
            }

            try
            {
                if (!File.Exists(Path.Combine(_logFilePath, _logFileName)))
                {
                    textStream = File.CreateText(Path.Combine(_logFilePath, _logFileName));
                }
                else
                {
                    textStream = File.AppendText(Path.Combine(_logFilePath, _logFileName));
                }

                textStream.WriteAsync(
                    string.Format("{0}" + Environment.NewLine, 
                    string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : "").Replace("\r\n", "; ")));
                textStream.Close();
                textStream.Dispose();
            }
            catch
            {
            }
        }
    }
}