//mcs poppassd.cs -r:Mono.Posix.dll -r:System.Configuration.dll -debug
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Text;
using Mono.Unix.Native;
using System.Threading;

namespace poppassd
{
    class Program
    {
        static int Main()
        {
            try
            {
                return Poppassd();
            }
            catch(Exception e)
            {
                Syscall.syslog(SyslogFacility.LOG_DAEMON, SyslogLevel.LOG_ERR, string.Format("unexpected error {0}", e.ToString()));
                throw;
            }
        }

        private static int Poppassd()
        {
            const int badPassDelay = 3000;
            const int maxLenPass = 128;
            const string version = "0.1shim";
            Console.WriteLine("200 poppassd v{0} hello, who are you?", version);
            string line = (Console.ReadLine() ?? "").ToLowerInvariant();
            string user;
            if (!line.StartsWith("user ") || (user = line.Substring(5)).Length == 0)
            {
                Console.WriteLine("500 Username required.");
                return 1;
            }

            if (!CheckUserName(user))
            {
                Console.WriteLine("500 Invalid username.");
                return 1;
            }

            Console.WriteLine("200 Your password please.");
            line = (Console.ReadLine() ?? "").ToLowerInvariant();
            if (line.Length > maxLenPass)
            {
                Console.WriteLine("500 Password length exceeded (max {0}).", maxLenPass);
                return 1;
            }
            string oldpass;
            if (!line.StartsWith("pass ") || (oldpass = line.Substring(5)).Length == 0)
            {
                Console.WriteLine("500 Password required.");
                return 1;
            }
            if (!CheckPassword(user, oldpass))
            {
                Console.WriteLine("500 Old password is incorrect.");
                Syscall.syslog(SyslogFacility.LOG_DAEMON, SyslogLevel.LOG_ERR, string.Format("old password is incorrect for user {0}", user));
                Thread.Sleep(badPassDelay);
                return 1;
            }

            Console.WriteLine("200 Your new password please.");
            line = (Console.ReadLine() ?? "").ToLowerInvariant();
            string newpass;
            if (!line.StartsWith("newpass ") || (newpass = line.Substring(8)).Length == 0)
            {
                Console.WriteLine("500 New password required.");
                return 1;
            }

            if (!ChangePassword(user, newpass))
            {
                Console.WriteLine("500 Server error, password not changed");
                return 1;
            }

            Syscall.syslog(SyslogFacility.LOG_DAEMON, SyslogLevel.LOG_ERR, string.Format("changed POP3 password for {0}", user));
            Console.WriteLine("200 Password changed, thank-you.");
            line = (Console.ReadLine() ?? "").ToLowerInvariant();
            if (line != "quit")
            {
                Console.WriteLine("500 Quit required.");
                return 1;
            }
            Console.WriteLine("200 bye.");
            return 0;            
        }

        private static bool RunProcessWithExitCode(string path, string command)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    Arguments = command,
                }
            };
            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        private static string RunProcessWithOutput(string path, string command)
        {
            StringBuilder sbOut = new StringBuilder();
            StringBuilder sbErr = new StringBuilder();
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    Arguments = command,
                }
            };

            process.OutputDataReceived += (sender, a) => sbOut.AppendLine(a.Data);
            process.ErrorDataReceived += (sender, a) => sbErr.AppendLine(a.Data);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.WaitForExit();
            Syscall.syslog(SyslogFacility.LOG_DAEMON, SyslogLevel.LOG_ERR, string.Format("process error {0}", sbErr.ToString()));
            return sbOut.ToString();
        }

        private static bool CheckUserName(string user)
        {
            string path = ConfigurationManager.AppSettings["doveadm"];
            string command = ConfigurationManager.AppSettings["checkUser"];
            command = string.Format(command, user);
            return RunProcessWithExitCode(path, command);
        }
        private static bool CheckPassword(string user, string oldpass)
        {
            string path = ConfigurationManager.AppSettings["doveadm"];
            string command = ConfigurationManager.AppSettings["checkPassword"];
            command = string.Format(command, user, oldpass);
            return RunProcessWithExitCode(path, command);
        }
        private static bool ChangePassword(string user, string newpass)
        {
            try
            {
                string path = ConfigurationManager.AppSettings["doveadm"];
                string command = ConfigurationManager.AppSettings["getHash"];
                command = string.Format(command, user, newpass);
                string hash = RunProcessWithOutput(path, command);
                string shadowPath = GetShadowPath(user, ConfigurationManager.AppSettings["shadowPath"]);
                string[] lines = File.ReadAllLines(shadowPath);
                List<string> outLines = new List<string>();
                bool done = false;
                foreach (string line in lines)
                {
                    if (line.ToLowerInvariant().StartsWith(user + ":"))
                    {
                        done = true;
                        outLines.Add(user + ":" + hash);
                    }
                    else
                    {
                        outLines.Add(line);
                    }
                }
                File.WriteAllLines(shadowPath, outLines.ToArray());
                return done;
            }
            catch (Exception e)
            {
                Syscall.syslog(SyslogFacility.LOG_DAEMON, SyslogLevel.LOG_ERR, string.Format("cannot change password {0}", e.ToString()));
                return false;
            }
        }

        private static string GetShadowPath(string user, string path)
        {
            return path.Replace("%d", user.Split('@')[1]);
        }
    }
}
