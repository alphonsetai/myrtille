using System;
using System.IO;
using System.ServiceModel;

namespace Myrtille.Services.Contracts
{
    [ServiceContract]
    public interface IPrinterService
    {
        [OperationContract]
        Stream GetPdfFile(Guid remoteSessionId, string fileName);

        [OperationContract]
        void DeletePdfFile(Guid remoteSessionId, string fileName);
    }
}