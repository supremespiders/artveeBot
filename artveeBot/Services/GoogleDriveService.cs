using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using artveeBot.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using File = Google.Apis.Drive.v3.Data.File;

namespace artveeBot.Services
{
    public static class GoogleDriveService
    {
        static string[] Scopes = { DriveService.Scope.Drive };
        static string ApplicationName = "Drive API .NET Quickstart";
        private static DriveService _service;
        private static UserCredential _credential;
        private static string _imgFolderId;

        public static void Setup()
        {
            using (var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            {
                var credPath = "token.json";
                _credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Console.WriteLine("Credential file saved to: " + credPath);
            }

            _service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = _credential,
                ApplicationName = ApplicationName
            });
        }

        public static async Task<string> UploadFile(string filePath)
        {
            var fileMetadata = new File()
            {
                Name = Path.GetFileName(filePath),
                Parents = new List<string>() { _imgFolderId }
            };
            using (var fsSource = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                var request = _service.Files.Create(fileMetadata, fsSource, "application/zip");
                request.Fields = "*";
                var results = await request.UploadAsync(CancellationToken.None);

                if (results.Status == UploadStatus.Failed)
                {
                    throw new KnownException($"Error uploading file: {results.Exception.ToString()}");
                }

                return request.ResponseBody?.Id;
            }
        }

        static async Task<string> UploadAngGetUrl(string localPath)
        {
            var name = Path.GetFileName(localPath);
            var exist = await GetIfExist(name);
            if (exist != null)
                return exist;
            var id = await UploadFile(localPath);
            var req = _service.Files.Get(id);
            req.Fields = "id, name,webContentLink";
            return (await req.ExecuteAsync()).WebContentLink;
        }

        static async Task<string> GetIfExist(string name)
        {
            var req = _service.Files.List();
            req.Q = $"mimeType='image/jpeg' and name = '{name}'";
            req.PageSize = 1;
            req.Fields = "files(id, name,webContentLink)";
            var files = (await req.ExecuteAsync()).Files;
            return files.Count == 0 ? null : files.First().WebContentLink;
        }

        public static async Task<string> CreateFolderIfNotExist(string name)
        {
            var req = _service.Files.List();
            req.Q = $"mimeType='application/vnd.google-apps.folder' and trashed=false and name='{name}'";
            req.PageSize = 1;
            var files = (await req.ExecuteAsync()).Files;
            if (files.Count != 0)
            {
                _imgFolderId = files.First().Id;
                return _imgFolderId;
            }
            var fileMetadata = new File
            {
                Name = name,
                MimeType = "application/vnd.google-apps.folder"
            };
            var req2 = _service.Files.Create(fileMetadata);
            _imgFolderId = (await req2.ExecuteAsync()).Id;
            //req2.Fields = "id";
            return _imgFolderId;
        }
    }
}