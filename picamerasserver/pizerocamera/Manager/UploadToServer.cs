using System.Net;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using picamerasserver.Database;
using picamerasserver.Database.Models;
using picamerasserver.Options;
using SMBLibrary;
using SMBLibrary.Client;
using FileAttributes = SMBLibrary.FileAttributes;

namespace picamerasserver.pizerocamera.manager;

public class UploadToServer(
    PiZeroCameraManager piZeroCameraManager,
    IOptionsMonitor<DirectoriesOptions> dirOptionsMonitor,
    IOptionsMonitor<SmbOptions> smbOptionsMonitor,
    IDbContextFactory<PiDbContext> dbContextFactory,
    ILogger<UploadToServer> logger
)
{
    public bool UploadActive { get; private set; }
    private readonly SemaphoreSlim _uploadSemaphore = new(1, 1);
    private CancellationTokenSource? _uploadCancellationTokenSource;

    public async Task<Result> Upload(Guid pictureSetUuid)
    {
        // Another upload operation is already running
        if (!await _uploadSemaphore.WaitAsync(TimeSpan.Zero))
        {
            return Result.Failure("Another upload operation is already running");
        }

        // Just in case, cancel existing uploads (there shouldn't be any)
        if (_uploadCancellationTokenSource != null)
        {
            await _uploadCancellationTokenSource.CancelAsync();
        }

        _uploadCancellationTokenSource?.Dispose();
        _uploadCancellationTokenSource = new CancellationTokenSource();

        await using var piDbContext = await dbContextFactory.CreateDbContextAsync();

        var isConnected = false;
        var isLoggedIn = false;
        var isConnectedTree = false;
        SMB2Client? smbClient = null;
        ISMBFileStore? fileStore = null;
        try
        {
            UploadActive = true;
            piZeroCameraManager.UpdatePictureSet(pictureSetUuid);
            
            var pictureSetResult =
                await GetAndValidatePictureSet(piDbContext, pictureSetUuid, _uploadCancellationTokenSource.Token);

            if (pictureSetResult.IsFailure)
            {
                return Result.Failure(pictureSetResult.Error);
            }

            _uploadCancellationTokenSource.Token.ThrowIfCancellationRequested();

            var pictureSet = pictureSetResult.Value;
            
            var smbOptions = smbOptionsMonitor.CurrentValue;

            var connectResult = ConnectToSmb(smbOptions, _uploadCancellationTokenSource.Token);
            if (connectResult.IsFailure)
            {
                return Result.Failure(connectResult.Error);
            }

            (isConnected, isLoggedIn, isConnectedTree, smbClient, fileStore) = connectResult.Value;

            // Make sure that the "pictureSet.Name" directory exists
            var pictureSetExists = DirectoryExists(fileStore,
                PathCombineWithBackslashes(smbOptions.FileDirectory, pictureSet.Name));
            if (!pictureSetExists)
            {
                _uploadCancellationTokenSource.Token.ThrowIfCancellationRequested();
                CreateDirectory(fileStore, PathCombineWithBackslashes(smbOptions.FileDirectory, pictureSet.Name));
            }
            _uploadCancellationTokenSource.Token.ThrowIfCancellationRequested();

            // Make sure that the "pictureSet.Name/pictureSet.Uuid" directory exists
            var date = pictureSet.Created.ToLocalTime().ToString("yyyy-MM-ddTHHmm");
            var nestedDir =
                PathCombineWithBackslashes(smbOptions.FileDirectory, pictureSet.Name, date);
            if (!DirectoryExists(fileStore, nestedDir))
            {
                _uploadCancellationTokenSource.Token.ThrowIfCancellationRequested();
                CreateDirectory(fileStore, nestedDir);
            }
            _uploadCancellationTokenSource.Token.ThrowIfCancellationRequested();

            foreach (var pictureRequest in pictureSet.PictureRequests)
            {
                var path = Path.Combine(dirOptionsMonitor.CurrentValue.UploadDirectory, pictureRequest.Uuid.ToString());

                // directory name for request
                var dir = pictureRequest.PictureRequestType switch
                {
                    PictureRequestType.StandingSpread => "stav-starpa",
                    PictureRequestType.StandingTogether => "stav-kopa",
                    PictureRequestType.Sitting => "sez",
                    PictureRequestType.Mask => "maska",
                    PictureRequestType.Other => "other",
                    _ => throw new ArgumentOutOfRangeException()
                };
                dir += $"_{pictureRequest.Uuid}";

                // picture request directory
                if (!DirectoryExists(fileStore, PathCombineWithBackslashes(nestedDir, dir)))
                {
                    _uploadCancellationTokenSource.Token.ThrowIfCancellationRequested();
                    CreateDirectory(fileStore, PathCombineWithBackslashes(nestedDir, dir));
                }
                _uploadCancellationTokenSource.Token.ThrowIfCancellationRequested();

                // metadata directory
                if (!DirectoryExists(fileStore, PathCombineWithBackslashes(nestedDir, dir, "metadata")))
                {
                    _uploadCancellationTokenSource.Token.ThrowIfCancellationRequested();
                    CreateDirectory(fileStore, PathCombineWithBackslashes(nestedDir, dir, "metadata"));
                }
                _uploadCancellationTokenSource.Token.ThrowIfCancellationRequested();

                var sentCameraPictures = pictureRequest.CameraPictures.Where(x =>
                    x is
                    {
                        CameraPictureStatus: CameraPictureStatus.Success,
                        Synced: false
                    }
                ).ToList();

                foreach (var cameraPicture in sentCameraPictures)
                {
                    _uploadCancellationTokenSource.Token.ThrowIfCancellationRequested();
                    
                    var picturePath = Path.Combine(path, $"{pictureRequest.Uuid}_{cameraPicture.CameraId}.jpg");
                    var pictureFileName = Path.GetFileName(picturePath).Replace(pictureRequest.Uuid + "_", "");

                    // Just in case, this is actually already checked before
                    if (!File.Exists(picturePath))
                    {
                        return Result.Failure<PictureSetModel>($"File {picturePath} not found");
                    }
                    _uploadCancellationTokenSource.Token.ThrowIfCancellationRequested();

                    var copyResult = CopyFileToSmb(
                        fileStore, picturePath,
                        PathCombineWithBackslashes(nestedDir, dir, pictureFileName),
                        smbClient.MaxWriteSize,
                        _uploadCancellationTokenSource.Token
                    );

                    if (copyResult.IsSuccess)
                    {
                        cameraPicture.Synced = true;
                    }

                    var metadataPath = Path.Combine(path,
                        $"{pictureRequest.Uuid}_{cameraPicture.CameraId}_metadata.json");
                    var metadataFileName = Path.GetFileName(metadataPath).Replace(pictureRequest.Uuid + "_", "");
                    if (File.Exists(metadataPath))
                    {
                        _uploadCancellationTokenSource.Token.ThrowIfCancellationRequested();
                        _ = CopyFileToSmb(
                            fileStore,
                            metadataPath,
                            PathCombineWithBackslashes(nestedDir, dir, "metadata", metadataFileName),
                            smbClient.MaxWriteSize,
                            _uploadCancellationTokenSource.Token
                        );
                    }

                    await piDbContext.SaveChangesAsync(_uploadCancellationTokenSource.Token);
                    piZeroCameraManager.UpdatePictureSet(pictureSetUuid);
                }
            }
        }
        finally
        {
            // Save sync status
            await piDbContext.SaveChangesAsync(CancellationToken.None);

            // Make sure to clean up
            if (isConnectedTree && fileStore != null)
            {
                fileStore.Disconnect();
            }

            if (isLoggedIn && smbClient != null)
            {
                smbClient.Logoff();
            }

            if (isConnected && smbClient != null)
            {
                smbClient.Disconnect();
            }
            
            UploadActive = false;
            _uploadCancellationTokenSource?.Dispose();
            _uploadCancellationTokenSource = null;
            _uploadSemaphore.Release();
            piZeroCameraManager.UpdatePictureSet(pictureSetUuid);
        }

        return Result.Success();
    }

    /// <summary>
    /// Gets a picture set from the database and validates that all files are present
    /// </summary>
    /// <param name="piDbContext">Database context</param>
    /// <param name="pictureSetUuid">Uuid of the picture set</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Picture set with Picture Requests and Camera Pictures</returns>
    private async Task<Result<PictureSetModel>> GetAndValidatePictureSet(PiDbContext piDbContext, Guid pictureSetUuid,
        CancellationToken cancellationToken)
    {
        var pictureSet = await piDbContext.PictureSets
            .Include(x => x.PictureRequests.Where(y => y.IsActive))
            .ThenInclude(x => x.CameraPictures)
            .FirstOrDefaultAsync(x => x.Uuid == pictureSetUuid, cancellationToken);

        if (pictureSet == null)
        {
            return Result.Failure<PictureSetModel>("Picture set not found");
        }

        // check if all sent
        foreach (var pictureRequest in pictureSet.PictureRequests)
        {
            var path = Path.Combine(dirOptionsMonitor.CurrentValue.UploadDirectory, pictureRequest.Uuid.ToString());

            if (!Directory.Exists(path))
            {
                return Result.Failure<PictureSetModel>($"Directory {path} not found");
            }

            foreach (var cameraPicture in pictureRequest.CameraPictures)
            {
                var picturePath = Path.Combine(path, $"{pictureRequest.Uuid}_{cameraPicture.CameraId}.jpg");

                if (!File.Exists(picturePath))
                {
                    return Result.Failure<PictureSetModel>($"File {picturePath} not found");
                }
            }
        }

        return Result.Success(pictureSet);
    }

    /// <summary>
    /// Connects to SMB server and share
    /// </summary>
    /// <param name="smbOptions">The SMB options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Whether the client connected, logged in and connected to the tree and the filestore</returns>
    private Result<(
        bool isConnected,
        bool isLoggedIn,
        bool isConnectedTree,
        SMB2Client smbClient,
        ISMBFileStore fileStore)> ConnectToSmb(SmbOptions smbOptions, CancellationToken cancellationToken)
    {
        var isConnected = false;
        var isLoggedIn = false;
        var isConnectedTree = false;
        var smbClient = new SMB2Client();
        ISMBFileStore? fileStore = null;
        try
        {
            isConnected = smbClient.Connect(IPAddress.Parse(smbOptions.Host), SMBTransportType.DirectTCPTransport);
            if (!isConnected)
            {
                throw new Exception("Failed to connect");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var status = smbClient.Login(string.Empty, smbOptions.Username, smbOptions.Password);
            if (status != NTStatus.STATUS_SUCCESS)
            {
                throw new Exception("Failed to login");
            }

            isLoggedIn = true;
            cancellationToken.ThrowIfCancellationRequested();

            fileStore = smbClient.TreeConnect(smbOptions.ShareName, out status);
            if (status != NTStatus.STATUS_SUCCESS)
            {
                throw new Exception("TreeConnect failed");
            }

            isConnectedTree = true;
            cancellationToken.ThrowIfCancellationRequested();

            return Result.Success((isConnected, isLoggedIn, isConnectedTree, smbClient, fileStore));
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
                logger.LogError(ex,
                    "SMB - Connection error, IsConnected: {IsConnected}, IsLoggedIn: {IsLoggedIn}, IsConnectedTree: {IsConnectedTree}",
                    isConnected, isLoggedIn, isConnectedTree);

            if (isConnectedTree && fileStore != null)
            {
                fileStore.Disconnect();
            }

            if (isLoggedIn)
            {
                smbClient.Logoff();
            }

            if (isConnected)
            {
                smbClient.Disconnect();
            }

            // rethrow cancellation
            if (ex is OperationCanceledException) throw;

            return Result.Failure<(bool, bool, bool, SMB2Client, ISMBFileStore)>(ex.Message);
        }
    }

    /// <summary>
    /// Checks if a directory exists on the SMB server
    /// </summary>
    /// <param name="fileStore">SMB FileStore</param>
    /// <param name="directoryPath">Path to check</param>
    /// <returns></returns>
    private static bool DirectoryExists(ISMBFileStore fileStore, string directoryPath)
    {
        var status = fileStore.CreateFile(
            out var dirHandle,
            out _,
            directoryPath,
            AccessMask.SYNCHRONIZE | AccessMask.GENERIC_READ,
            FileAttributes.Directory,
            ShareAccess.Read | ShareAccess.Write,
            CreateDisposition.FILE_OPEN,
            CreateOptions.FILE_DIRECTORY_FILE,
            null
        );

        if (dirHandle != null) fileStore.CloseFile(dirHandle);

        return status == NTStatus.STATUS_SUCCESS;
    }

    /// <summary>
    /// Creates a directory on the SMB server
    /// </summary>
    /// <param name="fileStore">SMB FileStore</param>
    /// <param name="directoryPath">Directory to create</param>
    private void CreateDirectory(ISMBFileStore fileStore, string directoryPath)
    {
        var status = fileStore.CreateFile(
            out var dirHandle,
            out _,
            directoryPath,
            AccessMask.SYNCHRONIZE | AccessMask.GENERIC_READ,
            FileAttributes.Directory,
            ShareAccess.Read | ShareAccess.Write,
            CreateDisposition.FILE_CREATE,
            CreateOptions.FILE_DIRECTORY_FILE,
            null
        );

        if (dirHandle != null)
        {
            fileStore.CloseFile(dirHandle);
        }

        if (status != NTStatus.STATUS_SUCCESS)
        {
            logger.LogError("Failed to create directory: {DirectoryPath}", directoryPath);
        }
    }

    /// <summary>
    /// Copies a file in <paramref name="sourcePath"/> to <paramref name="targetPath"/> on the SMB server.
    /// </summary>
    /// <param name="fileStore">SMB FileStore</param>
    /// <param name="sourcePath">Local path</param>
    /// <param name="targetPath">SMB path</param>
    /// <param name="maxWriteSize">SMB client's max write size</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private Result CopyFileToSmb(ISMBFileStore fileStore, string sourcePath, string targetPath, uint maxWriteSize, CancellationToken cancellationToken)
    {
        object? fileHandle = null;

        try
        {
            using var sourceStream = File.OpenRead(sourcePath);

            var status = fileStore.CreateFile(
                out fileHandle,
                out _,
                targetPath,
                AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE,
                FileAttributes.Normal,
                ShareAccess.Write,
                CreateDisposition.FILE_CREATE,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                null
            );

            if (status != NTStatus.STATUS_SUCCESS)
            {
                logger.LogError("Failed to create target file: {TargetPath}", targetPath);
                return Result.Failure($"Failed to create target file {targetPath}");
            }
            cancellationToken.ThrowIfCancellationRequested();

            // Write in chunks, as the SMB client's max write size may be smaller than the file size
            var buffer = new byte[maxWriteSize];
            int bytesRead;
            // Offset for the written file
            long offset = 0;

            while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Buffer is not always fully filled, take only what was read
                var actualBuffer = buffer.Take(bytesRead).ToArray();

                status = fileStore.WriteFile(out var numberOfBytesWritten, fileHandle, offset, actualBuffer);
                if (status != NTStatus.STATUS_SUCCESS)
                {
                    logger.LogError("Failed to write to target file: {TargetPath}", targetPath);
                    return Result.Failure($"Failed to write to target file {targetPath}");
                }

                // Add the written bytes to the offset
                offset += numberOfBytesWritten;
            }
        }
        finally
        {
            // Close the SMB file handle in case of success or failure
            if (fileHandle != null)
            {
                fileStore.CloseFile(fileHandle);
            }
        }

        return Result.Success();
    }

    private static string PathCombineWithBackslashes(params string[] paths)
    {
        var combinedPath = Path.Combine(paths);

        // Replace any forward slashes with backslashes for consistency
        return combinedPath.Replace('/', '\\');
    }
    
    public async Task CancelUpload()
    {
        if (_uploadCancellationTokenSource != null)
        {
            await _uploadCancellationTokenSource.CancelAsync();
        }
    }
}