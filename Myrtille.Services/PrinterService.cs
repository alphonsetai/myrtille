using System;
using System.Diagnostics;
using System.IO;
using Myrtille.Services.Contracts;

namespace Myrtille.Services
{
    public class PrinterService : IPrinterService
    {
        public Stream GetPdfFile(Guid remoteSessionId, string fileName)
        {
            Stream fileStream = null;

            try
            {
                var systemTempPath = Environment.GetEnvironmentVariable("TEMP", EnvironmentVariableTarget.Machine);
                fileStream = File.Open(Path.Combine(systemTempPath, fileName), FileMode.Open, FileAccess.Read, FileShare.Read);
                Trace.TraceInformation("Downloaded pdf file {0}, remote session {1}", fileName, remoteSessionId);
            }
            catch (Exception exc)
            {
                Trace.TraceError("Failed to download pdf file {0}, remote session {1} ({2})", fileName, remoteSessionId, exc);
                throw;
            }

            return fileStream;
        }

        public void DeletePdfFile(Guid remoteSessionId, string fileName)
        {
            try
            {
                var systemTempPath = Environment.GetEnvironmentVariable("TEMP", EnvironmentVariableTarget.Machine);
                File.Delete(Path.Combine(systemTempPath, fileName));
                Trace.TraceInformation("Deleted pdf file {0}, remote session {1}", fileName, remoteSessionId);
            }
            catch (Exception exc)
            {
                Trace.TraceError("Failed to delete pdf file {0}, remote session {1} ({2})", fileName, remoteSessionId, exc);
                throw;
            }
        }
    }
}